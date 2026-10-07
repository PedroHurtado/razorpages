using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using SIREI.Servicios.Fake;
using SIREI.Web.Infraestructura;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("es-ES");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
// Emitir acentos y eñes tal cual en el HTML, no como entidades
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddServiciosFake();

var app = builder.Build();

// Al principio: así también la llevan las páginas de error y las de UseStatusCodePages
app.UseContentSecurityPolicy();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?codigo={0}");
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
