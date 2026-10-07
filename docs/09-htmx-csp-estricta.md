# 09 · htmx con CSP estricta

SIREI aplica la **Content Security Policy** estricta descrita en el apartado 8.7 de [08-optimizacion-entrega.md](08-optimizacion-entrega.md):

```
script-src 'self'; script-src-attr 'none'; style-src 'self'; style-src-attr 'none'; ...
```

Con esta política solo se ejecutan scripts y se aplican hojas de estilo que vengan de **ficheros del propio sitio**. No se permite `'unsafe-eval'`, `'unsafe-inline'` ni nonces.

htmx 2 funciona con esta política, pero algunas de sus funciones evalúan JavaScript escrito en atributos o insertan código inline. Este documento recoge **qué no se debe usar** y qué usar en su lugar.

## Configuración obligatoria

En [_Layout.cshtml](../src/SIREI.Web/Pages/Shared/_Layout.cshtml):

```html
<meta name="htmx-config" content='{"includeIndicatorStyles":false,"allowEval":false,"allowScriptTags":false,"defaultFocusScroll":false}'>
```

| Opción | Qué evita |
|---|---|
| `includeIndicatorStyles: false` | Que htmx inyecte un `<style>` en el `<head>` para `.htmx-indicator`. Los estilos del indicador van en `app.css`. |
| `allowEval: false` | Que htmx llame a `Function()`. En lugar de evaluar, emite el evento `htmx:evalDisallowedError` y la función afectada se ignora (ver cada caso abajo). |
| `allowScriptTags: false` | Que htmx ejecute los `<script>` de las respuestas parciales. Los elimina antes de insertar el HTML. |

Con `allowEval: false`, htmx avisa de forma explícita en lugar de dejar que la CSP bloquee en silencio. Para verlo durante el desarrollo, en `sirei.js`:

```js
document.addEventListener('htmx:evalDisallowedError', function (e) {
  console.error('htmx: JavaScript en atributo no permitido por la CSP', e.target);
});
```

## Lo que no se debe usar

### 1. `hx-on` en cualquiera de sus formas

```html
<!-- NO -->
<button hx-on:click="alert('hola')">...</button>
<button hx-on::after-request="this.reset()">...</button>
<form hx-on="htmx:afterRequest: this.reset()">...</form>
```

htmx convierte el valor en una función con `new Function()`. Es el equivalente htmx de `onclick="..."`.

**En su lugar:** un listener en un fichero `.js`, identificando el elemento con un atributo `data-*`:

```html
<form data-limpiar-al-enviar hx-post="...">...</form>
```

```js
document.addEventListener('htmx:afterRequest', function (e) {
  if (e.detail.successful && e.target.matches('[data-limpiar-al-enviar]')) e.target.reset();
});
```

### 2. Filtros de evento en `hx-trigger`

```html
<!-- NO -->
<input hx-get="..." hx-trigger="keyup[key=='Enter']">
<div hx-get="..." hx-trigger="every 30s [document.visibilityState === 'visible']">
```

La expresión entre corchetes se evalúa con `Function()`. Afecta a cualquier evento: `click[ctrlKey]`, `load[...]`, `every Ns [...]`, etc.

**Ojo: el fallo es silencioso y al revés de lo que se esperaría.** Cuando la CSP impide evaluar el filtro, htmx **no bloquea el evento: lo deja pasar siempre**, como si el filtro no existiera. Pasa tanto con `allowEval: true` (htmx captura el `EvalError` y descarta el filtro) como con `allowEval: false` (el filtro vale `true`). En el refresco automático del listado, eso significa que se refresca cada 30 s **aunque el usuario tenga el foco dentro de los resultados**, que es justo lo que el filtro quería evitar.

Los **modificadores sin corchetes** sí se pueden usar: `delay:500ms`, `throttle:1s`, `changed`, `once`, `from:#id`, `target:...`, `consume`, `queue:...`.

**En su lugar:** dejar el trigger sin filtro y cancelar la petición en `htmx:confirm` cuando no se cumpla la condición:

```html
<div hidden data-refresco-auto hx-get="..." hx-trigger="every 30s"></div>
```

```js
document.addEventListener('htmx:confirm', function (e) {
  if (!e.target.matches('[data-refresco-auto]')) return;
  if (document.getElementById('resultados').contains(document.activeElement)) e.preventDefault();
});
```

Para una tecla concreta, otra opción es lanzar un evento propio desde JavaScript y escucharlo con `hx-trigger="buscar"`.

### 3. Prefijos `js:` y `javascript:` en atributos con JSON

```html
<!-- NO -->
<button hx-get="..." hx-vals='js:{"ancho": window.innerWidth}'>
<div hx-headers='js:{"X-Hora": new Date().toISOString()}'>
<div hx-request='js:{"timeout": calcularTimeout()}'>
```

Afecta a `hx-vals`, `hx-headers` y `hx-request`. Con el prefijo, el valor se evalúa como JavaScript.

**Sí se puede usar** JSON puro, sin prefijo:

```html
<button hx-get="..." hx-vals='{"pagina": 2}'>
```

**En su lugar**, para valores calculados en el navegador, usar `htmx:configRequest`:

```js
document.addEventListener('htmx:configRequest', function (e) {
  if (e.target.matches('[data-enviar-ancho]')) e.detail.parameters.ancho = window.innerWidth;
});
```

