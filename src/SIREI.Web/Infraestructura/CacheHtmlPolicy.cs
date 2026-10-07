using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace SIREI.Web.Infraestructura;

/// <summary>
/// Política de Output Cache para las páginas Razor y sus fragmentos htmx.
///
/// La respuesta se guarda YA minificada (UseOutputCache va antes que UseWebMarkupMin),
/// así que en un acierto de caché no se ejecuta ni el PageModel, ni Razor, ni la minificación.
///
/// La política por defecto (.CacheOutput()) no sirve aquí porque estas páginas tienen partes "por usuario":
///   - Token antiforgery (meta csrf-token del layout y formularios POST) → la clave de caché varía por la
///     cookie antiforgery, así cada usuario recibe SU token. Sin cookie, la respuesta trae Set-Cookie y no se guarda.
///   - Avisos de TempData (Post-Redirect-Get sin JavaScript) → si la petición trae la cookie de TempData,
///     ni se lee ni se guarda en caché: hay un aviso de un solo uso que mostrar.
/// </summary>
public sealed class CacheHtmlPolicy : IOutputCachePolicy
{
    public const string Nombre = "Html";
    public const string Etiqueta = "incidencias";   // se invalida tras cualquier cambio (ver Program.cs)

    private static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);

    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        var request = http.Request;
        var servicios = http.RequestServices;

        var cookieTempData = servicios.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name!;
        var cookieAntiforgery = servicios.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name!;

        var cacheable =
            (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && http.User.Identity?.IsAuthenticated != true      // contenido personalizado: nunca compartirlo
            && !request.Cookies.ContainsKey(cookieTempData);    // hay un aviso pendiente de mostrar

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;                            // peticiones simultáneas: solo una genera la página
        context.ResponseExpirationTimeSpan = Duracion;
        context.Tags.Add(Etiqueta);

        // Clave = ruta + query string completo + cookie antiforgery del usuario.
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.VaryByValues["antiforgery"] = request.Cookies[cookieAntiforgery] ?? "";

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;

        // Solo respuestas 200 y sin cookies: una cookie guardada en caché se repartiría a todos los usuarios.
        if (response.StatusCode != StatusCodes.Status200OK || response.Headers.SetCookie.Count > 0)
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
