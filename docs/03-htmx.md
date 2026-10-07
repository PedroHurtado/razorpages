# 03 · htmx

**htmx** es una librería JavaScript pequeña (~14 KB) que permite actualizar partes de una página **sin escribir JavaScript**. Basta con añadir atributos HTML. El servidor devuelve **fragmentos de HTML** (no JSON) y htmx los coloca donde se indica.

Para quien viene de Web Forms se parece al `UpdatePanel`, pero sin ViewState y con control total de qué se envía y qué se sustituye.

## Atributos principales

| Atributo | Para qué sirve |
|---|---|
| `hx-get` / `hx-post` | Qué URL llamar |
| `hx-trigger` | Cuándo: `change`, `keyup delay:500ms`, `every 30s` |
| `hx-target` | Qué elemento sustituir |
| `hx-swap` | Cómo insertarlo (`innerHTML`, `outerHTML`…) |
| `hx-include` | Qué otros campos enviar |

## Ejemplo: filtros del listado

```html
<form id="filtros" hx-get="/Incidencias?handler=Tabla" hx-target="#tabla"
      hx-trigger="change, keyup delay:500ms from:#texto">
    <select name="estado" asp-items="Model.Estados"></select>
    <input id="texto" name="texto" />
</form>
<div id="tabla"><partial name="_TablaIncidencias" model="Model.Resultado" /></div>
```

```csharp
public async Task<PartialViewResult> OnGetTablaAsync([FromQuery] FiltroIncidencias filtro)
    => Partial("_TablaIncidencias", await service.BuscarAsync(filtro));
```

Al cambiar un filtro solo se recarga la tabla. Todo es C# y Razor.

## Usos en SIREI

- Filtros y paginación sin recargar la página.
- Refresco automático del listado cada 30 s (`hx-trigger="every 30s"`).
- Buscador de "Cambiar interesado".
- Botón "Asignarme", que sustituye solo la fila afectada.

## A tener en cuenta

- **No sirve para interfaces muy interactivas** (arrastrar y soltar, editores visuales).
- **Token antiforgery:** las peticiones `hx-post` deben enviarlo. Es la protección de ASP.NET Core contra CSRF (peticiones falsificadas desde otra web) y se configura una sola vez en el layout.
- **Servir la librería desde el propio servidor**, sin CDN (red de distribución de contenidos externa), si la política del cliente lo exige.
