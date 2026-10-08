# 03 · htmx

**htmx** es una librería JavaScript pequeña (~16 KB comprimida) que permite actualizar partes de una página **sin escribir JavaScript**. Basta con añadir atributos HTML. El servidor devuelve **fragmentos de HTML** (no JSON) y htmx los coloca donde se indica.

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

## Cargar htmx solo en las páginas que lo usan

Lighthouse avisa de "JavaScript no utilizado" y señala a htmx. Es lo esperado: mide qué código se ejecuta **durante la carga**, y casi todo htmx (peticiones, swaps, historial) solo se ejecuta cuando el usuario hace algo.

htmx no se puede dividir en módulos. Se publica como un único fichero, y un bundler no puede eliminar lo que no se usa porque htmx lee los atributos `hx-*` del HTML mientras la página funciona. Lo único que podemos decidir es **en qué páginas se descarga**.

Hoy htmx está en el layout porque lo usan las tres páginas principales (listado, consulta y nueva incidencia). Si aparecen páginas sin `hx-*` (ayuda, aviso legal, declaración de accesibilidad), se puede quitar del layout y cargar desde la sección `Scripts` de cada página que lo necesite.

**Layout:** se quita htmx y se mantiene el resto.

```html
<head>
...
<script src="~/js/sirei.js" asp-append-version="true" defer></script>
@await RenderSectionAsync("Scripts", required: false)
</head>
```

**Página que usa htmx** (por ejemplo, `Incidencias/Consulta.cshtml`):

```html
@section Scripts {
<script src="~/lib/htmx/htmx.min.js" asp-append-version="true" defer></script>
<script src="~/js/consulta.js" asp-append-version="true" defer></script>
}
```

**Página que no lo usa:** no declara nada y no descarga htmx.

Reglas a respetar:

- **Todos con `defer`.** Los scripts diferidos se ejecutan en el orden en que aparecen, así que htmx tiene que ir antes que el JavaScript de la página que lo llama (`htmx.trigger`, `htmx.ajax`).
- **`sirei.js` no puede dar por hecho que htmx existe.** Escuchar sus eventos (`htmx:configRequest`, `htmx:afterSettle`…) no falla sin htmx, pero llamar a `htmx.*` sí. Hoy solo lo hace el clic en los filtros rápidos (`data-preset`), que solo existen en el listado. Si se añaden más llamadas, protegerlas con `if (window.htmx)`.
- **El `<meta name="htmx-config">` puede seguir en el layout.** Si htmx no se carga, se ignora.

El ahorro es de unos 16 KB comprimidos, y solo en la primera visita, porque después el navegador lo tiene en caché. Merece la pena cuando hay muchas páginas sin htmx. Si todas lo usan, es mejor dejarlo en el layout.

## Alternativa: fixi.js

[fixi.js](https://github.com/bigskysoftware/fixi) es una versión mínima de la misma idea, del autor de htmx. Ocupa unos pocos KB y deja fuera a propósito todo lo que no es imprescindible: el elemento hace una petición y sustituye un destino con el HTML que devuelve el servidor.

```html
<form id="filtros" fx-action="/Incidencias?handler=Tabla" fx-method="get"
      fx-target="#tabla" fx-trigger="change">
    <select name="estado" asp-items="Model.Estados"></select>
    <input id="texto" name="texto" />
</form>
<div id="tabla"><partial name="_TablaIncidencias" model="Model.Resultado" /></div>
```

El servidor no cambia: el handler sigue devolviendo un parcial de Razor.

Lo que habría que reescribir en SIREI si se cambiara:

| Usamos de htmx | En fixi |
|---|---|
| Swaps fuera de banda (`hx-swap-oob`): aviso, resumen, KPI, historial | No existen. Habría que hacerlos en JavaScript con sus eventos o repartir la respuesta en varias peticiones |
| `hx-trigger="every 30s"` (refresco automático) | No existe. Un `setInterval` propio |
| `keyup delay:500ms` (buscadores) | No hay retardo. Código propio |
| Token antiforgery en `htmx:configRequest` | Igual, pero con el evento de configuración de fixi |
| Eventos `htmx:*` en `sirei.js`, `consulta.js` y `nueva.js` | Otros nombres de evento: hay que adaptarlos todos |

**Conclusión:** fixi ahorra algo más de 10 KB comprimidos, pero SIREI depende mucho de los swaps fuera de banda, y rehacerlos a mano costaría más que lo que se ahorra. Tiene sentido en un proyecto nuevo con interacciones simples (un destino por petición), no para migrar este.
