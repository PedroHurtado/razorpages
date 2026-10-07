# SIREI · Prototipo Razor Pages + htmx

Prototipo navegable de las pantallas rediseñadas (listado, nueva incidencia, consulta y guía de estilos) sobre .NET 10.
Sin autenticación ni base de datos: los servicios son *fakes* en memoria.

## Arrancar

```
cd src/SIREI.Web
dotnet run --launch-profile http
```

Abre http://localhost:5049. La guía de estilos está en `/Estilos`.

Los datos se cargan al arrancar y se pierden al parar la aplicación: cada arranque empieza con los datos de ejemplo.

## Proyectos

| Proyecto | Contenido |
|---|---|
| `SIREI.Dominio` | Modelos, enumerados e interfaces de servicio (`IIncidenciaServicio`, `IPersonaServicio`…) |
| `SIREI.Servicios.Fake` | Implementación en memoria. Se registra con `AddServiciosFake()` |
| `SIREI.Web` | Razor Pages, htmx, CSS y JS |

Para conectar un backend real basta con otra implementación de las interfaces de `SIREI.Dominio` y cambiar el registro en `Program.cs`.

## Notas

- El CSS de `wwwroot/css/sirei.css` es el bloque común de los wireframes, sin cambios. Los añadidos están en `app.css`.
- htmx 2.0.11 se sirve desde `wwwroot/lib/htmx`. Las fuentes se cargan desde Google Fonts.
- Todo funciona también sin JavaScript (formularios y redirecciones), salvo las tarjetas de resumen y el buscador del interesado.
