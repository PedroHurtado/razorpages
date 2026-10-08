using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.WebUI;

var builder = WebApplication.CreateBuilder(args);

// Templates are compiled by `npm run build` in ../client into protocol.bin + client assets.
var distPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["WebUi:DistPath"] ?? "../client/dist"));
var protocolPath = Path.Combine(distPath, "protocol.bin");
if (!File.Exists(protocolPath))
{
    throw new FileNotFoundException(
        $"No se encuentra {protocolPath}. Ejecuta 'npm install' y 'npm run build' en poc/webui/client.");
}

// Protocol and handler are thread-safe: load once, reuse for every request.
builder.Services.AddSingleton(_ => new Protocol(File.ReadAllBytes(protocolPath)));
builder.Services.AddSingleton(_ => new WebUIHandler("webui"));
builder.Services.AddSingleton<TaskStore>();

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(distPath) });

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/", (Protocol protocol, WebUIHandler handler, TaskStore store) =>
{
    var items = store.All();
    var state = new
    {
        Title = "WebUI + ASP.NET Core",
        Server = new
        {
            Runtime = $".NET {Environment.Version}",
            RenderedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
        },
        Facts = new[]
        {
            new { Label = "Motor", Value = "Microsoft.WebUI 0.0.26" },
            new { Label = "Sistema", Value = Environment.OSVersion.VersionString },
            new { Label = "Tareas", Value = items.Count.ToString() },
        },
        BoardHeading = "Tareas",
        Items = items,
        PendingCount = items.Count(t => !t.Done),
    };

    var html = handler.Render(protocol, JsonSerializer.Serialize(state, jsonOptions), "index.html", "/");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.Run();

record TaskItem(string Id, string Title, bool Done);

sealed class TaskStore
{
    private readonly List<TaskItem> _items =
    [
        new("1", "Compilar plantillas con webui build", true),
        new("2", "Renderizar protocol.bin desde C#", true),
        new("3", "Hidratar el componente interactivo", false),
        new("4", "Evaluar encaje con SIREI", false),
    ];

    public IReadOnlyList<TaskItem> All() => _items;
}
