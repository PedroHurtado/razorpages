# SIREI · Documentación de la modernización

SIREI (Sistema de REsolución de Incidencias informáticas de la SGFAL) está hecho hoy en **ASP.NET Web Forms**, la tecnología web de Microsoft basada en páginas `.aspx` con *code-behind* y ViewState. El cliente quiere pasarlo a tecnologías actuales sobre **.NET** (la plataforma moderna y multiplataforma de Microsoft, antes llamada .NET Core).

Esta carpeta recoge lo analizado y lo decidido hasta ahora.

## Qué se ha hecho

1. **Revisión de las pantallas actuales** a partir de tres capturas (listado, alta y consulta de incidencias): errores de datos, usabilidad, coherencia y accesibilidad.
2. **Propuesta de arquitectura** para la migración: Razor Pages + htmx, con la opción de separar una API mediante el patrón BFF.
3. **Wireframes interactivos** de las tres pantallas, rediseñadas para cumplir WCAG 2.1 AA, más una **guía de estilos** de la que sacar los colores, tipografías y componentes.

## Documentos

| Documento | Contenido |
|---|---|
| [01-revision-pantallas-actuales.md](01-revision-pantallas-actuales.md) | Problemas detectados en las capturas actuales |
| [02-arquitectura-propuesta.md](02-arquitectura-propuesta.md) | Tecnologías, alternativas comparadas y plan de migración |
| [03-htmx.md](03-htmx.md) | Qué es htmx y cómo se usa con Razor Pages |
| [04-wireframes-y-guia-de-estilos.md](04-wireframes-y-guia-de-estilos.md) | Qué contienen los wireframes y la guía de estilos |
| [05-paginas-razor.md](05-paginas-razor.md) | Detalle de cada página `.cshtml` del prototipo y sus dependencias |

## Cómo ver los wireframes

Los wireframes están publicados como un *artifact* de Claude, una página web alojada en claude.ai:

**https://claude.ai/artifact/CdtFYo1ytRwCyHa7vQ3K82**

1. Abre el enlace con la cuenta de claude.ai que lo creó. Por defecto es **privado**.
2. Verás un lienzo con cuatro tableros: Listado, Nueva incidencia, Consulta y Guía de estilos.
3. Pulsa **Play** en un tablero para usarlo: los filtros, botones y formularios funcionan, y las pantallas están enlazadas entre sí.
4. Para que otras personas lo vean, compártelo desde el menú **Share** del propio lienzo.

Los datos que aparecen son los de las capturas o de ejemplo. No se conectan a ningún sistema real.

## Capturas de partida

Están en la raíz del proyecto:

- `Captura de pantalla_GListadoIncidencias.aspx_29-9-2026_sirei.jpeg`
- `Captura de pantalla_GNuevaIncidencia.aspx_29-9-2026_sirei.jpeg`
- `Captura de pantalla_GConsultaIncidencia.aspx_29-9-2026_sirei.jpeg`
