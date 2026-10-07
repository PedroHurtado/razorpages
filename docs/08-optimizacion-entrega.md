# 8. Optimización de la entrega: minificación, caché, compresión y CSP

Este capítulo es **material complementario**: no está en la agenda del día, pero recoge las optimizaciones que se han aplicado al proyecto de [src/day-02](../../src/day-02/) después de analizar el rendimiento de `/Paginas/Incidencias`.

El objetivo es enviar **menos bytes**, **menos veces** y gastando **menos CPU** en el servidor, sin romper nada de lo visto en el día (antiforgery, TempData, Post-Redirect-Get).

Al final se añade una **Content Security Policy** (apartado 8.7). No es una optimización, sino un arnés para que nadie escriba JavaScript ni CSS inline, algo que además rompería la separación entre HTML y ficheros estáticos en la que se apoya todo lo anterior.

## 8.1 Punto de partida

Lo que devolvía el servidor antes de tocar nada, medido con `curl`:

| Recurso | Problema |
|---|---|
| HTML de `/Paginas/Incidencias` | 4290 bytes, sin minificar y sin comprimir |
| `site.css` | 3905 bytes, sin minificar |
| CSS en Development | `Cache-Control: no-cache` + `ETag` + `Last-Modified`: el navegador lo revalida en cada página |
| CSS en las vistas MVC | Se enlazaba `/css/site.css` **sin huella** (solo Razor Pages la tenía) |
| Todas las páginas HTML | Llevaban un `Set-Cookie` en cada respuesta, lo que impide cachearlas |

Y el resultado final:

| Recurso | Antes | Después |
|---|---|---|
| HTML | 4290 bytes, generado en cada petición | 2910 bytes (−32 %), servido desde caché |
| CSS | 3905 bytes, revalidado en cada página | 3122 bytes (1183 con gzip, 986 con Brotli al publicar), en caché del navegador un año |

## 8.2 Minificar el CSS al compilar

El CSS es un fichero estático: se minifica **una vez, al compilar**, no en cada petición.

