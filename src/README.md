# SIREI · Prototipo Razor Pages + htmx

Prototipo navegable de las pantallas rediseñadas (listado, nueva incidencia, consulta y guía de estilos) sobre .NET 10.
Sin autenticación ni base de datos: los servicios son *fakes* en memoria.

## Arrancar

La aplicación se usa a través de un gateway (YARP). Hay que arrancar los dos, cada uno en su terminal:

```
cd src/SIREI.Web
dotnet run --launch-profile http
```

```
cd src/SIREI.Gateway
dotnet run --launch-profile http
```

Abre http://localhost:5000 (el gateway). La guía de estilos está en `/Estilos`.

SIREI.Web sigue escuchando en http://localhost:5049, pero ya no abre el navegador: la entrada es el gateway.

Los datos se cargan al arrancar y se pierden al parar la aplicación: cada arranque empieza con los datos de ejemplo.

## Proyectos

| Proyecto | Contenido |
|---|---|
| `SIREI.Dominio` | Modelos, enumerados e interfaces de servicio (`IIncidenciaServicio`, `IPersonaServicio`…) |
| `SIREI.Servicios.Fake` | Implementación en memoria. Se registra con `AddServiciosFake()` |
| `SIREI.Web` | Razor Pages, htmx, CSS y JS |
| `SIREI.Gateway` | Gateway con YARP: punto de entrada único que reenvía todo a `SIREI.Web` |

## Gateway

La configuración está en `SIREI.Gateway/appsettings.json`, sección `ReverseProxy`:

- **Ruta** `sirei-web`: cualquier petición (`{**resto}`) va al clúster `sirei-web`.
- **Clúster** `sirei-web`: un destino, `http://localhost:5049/`. Para repartir carga basta con añadir más destinos.
- **Salud**: cada 10 s el gateway pide `/health` a SIREI.Web. Tras dos fallos seguidos deja de enviarle tráfico
  y responde **503**; en cuanto vuelve a responder, se reanuda.

SIREI.Web, por su parte:

- Usa `UseForwardedHeaders()`, para ver la IP, el esquema y el host originales (`X-Forwarded-*`) que envía el gateway.
  Solo se aceptan de un proxy en localhost.
- Expone `/health` (`MapHealthChecks`).

La compresión, la caché, la CSP y el antiforgery no cambian: el gateway reenvía cabeceras y cookies tal cual.

Para conectar un backend real basta con otra implementación de las interfaces de `SIREI.Dominio` y cambiar el registro en `Program.cs`.

## Notas

- El CSS de `wwwroot/css/sirei.css` es el bloque común de los wireframes, sin cambios. Los añadidos están en `app.css`.
- htmx 2.0.11 se sirve desde `wwwroot/lib/htmx`. Las fuentes se cargan desde Google Fonts.
- Todo funciona también sin JavaScript (formularios y redirecciones), salvo las tarjetas de resumen y el buscador del interesado.
