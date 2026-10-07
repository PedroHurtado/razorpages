using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.WebEncoders;
using Microsoft.Net.Http.Headers;
using SIREI.Servicios.Fake;
using SIREI.Web.Infraestructura;
using WebMarkupMin.AspNetCoreLatest;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-ES");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
// Emitir acentos y eñes tal cual en el HTML, no como entidades
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddServiciosFake();

// Minificación del HTML generado por Razor (quita espacios, comentarios, comillas innecesarias...).
// Por defecto WebMarkupMin no actúa en Development; se activa para que desarrollo se comporte como producción.
// DisablePoweredByHttpHeaders: no enviar "X-HTML-Minification-Powered-By: WebMarkupMin"
// (no aporta nada al cliente y revela qué librería usa el servidor).
builder.Services.AddWebMarkupMin(o =>
    {
        o.AllowMinificationInDevelopmentEnvironment = true;
        o.DisablePoweredByHttpHeaders = true;
    })
    .AddHtmlMinification();

// Output Cache: guarda en memoria del servidor el HTML ya minificado (ver Infraestructura/CacheHtmlPolicy.cs).
builder.Services.AddOutputCache(o => o.AddPolicy(CacheHtmlPolicy.Nombre, new CacheHtmlPolicy()));

var app = builder.Build();

// Al principio: así también la llevan las páginas de error, las de UseStatusCodePages y los aciertos del Output Cache
app.UseContentSecurityPolicy();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?codigo={0}");
app.UseRouting();
// Antes que la caché: un acierto no debe saltarse la autorización
app.UseAuthorization();

// ORDEN IMPORTANTE: UseOutputCache va ANTES que UseWebMarkupMin.
//   - En la ida, si hay acierto de caché se responde aquí y no se ejecuta nada de lo que sigue.
//   - En la vuelta, WebMarkupMin ya ha minificado, así que lo que se guarda es el HTML minificado.
app.UseOutputCache();

// Cualquier POST/PUT/PATCH/DELETE que termine bien puede haber cambiado incidencias:
// se vacía la caché ANTES de enviar la respuesta (el 302 del Post-Redirect-Get o el fragmento htmx).
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.OnStarting(async () =>
        {
            if (context.Response.StatusCode < 400)
            {
                var cache = context.RequestServices.GetRequiredService<IOutputCacheStore>();
                await cache.EvictByTagAsync(CacheHtmlPolicy.Etiqueta, default);
            }
        });
    }
    await next(context);
});

// Debe ir antes de los endpoints que generan HTML.
app.UseWebMarkupMin();

// Las URLs con huella (sirei.min.x4wtre2m4d.css) se sirven con "Cache-Control: max-age=31536000, immutable":
// el navegador nunca revalida, así que ETag y Last-Modified sobran. Se quitan solo en esas respuestas;
// las URLs sin huella (no-cache) los conservan porque los necesitan para responder 304.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        if (headers.CacheControl.ToString().Contains("immutable"))
        {
            headers.Remove(HeaderNames.ETag);
            headers.Remove(HeaderNames.LastModified);
        }
        return Task.CompletedTask;
    });
    return next(context);
});

// Ficheros de wwwroot optimizados (compresión, caché, huella en el nombre).
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets()
   .CacheOutput(CacheHtmlPolicy.Nombre);

app.Run();
