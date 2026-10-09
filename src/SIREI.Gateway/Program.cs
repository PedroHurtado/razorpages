// Puerta de entrada a SIREI: todas las peticiones del navegador llegan aquí y YARP las reenvía a SIREI.Web.
// Rutas, clústeres y comprobaciones de salud se definen en appsettings.json (sección "ReverseProxy").
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
