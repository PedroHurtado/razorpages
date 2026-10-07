# 02 · Arquitectura propuesta

Punto de partida: el backend será **.NET** (versión 10, LTS: *Long Term Support*, con soporte de 3 años).

## Frontend: qué se valoró

| Opción | Qué es | Valoración para SIREI |
|---|---|---|
| **Razor Pages** | Páginas `.cshtml` con su clase C# (`PageModel`), renderizadas en el servidor | **Elegida.** Es lo más parecido a Web Forms (página + code-behind), sin ViewState. Infraestructura simple y HTML accesible |
| **Blazor** | Componentes C# con interactividad; en modo *Server* mantiene una conexión permanente por usuario | Válida, pero esa conexión complica la infraestructura (memoria, reconexiones, balanceadores). Solo compensa con mucha interactividad |
| **Angular** | Framework JavaScript/TypeScript de tipo SPA (*Single Page Application*: la página se carga una vez y el navegador hace el resto) | Solo si el cliente ya lo tiene como estándar o hay un equipo de frontend dedicado |

**Decisión:** Razor Pages + **htmx** (ver [03-htmx.md](03-htmx.md)) + **SignalR** solo para los mensajes en tiempo real. SignalR es la librería de .NET para enviar datos del servidor al navegador al instante.

Equivalencias para un equipo que viene de Web Forms:

| Web Forms | Razor Pages |
|---|---|
| `Pagina.aspx` / `.aspx.cs` | `Pagina.cshtml` / `.cshtml.cs` |
| `Page_Load` | `OnGet()` |
| `btnGuardar_Click` | `OnPostGuardar()` |
| Validators | DataAnnotations (atributos de validación en el modelo) |

## Backend: BFF + Minimal API

Se planteó usar el patrón **BFF** (*Backend For Frontend*). Consiste en tener un servidor intermedio dedicado a la web: gestiona la sesión del usuario y llama a la API de negocio, de modo que el navegador nunca habla directamente con la API ni guarda tokens.

```
Navegador ──cookie──▶ SIREI.Web (Razor Pages = BFF) ──token──▶ SIREI.Api (Minimal APIs) ──▶ BD
```

- **Minimal APIs:** forma ligera de definir endpoints HTTP en ASP.NET Core sin controladores.
- **Token:** credencial firmada (normalmente JWT) que identifica al usuario ante la API.

Con Razor Pages el BFF sale casi gratis, porque la web ya se ejecuta en el servidor.

**Ventajas:**
- La API se puede reutilizar (otros sistemas, una app móvil).
- La API no queda expuesta al navegador.
- La lógica de negocio queda en un único sitio.

**Costes:**
- Dos aplicaciones que desplegar.
- Un salto HTTP más en cada operación.
- Hay que traducir los errores de la API a los formularios.
- Los adjuntos tienen que atravesar el BFF.

**Punto crítico, la identidad del usuario:**
- Con **OIDC** (*OpenID Connect*: inicio de sesión estándar con Keycloak, Entra ID…) es sencillo: el BFF reenvía el token del usuario a la API.
- Con **autenticación Windows/AD** hace falta *delegación Kerberos* (difícil de configurar), o bien que el BFF se identifique ante la API y le pase el usuario en una cabecera (*subsistema de confianza*).

**Recomendación:**
- Si la API va a tener **otros consumidores**: BFF + Minimal API.
- Si no: **monolito modular**, una sola aplicación con capas bien separadas. Separarla en BFF + API más adelante es casi mecánico.

## Piezas comunes

| Necesidad | Propuesta |
|---|---|
| Acceso a datos | **EF Core** (el ORM de .NET), generando el modelo desde la BD actual |
| Validación | FluentValidation |
| HTML enriquecido | Limpiar en servidor con HtmlSanitizer, para evitar **XSS** (inyección de scripts) |
| Tareas en segundo plano | Hangfire o `BackgroundService` (avisos por correo) |
| Logs y trazas | Serilog + OpenTelemetry |
| Pruebas | xUnit, Playwright (pruebas de extremo a extremo en el navegador), axe (accesibilidad) |
| Entorno local | .NET Aspire: levanta web, API y BD juntas |

## Plan de migración

1. **Inventario:** código, procedimientos almacenados, integraciones (correo, AD, telefonía).
2. **Backend** sobre la BD actual, sin cambiar su esquema.
3. **Pantallas en este orden:** listado, consulta, alta, formularios.
4. **Convivencia en PRE:** las dos versiones atacan la misma BD.
5. **Paso a producción** y retirada de Web Forms.

Si la migración se complica, se puede poner **YARP** delante: un *proxy* inverso de Microsoft que sirve páginas antiguas y nuevas bajo la misma URL.

## Pendiente de confirmar con el cliente

- Motor de BD (SQL Server u Oracle) y cuánta lógica hay en procedimientos almacenados.
- Sistema de autenticación (AD, Cl@ve, OIDC).
- Infraestructura (IIS o contenedores).
- Si la API tendrá otros consumidores.