### 4. `hx-vars`

```html
<!-- NO -->
<div hx-vars="pagina: paginaActual + 1">
```

Está obsoleto en htmx 2 y **siempre** se evalúa como JavaScript. Se sustituye igual que el punto anterior.

### 5. `<script>` en las respuestas parciales

```cshtml
@* NO: dentro de un parcial que devuelve htmx *@
<tr id="fila-3">...</tr>
<script>document.getElementById("aviso").focus();</script>
```

Con `allowScriptTags: false` htmx lo elimina, y aunque no lo hiciera, la CSP lo bloquearía. Tampoco sirve `<script src="...">` dentro del parcial: también se elimina.

**En su lugar:**
- El código va en `sirei.js` o en el `.js` de la página, escuchando `htmx:afterSwap` o `htmx:afterSettle`.
- Si el servidor tiene que pedir una acción, que lo diga con la cabecera de respuesta `HX-Trigger` y el JavaScript la escuche:

```csharp
Response.Headers["HX-Trigger"] = "incidenciaGuardada";
```

```js
document.body.addEventListener('incidenciaGuardada', function () {
  sirei.enfocar('#aviso');
});
```

En SIREI ya se usa un patrón así: el atributo `data-foco` de los botones de paginación le indica a `sirei.js` dónde poner el foco después de `htmx:afterSettle`.

### 6. `<style>` y `style=""` en las respuestas parciales

```cshtml
@* NO *@
<style>.fila-nueva { background: #fff3cd; }</style>
<tr style="background: #fff3cd">...</tr>
```

La CSP bloquea los dos: `style-src 'self'` el bloque y `style-src-attr 'none'` el atributo. No es algo específico de htmx, pero en los parciales es fácil olvidarlo porque no pasan por el layout.

**En su lugar:** clases definidas en `app.css` o en el `.css` de la página.

Por el mismo motivo, el parámetro `style` del tag helper `<icono>` no se debe usar. Se usa `class`.

### 7. Nonces (`inlineScriptNonce`, `inlineStyleNonce`)

htmx admite nonces para permitir scripts y estilos inline concretos. **No se usan en SIREI**:

- Un nonce tiene que ser distinto en cada respuesta, y el **Output Cache** (apartado 8.5) sirve el mismo HTML a varias peticiones con el mismo nonce. Eso anula su protección.
- Reabrirían la puerta al código inline que la política quiere evitar.

### 8. Extensiones de htmx sin revisar

Antes de añadir una extensión (`hx-ext`), hay que comprobar que no use `eval`/`Function`, no inyecte `<style>` y no inserte atributos `style`. Si lo hace, no se usa o se busca una alternativa.

## Lo que sí funciona sin restricciones

- `hx-get`, `hx-post`, `hx-put`, `hx-patch`, `hx-delete`
- `hx-target`, `hx-swap`, `hx-swap-oob`, `hx-select`, `hx-select-oob`
- `hx-trigger` con eventos y modificadores, **sin** filtros `[...]`
- `hx-include`, `hx-params`, `hx-vals` y `hx-headers` con JSON puro
- `hx-indicator`, `hx-disabled-elt`, `hx-sync`, `hx-confirm`, `hx-push-url`, `hx-boost`
- Las cabeceras de respuesta `HX-Trigger`, `HX-Redirect`, `HX-Retarget`, `HX-Reswap`, etc.
- La API de JavaScript (`htmx.ajax`, `htmx.on`, `htmx.process`...) llamada desde ficheros `.js`
- Los eventos `htmx:*` escuchados con `addEventListener`

## Resumen

| No usar | Usar en su lugar |
|---|---|
| `hx-on:*`, `hx-on` | `addEventListener` en un `.js` + atributo `data-*` |
| `hx-trigger="evento[condición]"` | `hx-trigger="evento"` + `htmx:confirm` con `preventDefault()` |
| `hx-vals` / `hx-headers` / `hx-request` con `js:` o `javascript:` | JSON puro, o `htmx:configRequest` |
| `hx-vars` | `htmx:configRequest` |
| `<script>` en un parcial | `htmx:afterSwap` o cabecera `HX-Trigger` |
| `<style>` o `style=""` en un parcial | Clases en `app.css` |
| `<icono style="...">` | `<icono class="...">` |
| Nonces | No hacen falta: todo va en ficheros |

## Referencias

- [htmx: configuración](https://htmx.org/reference/#config) — `allowEval`, `allowScriptTags`, `includeIndicatorStyles` y el resto de opciones.
- [htmx: seguridad y CSP](https://htmx.org/docs/#security) — Qué funciones dependen de `eval` y cómo desactivarlas.
- [hx-on](https://htmx.org/attributes/hx-on/), [hx-trigger](https://htmx.org/attributes/hx-trigger/), [hx-vals](https://htmx.org/attributes/hx-vals/), [hx-headers](https://htmx.org/attributes/hx-headers/) — Atributos afectados.
- [Eventos de htmx](https://htmx.org/events/) — `htmx:confirm`, `htmx:configRequest`, `htmx:afterSwap`, `htmx:evalDisallowedError`.
- [Cabeceras de respuesta](https://htmx.org/reference/#response_headers) — `HX-Trigger` y las demás.
- [Content Security Policy (MDN)](https://developer.mozilla.org/es/docs/Web/HTTP/Guides/CSP) — Guía general de CSP.
