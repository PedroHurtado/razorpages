using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
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

// esbuild emits linked source maps (*.js.map) next to each bundle.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".map"] = "application/json";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(distPath),
    ContentTypeProvider = contentTypes,
});

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// Progressive streaming: the shell is flushed at once and every <boundary> is
// flushed (and hydrated in the browser) as soon as its data is ready.
app.MapGet("/", async (HttpContext ctx, Protocol protocol, WebUIHandler handler, TaskStore store) =>
{
    var aborted = ctx.RequestAborted;

    // Start every slow load now so they run in parallel; boundaries are still
    // emitted in document order, so each one waits only for its own data.
    var factsTask = store.LoadFactsAsync(aborted);
    var tasksTask = store.LoadTasksAsync(aborted);

    // Shell state: only what is rendered outside the boundaries.
    var shellState = new
    {
        Title = "WebUI + ASP.NET Core (streaming)",
        Server = new
        {
            Runtime = $".NET {Environment.Version}",
            RenderedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
        },
    };

    ctx.Response.ContentType = "text/html; charset=utf-8";
    ctx.Response.Headers.CacheControl = "no-store";
    // Ask reverse proxies (nginx and similar) not to buffer the chunks.
    ctx.Response.Headers["X-Accel-Buffering"] = "no";
    ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

    using var session = handler.StreamResponse(protocol, "index.html", ctx.Request.Path);
    var step = session.Start(JsonSerializer.Serialize(shellState, jsonOptions));

    while (true)
    {
        await ctx.Response.Body.WriteAsync(step.Bytes, aborted);
        await ctx.Response.Body.FlushAsync(aborted);
        if (step.Done) break;

        if (step.Boundary is BoundaryDescriptor boundary)
        {
            object state = boundary.Name switch
            {
                "facts" => new { Facts = await factsTask },
                "tasks" => TasksState(await tasksTask),
                _ => throw new InvalidOperationException(
                    $"Boundary sin estado: {boundary.Owner}/{boundary.Name}"),
            };
            step = session.Resume(boundary.InstanceId, JsonSerializer.Serialize(state, jsonOptions), BoundaryMode.Final);
        }
        else
        {
            step = session.Advance();
        }
    }
});

// Same page rendered in one piece, to compare against streaming.
app.MapGet("/buffered", async (HttpContext ctx, Protocol protocol, WebUIHandler handler, TaskStore store) =>
{
    var factsTask = store.LoadFactsAsync(ctx.RequestAborted);
    var tasksTask = store.LoadTasksAsync(ctx.RequestAborted);
    var items = await tasksTask;
    var state = new
    {
        Title = "WebUI + ASP.NET Core (buffered)",
        Server = new
        {
            Runtime = $".NET {Environment.Version}",
            RenderedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
        },
        Facts = await factsTask,
        BoardHeading = "Tareas",
        Items = items,
        PendingCount = items.Count(t => !t.Done),
    };

    var html = handler.Render(protocol, JsonSerializer.Serialize(state, jsonOptions), "index.html", ctx.Request.Path);
    return Results.Content(html, "text/html; charset=utf-8");
});

static object TasksState(IReadOnlyList<TaskItem> items) => new
{
    BoardHeading = "Tareas",
    Items = items,
    PendingCount = items.Count(t => !t.Done),
};

app.Run();

record TaskItem(string Id, string Title, bool Done);

record Fact(string Label, string Value);

// Simulates slow data sources (database, remote API) so the streaming is visible.
sealed class TaskStore(IConfiguration config)
{
    private readonly List<TaskItem> _items =
    [
        new("1", "Compilar plantillas con webui build", true),
        new("2", "Renderizar protocol.bin desde C#", true),
        new("3", "Hidratar el componente interactivo", false),
        new("4", "Evaluar encaje con SIREI", false),
    ];

    public async Task<IReadOnlyList<Fact>> LoadFactsAsync(CancellationToken ct)
    {
        await Task.Delay(config.GetValue("WebUi:FactsDelayMs", 400), ct);
        return
        [
            new("Motor", "Microsoft.WebUI 0.0.26"),
            new("Sistema", Environment.OSVersion.VersionString),
            new("Tareas", _items.Count.ToString()),
        ];
    }

    public async Task<IReadOnlyList<TaskItem>> LoadTasksAsync(CancellationToken ct)
    {
        await Task.Delay(config.GetValue("WebUi:TasksDelayMs", 1500), ct);
        return _items;
    }
}