Se hace con una **tarea MSBuild propia** dentro del [.csproj](../../src/day-02/GestorIncidencias.Web/GestorIncidencias.Web.csproj), que usa la librería [NUglify](https://github.com/trullock/NUglify) para generar `wwwroot/css/site.min.css` a partir de `site.css`.

Se sigue editando `site.css`; `site.min.css` se genera en cada compilación, está en `.gitignore` y es el que enlaza el layout:

```cshtml
<link rel="stylesheet" href="~/css/site.min.css" asp-append-version="true" />
```

Las piezas del `.csproj`:

```xml
<!-- 1. NUglify solo se usa al compilar: no llega a bin ni al código -->
<PackageReference Include="NUglify" Version="1.23.3" GeneratePathProperty="true" PrivateAssets="all" ExcludeAssets="all" />

<!-- 2. Qué ficheros se minifican y dónde se escribe el resultado -->
<ItemGroup>
  <CssMinificable Include="wwwroot\css\site.css" Destino="wwwroot\css\site.min.css" />
</ItemGroup>

<!-- 3. Tarea inline (C# compilado por MSBuild) que llama a NUglify -->
<UsingTask TaskName="MinificarCss" TaskFactory="RoslynCodeTaskFactory" ...>
  ...
  var resultado = Uglify.Css(File.ReadAllText(Origen), Origen);
  // errores del CSS → error de compilación apuntando a site.css
  if (!resultado.HasErrors) File.WriteAllText(Destino, resultado.Code);
</UsingTask>

<!-- 4. Cuándo se ejecuta (ver la trampa de abajo) -->
<Target Name="MinificarCss" BeforeTargets="ResolveProjectStaticWebAssets"
        Inputs="@(CssMinificable)" Outputs="@(CssMinificable->'%(Destino)')">
  <MinificarCss Origen="%(CssMinificable.FullPath)" Destino="%(CssMinificable.Destino)" />
  <ItemGroup>
    <Content Include="@(CssMinificable->'%(Destino)')" Exclude="@(Content)" />
  </ItemGroup>
</Target>
```

- `Inputs`/`Outputs`: solo se vuelve a minificar si `site.css` es más reciente que `site.min.css`.
- Si el CSS tiene un error de sintaxis, la compilación falla con un error que apunta a `site.css` (por ejemplo `CSS1066: Unexpected end of file encountered`).
- El `<Content Include=...>` cubre la compilación limpia: si `site.min.css` no existía al empezar, se añade para que `MapStaticAssets` lo registre en esa misma compilación.
- `dotnet clean` borra `site.min.css` (target `LimpiarCssMinificado`).

### ¿Por qué no un paquete como BuildBundlerMinifier?

Se probó primero `BuildBundlerMinifier2022` (con un `bundleconfig.json`). Funciona, pero tiene dos inconvenientes:

1. Escribe "Bundler: Begin/Done processing bundleconfig.json" con importancia alta en **cada** compilación, y VS Code los muestra en la ventana **Problemas** como si fueran avisos. No se puede configurar.
2. Se ejecuta en `BeforeCompile`, demasiado tarde para la huella (ver la trampa siguiente).

La tarea propia son unas 30 líneas, usa el mismo motor (el paquete también usa NUglify por dentro) y solo dice algo cuando hay errores.

### Trampa: la huella puede ir una compilación por detrás

`MapStaticAssets` calcula la huella del fichero (`site.min.x4wtre2m4d.css`) en el target `ResolveProjectStaticWebAssets`. Si el CSS se minifica después (como hacía `BuildBundlerMinifier2022`, en `BeforeCompile`), al cambiar `site.css` y compilar, la URL sigue teniendo **el hash del contenido anterior**.

Con la caché de un año del apartado siguiente esto es grave: el navegador podría quedarse con un CSS que no corresponde a su URL. Por eso el target lleva `BeforeTargets="ResolveProjectStaticWebAssets"`.

Comprobación: cambiar un color en `site.css` y compilar **una sola vez** produce una URL nueva (`site.min.7iwlkjmb12.css`); deshacer el cambio vuelve a la original.

## 8.3 Caché del CSS en el navegador: huella + `immutable`

La idea: si el nombre del fichero cambia cada vez que cambia su contenido, esa URL **nunca** cambia de contenido y se puede cachear para siempre.

| URL | Cabeceras | Quién la usa |
|---|---|---|
| `/css/site.min.x4wtre2m4d.css` (con huella) | `Cache-Control: max-age=31536000, immutable` | Todas las páginas |
| `/css/site.min.css` (sin huella) | `Cache-Control: no-cache` + `ETag` → responde `304` | Nadie (solo si se teclea a mano) |

`31536000` segundos = 365 × 24 × 60 × 60 = un año. Al publicar una versión nueva, el HTML apunta a otra URL y el navegador descarga el CSS nuevo.

Para llegar a esto hubo que resolver tres cosas.

**1. Development desactiva la caché.** Con `dotnet run`, `MapStaticAssets` sirve todo con `no-cache` para que los cambios se vean al instante. Al publicar ya se usaba `immutable`. Para que desarrollo se comporte como producción, en [appsettings.Development.json](../../src/day-02/GestorIncidencias.Web/appsettings.Development.json):

```json
"EnableStaticAssetsDevelopmentCaching": true
```

**2. `.WithStaticAssets()` no funciona encadenado a `MapControllerRoute`.** En .NET 10 el builder que devuelve `MapControllerRoute(...)` no lleva la referencia que necesita `WithStaticAssets()`, y la llamada no hace nada (sin error ni aviso). Por eso las vistas MVC enlazaban el CSS sin huella. Se aplica sobre `MapControllers()`, que devuelve el builder común a todos los controladores:

```csharp
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")
    .CacheOutput(CacheHtmlPolicy.Nombre);

// Aquí sí funciona. Los [ApiController] se excluyen solos y no se duplican rutas.
app.MapControllers()
    .WithStaticAssets();
```

**3. `ETag` y `Last-Modified` sobran con `immutable`.** El navegador no va a revalidar, así que no se usan. Un pequeño middleware los quita **solo** en las respuestas `immutable`; las URLs con `no-cache` los conservan porque los necesitan para el `304`:

```csharp
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
```

## 8.4 Minificar el HTML

El HTML lo genera Razor en cada petición, así que no se puede minificar al compilar. Se usa el middleware de `WebMarkupMin.AspNetCoreLatest`:

```csharp
builder.Services.AddWebMarkupMin(o =>
    {
        // Por defecto WebMarkupMin no actúa en Development; se activa para poder verlo en el curso.
        o.AllowMinificationInDevelopmentEnvironment = true;
        // No enviar "X-HTML-Minification-Powered-By: WebMarkupMin".
        o.DisablePoweredByHttpHeaders = true;
    })
    .AddHtmlMinification();

app.UseWebMarkupMin();   // antes de los endpoints que generan HTML
```

Quita espacios, saltos de línea, comentarios y comillas innecesarias en los atributos:

```html
<!DOCTYPE html><html lang=es><head><meta charset=utf-8>...<link rel=stylesheet href=/css/site.min.x4wtre2m4d.css>
```

**Sin cabecera "Powered-By".** Por defecto WebMarkupMin añade `X-HTML-Minification-Powered-By: WebMarkupMin` a cada respuesta. No aporta nada al navegador y le dice a cualquiera qué librería usa el servidor, así que se desactiva con `DisablePoweredByHttpHeaders`. Las respuestas siguen llevando `Server: Kestrel`; si también se quisiera quitar: `builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false)`.

**El precio: CPU en cada petición.** Medido con WebMarkupMin 2.22.5: unos **70 µs por KB** de HTML (≈ 0,2 ms para esta página, ≈ 5 ms para una de 70 KB). Es lineal con el tamaño, así que en listados grandes se nota. El apartado siguiente lo elimina.

## 8.5 Output Cache: no minificar (ni renderizar) en cada petición

El **Output Cache** guarda la respuesta completa en la memoria del servidor. En un acierto no se ejecuta el controlador, ni la consulta a la base de datos, ni Razor, ni la minificación.

### El orden del pipeline lo es todo

```csharp
app.UseRouting();
app.UseOutputCache();    // 1º
app.UseWebMarkupMin();   // 2º
// endpoints
```

```
  Petición ──▶ OutputCache ──▶ WebMarkupMin ──▶ Razor Page
                   │  ▲              │               │
      acierto ◀────┘  │              │◀── HTML ──────┘
                      └── guarda ◀───┘ minificado
```

- **A la ida**: si hay acierto, OutputCache responde y no se ejecuta nada más.
- **A la vuelta**: WebMarkupMin ya ha minificado, así que lo que se guarda es el HTML minificado.

Con el orden al revés se cachearía el HTML sin minificar y se minificaría en cada acierto.

### Por qué no basta con `.CacheOutput()`

La política por defecto no cachea respuestas con `Set-Cookie`, y **todas** las páginas llevaban uno. Además, estas páginas tienen partes que son de cada usuario:

| Problema | Por qué importa | Solución |
|---|---|---|
| `_Mensajes.cshtml` leía TempData en cada petición | Leer TempData sin cookie genera un `Set-Cookie` de borrado en **todas** las respuestas | Solo se lee TempData si la petición trae su cookie |
| Formularios POST con token antiforgery | Un token cacheado y compartido se entregaría a todos los usuarios | La clave de caché incluye la cookie antiforgery: cada usuario tiene su copia |
| Mensajes de TempData tras un POST (PRG) | Una página cacheada no mostraría el mensaje | Si la petición trae la cookie de TempData, no se usa la caché |
| Los datos cambian (Resolver, Crear...) | Se servirían listados desfasados | Tras cualquier POST/PUT/PATCH/DELETE correcto se invalida la etiqueta `incidencias` |

Todo esto está en una política propia, [Cache/CacheHtmlPolicy.cs](../../src/day-02/GestorIncidencias.Web/Cache/CacheHtmlPolicy.cs):

```csharp
var cacheable =
    (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
    && http.User.Identity?.IsAuthenticated != true      // contenido personalizado: nunca compartirlo
    && !request.Cookies.ContainsKey(cookieTempData);    // hay un mensaje pendiente de mostrar

context.ResponseExpirationTimeSpan = TimeSpan.FromSeconds(60);
context.Tags.Add("incidencias");

// Clave = ruta + query string completo + cookie antiforgery del usuario
context.CacheVaryByRules.QueryKeys = "*";
context.CacheVaryByRules.VaryByValues["antiforgery"] = request.Cookies[cookieAntiforgery] ?? "";
```

Y al terminar, solo se guardan respuestas `200` **sin** `Set-Cookie`: una cookie cacheada se repartiría a todos los usuarios.

La invalidación se hace en `OnStarting`, es decir, **antes** de enviar el `302` del Post-Redirect-Get. Así la petición GET que viene a continuación ya ve los datos nuevos:

```csharp
context.Response.OnStarting(async () =>
{
    if (context.Response.StatusCode < 400)
        await cache.EvictByTagAsync(CacheHtmlPolicy.Etiqueta, default);
});
```

### Lo que se comprobó

| Escenario | Resultado |
|---|---|
| Mismo usuario, tercera petición | Servida desde caché (cabecera `Age`) |
| Usuarios A y B | Cada uno recibe un token distinto; B enviando el token de A → `400` |
| POST "Resolver" | El redirect muestra "Incidencia 3 resuelta." con los datos actualizados |
| Siguiente GET | Sin mensaje y otra vez desde caché |
| Otro usuario tras el cambio | También ve los datos actualizados |
| `X-Correlation-Id` | Es nuevo en cada respuesta, también en los aciertos |

### Límites de esta caché

- Las páginas con formulario solo se sirven desde caché **a partir de la segunda visita** del mismo usuario (en la primera se crea la cookie antiforgery). Las páginas sin formulario, como `/`, se comparten entre todos.
- Está en la memoria de **un** servidor. Con varias instancias hace falta un almacén compartido (Redis con `AddStackExchangeRedisOutputCache`) para que la invalidación llegue a todas.
- En desarrollo, al editar una vista se puede ver el HTML anterior durante 60 segundos.

## 8.6 Compresión del HTML: no se implementa

El HTML dinámico de Razor **no se comprime**: `MapStaticAssets` solo comprime ficheros de `wwwroot`. El CSS sí sale comprimido (gzip en desarrollo; gzip y Brotli al publicar).

ASP.NET Core trae un middleware de compresión (`AddResponseCompression`, con Brotli y gzip), pero **se ha decidido no usarlo**, siguiendo la recomendación de Microsoft:

> *Use tecnologías de compresión de respuesta basadas en servidor en IIS, Apache o Nginx. Es probable que el rendimiento del middleware de compresión de respuesta no coincida con el de los módulos de servidor.*
> — [Compresión de respuesta en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/performance/response-compression?view=aspnetcore-10.0)

En producción la aplicación estará detrás de un proxy o servidor web (IIS, nginx, YARP o una CDN), y la compresión corresponde a esa capa. Una CDN suele ofrecer además **zstd**, que ASP.NET Core 10 no trae de serie.

**Si algún día no hubiera proxy**, estos serían los puntos a tener en cuenta:

1. **Orden**: `UseOutputCache → UseResponseCompression → UseWebMarkupMin`, para cachear los bytes ya comprimidos. Con la compresión fuera de la caché, se comprimiría en cada acierto.
2. **Clave de caché**: variar por la codificación **normalizada** (br / gzip / ninguna), no por la cabecera `Accept-Encoding` tal cual, que cambia de un navegador a otro.
3. **Nivel**: con caché se comprime una vez por entrada, así que se puede usar `CompressionLevel.SmallestSize` en Brotli.
4. **HTTPS y BREACH**: el middleware no comprime en HTTPS por defecto (`EnableForHttps = false`). Las páginas que mezclan un secreto (el token antiforgery) con texto reflejado de la petición (el filtro `?Estado=`) son el caso que ataca BREACH; hay que evaluarlo antes de activarlo.
5. **zstd**: no compensa aquí. Su ventaja es la velocidad de compresión, y con Output Cache se comprime una sola vez; Brotli al máximo nivel da un tamaño igual o menor en HTML.

## 8.7 Content Security Policy: arnés contra JavaScript y CSS inline

La cabecera `Content-Security-Policy` le dice al navegador qué puede ejecutar y aplicar la página. Aquí se usa una política **estricta a propósito**, como arnés para el desarrollador: si alguien escribe JavaScript o CSS inline, no funciona. En producción se ajustará la política según lo que haga falta.

### La política

Está en [appsettings.json](../../src/day-02/GestorIncidencias.Web/appsettings.json), no en el código, para poder cambiarla por entorno (`appsettings.Production.json` o la variable de entorno `ContentSecurityPolicy`):

```json
"ContentSecurityPolicy": "script-src 'self'; script-src-attr 'none'; style-src 'self'; style-src-attr 'none'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'"
```

| Lo que escribe el desarrollador | Qué pasa | Directiva |
|---|---|---|
| `<script>...</script>` | No se ejecuta | `script-src 'self'` |
| `onclick="..."`, `onerror="..."` y demás `on*` | No se ejecuta | `script-src-attr 'none'` |
| `href="javascript:..."` | No se ejecuta | `script-src 'self'` (sin `'unsafe-inline'`) |
| `<style>...</style>` | No se aplica | `style-src 'self'` |
| `style="..."` en cualquier elemento | No se aplica | `style-src-attr 'none'` |
| `<script src="/js/app.js">`, `<link href="/css/site.min.css">` | Funciona | `'self'` = mismo origen |

Las cuatro últimas directivas son protecciones baratas que no molestan: sin plugins (`object-src 'none'`), sin `<base>` que redirija las URLs relativas (`base-uri`), formularios solo hacia el propio sitio (`form-action`) y la página solo se puede meter en un iframe del mismo sitio (`frame-ancestors`).

**No se pone `default-src` a propósito.** Restringiría también conexiones, imágenes y fuentes. Por ejemplo, cortaría el websocket que usa `dotnet watch` para recargar el navegador, que va a otro puerto. El objetivo aquí es solo lo inline.

### El middleware

[Middleware/ContentSecurityPolicyMiddleware.cs](../../src/day-02/GestorIncidencias.Web/Middleware/ContentSecurityPolicyMiddleware.cs) sigue el mismo patrón que `CorrelationIdMiddleware` del día 1:

```csharp
public class ContentSecurityPolicyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    // Se lee una sola vez: el middleware es singleton.
    private readonly string? _politica = configuration["ContentSecurityPolicy"];

    public Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_politica))
            return next(context);

        // El Content-Type se conoce justo antes de enviar las cabeceras, no al entrar.
        context.Response.OnStarting(() =>
        {
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                context.Response.Headers.ContentSecurityPolicy = _politica;

            return Task.CompletedTask;
        });

        return next(context);
    }
}
```

**Solo en las páginas.** La CSP la aplica el navegador al **documento**: es la página la que decide qué scripts y estilos puede cargar o ejecutar. En los recursos que carga la página (CSS, JS, imágenes) o en el JSON de la API la cabecera no tendría ningún efecto; serían bytes de más en cada respuesta. Por eso solo se añade cuando el `Content-Type` es `text/html`.

Ese `Content-Type` no se conoce al entrar en el middleware (todavía no se ha ejecutado el controlador), así que la comprobación se hace en `OnStarting`, justo antes de enviar las cabeceras.

Se registra al principio del pipeline, justo después de `UseCorrelationId()`. Así `OnStarting` también se ejecuta en los aciertos del Output Cache, que no ejecutan nada de lo que viene detrás.

### Qué ve el desarrollador

Razor **no da ningún error** al compilar: el HTML se genera igual. Es el navegador el que bloquea y lo explica en la consola (F12):

```
Executing inline script violates the following Content Security Policy directive 'script-src 'self''.
... The action has been blocked.
```

La solución es siempre la misma: mover el código a un fichero de `wwwroot/js` o `wwwroot/css`, y en JavaScript usar `addEventListener` en lugar de `onclick`.

### Lo que se comprobó

Con Chrome en modo headless:

| Prueba | Resultado |
|---|---|
| Vista con `<style>`, `style=""`, `<script>`, `onerror` y `javascript:`, **sin** CSP | Los tres scripts se ejecutan |
| La misma vista **con** CSP | No se ejecuta ninguno; la consola muestra las 5 violaciones |
| Las 6 páginas reales del proyecto | 0 violaciones; el CSS se carga con normalidad |

Y con `curl`, qué respuestas llevan la cabecera:

| Respuesta | `Content-Type` | CSP |
|---|---|---|
| Las 6 páginas MVC y Razor Pages | `text/html` | Sí |
| Página servida desde el Output Cache | `text/html` | Sí |
| CSS (con y sin huella) | `text/css` | No |
| API, también su 404 con ProblemDetails | `application/json` | No |
| `/openapi/v1.json` | `application/json` | No |
| 404 sin cuerpo | — | No |

## 8.8 El pipeline completo

```csharp
app.UseCorrelationId();
app.UseContentSecurityPolicy();       // 8.7  CSP solo en las respuestas text/html
app.UseHttpsRedirection();
app.UseRouting();

app.UseOutputCache();                 // 8.5  respuesta HTML cacheada (ya minificada)
app.Use(/* invalidar tras POST */);   // 8.5  vacía la etiqueta "incidencias"
app.UseWebMarkupMin();                // 8.4  minifica el HTML
app.Use(/* quitar ETag/Last-Modified si immutable */);   // 8.3

app.MapStaticAssets();                // 8.2-8.3  CSS minificado, con huella, comprimido

app.MapControllerRoute(...).CacheOutput(CacheHtmlPolicy.Nombre);
app.MapControllers().WithStaticAssets();
app.MapRazorPages().WithStaticAssets().CacheOutput(CacheHtmlPolicy.Nombre);
```

## 8.9 Cómo comprobarlo

Con la aplicación arrancada (`dotnet run --project GestorIncidencias.Web`):

```bash
# HTML minificado
curl -s http://localhost:5196/Paginas/Incidencias | head -c 300

# Cabeceras de la página: debe salir Content-Security-Policy y NO X-HTML-Minification-Powered-By
curl -s -D - -o /dev/null http://localhost:5196/Paginas/Incidencias | grep -iE "content-security|powered"

# Qué CSS enlaza la página (debe llevar huella, también en /Incidencias y en /)
curl -s http://localhost:5196/Incidencias | grep -o '/css/[^" >]*css'

# Cabeceras del CSS con huella: max-age=31536000, immutable y sin ETag
curl -s -I -H "Accept-Encoding: gzip" http://localhost:5196/css/site.min.x4wtre2m4d.css

# Output Cache: ejecutadlo tres veces; a partir de la tercera aparece la cabecera Age (acierto).
# La primera crea la cookie antiforgery y la segunda guarda la página en caché para ese usuario.
curl -s -c c.txt -b c.txt -o /dev/null -D - http://localhost:5196/Paginas/Incidencias | grep -i "^age"
```

El hash (`x4wtre2m4d`) cambia cada vez que cambia `site.css`: copiadlo de la salida del segundo comando.

En el navegador (herramientas de desarrollo, F12):

- Pestaña **Red** → al recargar, el CSS aparece como *(memory cache)* o *(disk cache)* sin petición al servidor.
- Pestaña **Consola** → añadid a cualquier vista `<p style="color:red">prueba</p>`: el texto no sale en rojo y la consola muestra la violación de `style-src-attr`.

## 8.10 Aplicación en SIREI

Las mismas reglas están aplicadas en [src/SIREI.Web](../src/SIREI.Web/), salvo minificar JavaScript, que queda fuera. Este apartado recoge solo lo que cambia respecto a lo anterior y por qué.

### CSP: de estilos inline a un CSS por página

Al activar la política de 8.7, el layout y las páginas tenían 183 atributos `style=""` que el navegador ignoraba, y cargaban Google Fonts, que `style-src 'self'` bloquea. Se pasaron a clases **sin hacer una hoja monolítica**:

| Fichero | Contenido |
|---|---|
| `fuentes.css` | Solo los `@font-face` |
| `sirei.css` | Design system de los wireframes (sin tocar) |
| `layout.css` | Estructura de `_Layout` y esqueleto de página: `.contenido`, `.migas`, `.columnas` |
| `componentes.css` | Solo lo que usan dos o más páginas: aviso, tarjeta de mensaje, botonera |
| `app.css` | Añadidos de comportamiento (`[hidden]`, `aria-busy`) |
| `listado.css`, `formulario.css`, `consulta.css` | Cada página y sus parciales, enlazados desde su `@section Estilos` |

Los nombres dicen qué es cada cosa (`.celda-sin-asignar`, `.barra-acciones`), no cómo se ve; no hay clases de utilidad. La parte de htmx (filtro de `hx-trigger` que necesitaba `eval`) está en [09-htmx-csp-estricta.md](09-htmx-csp-estricta.md).

### Fuentes autoalojadas, con precarga y huella

Atkinson Hyperlegible está en `wwwroot/fonts` (licencia OFL). Son fuentes variables, así que un `.woff2` por subconjunto cubre todos los pesos. Los `@font-face` usan `font-display: swap` (no bloquea el pintado) y `unicode-range` (latin-ext solo se descarga si hace falta). El layout precarga la que usan todas las páginas:

```cshtml
<link rel="preload" href="~/fonts/atkinson-hyperlegible-next-latin.woff2" as="font" type="font/woff2" crossorigin asp-append-version="true">
```

El problema es que **el CSS no puede usar `asp-append-version`**: un `url("../fonts/x.woff2")` sin huella no es `immutable`, y si la precarga y el CSS piden URLs distintas, la fuente se descarga dos veces. La solución está en la compilación, en el [.csproj](../src/SIREI.Web/SIREI.Web.csproj):

1. Un `StaticWebAssetFingerprintPattern` para `*.woff2`. Por defecto `MapStaticAssets` no pone huella a las fuentes.
2. Al minificar, la tarea `MinificarCss` reescribe cada `url()` relativa con la huella del fichero: `url("../fonts/x.woff2")` → `url("../fonts/x.wm3e8jpykz.woff2")`.
3. La huella de la fuente solo depende de la fuente, así que el `.min.css` sale con su contenido definitivo **antes** de que se calcule su propia huella. Se evita así la trampa de 8.2.
4. Las fuentes están en los `Inputs` del target: si cambia una fuente, se vuelve a minificar el CSS.

La huella se calcula con el mismo algoritmo que `MapStaticAssets`: SHA-256 del contenido, los 9 primeros bytes como entero little-endian **con signo**, su valor absoluto y 10 dígitos en base 36. El signo importa: con un entero sin signo, la huella sale bien en unos ficheros y mal en otros, según el bit alto del noveno byte. Por eso el target `VerificarHuellasCss` compara, **en cada compilación**, las huellas escritas en los `.min.css` con las que calcula el SDK. Si un día cambia el algoritmo, la compilación falla con un error que lo explica, en vez de servir fuentes con un 404.

### TempData con `[TempData]`

En 8.5 el `Set-Cookie` de TempData venía de un parcial. En SIREI venía de la propiedad `[TempData] AvisoPendiente` de los PageModel: el atributo lee TempData en **cada** petición y añade un `Set-Cookie` de borrado a todas las respuestas, así que ninguna se guardaba en caché. Se sustituyó por un acceso que solo lee TempData si la petición trae su cookie ([TempDataExtensions.cs](../src/SIREI.Web/Infraestructura/TempDataExtensions.cs)):

```csharp
public string? AvisoPendiente
{
    get => this.LeerTempDataSiHayCookie(nameof(AvisoPendiente));
    set => TempData[nameof(AvisoPendiente)] = value;
}
```

### El minificador de HTML y el espacio en blanco

WebMarkupMin trata `<form>` como elemento de bloque y elimina el espacio en blanco que lo rodea. En el listado, el formulario "Asignarme" es `display: inline` y ese espacio separaba los botones "Asignarme" y "Ver", que quedaban pegados. La separación pasa a estar en el CSS (`margin-right`). Regla general: **no depender del espacio en blanco del HTML para el maquetado**.

Los textos con `white-space: pre-line` (descripción de la incidencia, mensajes) conservan sus saltos de línea: WebMarkupMin no los colapsa dentro del texto.

### Pipeline

```csharp
app.UseContentSecurityPolicy();       // 8.7
app.UseStatusCodePagesWithReExecute("/Error", "?codigo={0}");
app.UseRouting();
app.UseAuthorization();               // antes que la caché: un acierto no debe saltarse la autorización
app.UseOutputCache();                 // 8.5
app.Use(/* invalidar tras POST */);   // 8.5
app.UseWebMarkupMin();                // 8.4
app.Use(/* quitar ETag/Last-Modified si immutable */);   // 8.3
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets().CacheOutput(CacheHtmlPolicy.Nombre);
```

La política está en [Infraestructura/CacheHtmlPolicy.cs](../src/SIREI.Web/Infraestructura/CacheHtmlPolicy.cs). El token antiforgery está en el `<meta name="csrf-token">` del layout, en todas las páginas, así que la clave de caché varía por la cookie antiforgery también en las páginas sin formulario.

### Lo que se comprobó

| Prueba | Resultado |
|---|---|
| Estilo calculado, posición, atributos y texto de cada elemento frente a la versión original, en 14 escenarios (oculto forzado a visible, diálogos abiertos, errores de validación) a 1366 y 390 px | Idéntico. Única diferencia: 0,2 px en la tabla del listado (4 px de margen frente a un espacio de 4,2 px) |
| Consola en las 7 páginas, con un ciclo de refresco automático | Sin errores, sin violaciones CSP y sin avisos de precarga |
| HTML de `/` | 16 593 → 12 835 bytes (−23 %) |
| CSS y fuente con huella | `max-age=31536000, immutable`, sin `ETag` |
| Precarga y `fuentes.min.css` | La misma URL con huella |
| Output Cache, mismo usuario | `Age` a partir de la tercera petición, también en los fragmentos htmx |
| Usuarios A y B | Tokens distintos; B con el token de A → `400` |
| POST sin JavaScript (Post-Redirect-Get) | `302`, el aviso se muestra una vez y la siguiente petición vuelve a salir de caché |
| Otro usuario tras el cambio | Ve el dato nuevo |

### Pendiente

- La guía de estilos (`Estilos.cshtml`) queda fuera: sigue con estilos inline y Google Fonts, y con la CSP se ve rota.
- Los textos relativos ("hace 5 min") pueden ir hasta 60 s por detrás por la caché.

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Optimización de la entrega de recursos estáticos (MapStaticAssets)](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/map-static-files?view=aspnetcore-10.0) — Huella, compresión en compilación y cabeceras de caché.
- [Middleware de almacenamiento en caché de salida](https://learn.microsoft.com/es-es/aspnet/core/performance/caching/output?view=aspnetcore-10.0) — Políticas, etiquetas, invalidación y almacenamiento en Redis.
- [Información general sobre el almacenamiento en caché](https://learn.microsoft.com/es-es/aspnet/core/performance/caching/overview?view=aspnetcore-10.0) — Comparativa de los tipos de caché de ASP.NET Core.
- [Compresión de respuesta en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/performance/response-compression?view=aspnetcore-10.0) — Recomendación de comprimir en el servidor web y riesgos CRIME/BREACH con HTTPS.
- [Prevención de ataques CSRF](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) — Tokens antiforgery, por qué no se pueden compartir entre usuarios.
- [Cache-Control (MDN)](https://developer.mozilla.org/es/docs/Web/HTTP/Reference/Headers/Cache-Control) — `max-age`, `no-cache` e `immutable`.
- [Content-Encoding (MDN)](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Encoding) — gzip, br y zstd.
- [Content Security Policy (MDN)](https://developer.mozilla.org/es/docs/Web/HTTP/Guides/CSP) — Guía de CSP: qué protege y cómo se escribe una política.
- [Content-Security-Policy (MDN, referencia)](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy) — Todas las directivas; ver también [`script-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/script-src-attr) y [`style-src-attr`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/style-src-attr).
- [CSP Evaluator](https://csp-evaluator.withgoogle.com/) — Herramienta de Google para revisar una política antes de llevarla a producción.
- [WebMarkupMin](https://github.com/Taritsyn/WebMarkupMin) y [WebMarkupMin.AspNetCoreLatest](https://www.nuget.org/packages/WebMarkupMin.AspNetCoreLatest) — Minificador de HTML usado en el proyecto.
- [NUglify](https://github.com/trullock/NUglify) — Minificador de CSS usado por la tarea de compilación del proyecto.
- [Tareas insertadas de MSBuild con RoslynCodeTaskFactory](https://learn.microsoft.com/es-es/visualstudio/msbuild/msbuild-roslyncodetaskfactory) — Cómo funciona la tarea `MinificarCss` del `.csproj`.
