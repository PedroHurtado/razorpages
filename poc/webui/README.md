# Prueba de concepto: Microsoft WebUI + ASP.NET Core

Prueba aislada de [Microsoft WebUI](https://microsoft.github.io/webui/) renderizando desde .NET.
No depende de SIREI ni la modifica.

## Estructura

```
poc/webui/
├── client/                    Plantillas y build (npm)
│   ├── src/index.html         Plantilla de entrada
│   ├── src/info-card/         Componente sin script: solo SSR, 0 JS
│   ├── src/task-board/        Componente interactivo (isla hidratada)
│   ├── src/index.ts           Punto de entrada de hidratación
│   ├── src/streaming.ts       Coordinador de streaming (se carga en <head>)
│   ├── data/state.json        Estado para `npm run dev` (sin .NET)
│   └── build-client.mjs       esbuild + proyección de WebUI
└── server/                    ASP.NET Core (net10.0) + Microsoft.WebUI
    └── Program.cs             Carga protocol.bin y renderiza con estado C#
```

## Flujo

1. `webui build` compila las plantillas HTML a `dist/protocol.bin`.
2. ASP.NET Core carga el protocolo una vez y, en cada petición, envía la página por
   streaming (`/`) o de una vez (`/buffered`, para comparar).
3. En el navegador solo se descarga JS para `task-board`; `info-card` queda como HTML estático.

## Renderizado por streaming

`index.html` declara dos `<boundary>`: `facts` (tarjetas) y `tasks` (tablero). El servidor
(`Program.cs`) usa `WebUIHandler.StreamResponse`:

| Paso | Qué se envía | Estado que recibe |
|---|---|---|
| `Start` | `<head>` y cabecera hasta el primer boundary | `title`, `server` |
| `Resume(facts)` | Solo las tarjetas y su checkpoint | `facts` |
| `Advance` | HTML del padre hasta el siguiente boundary | (estado congelado) |
| `Resume(tasks)` | Solo el tablero y su checkpoint | `boardHeading`, `items`, `pendingCount` |
| `Advance` | Cola del documento y registro terminal | (estado congelado) |

Tras cada paso se hace `FlushAsync`. Las cargas lentas (simuladas con
`WebUi:FactsDelayMs` = 400 y `WebUi:TasksDelayMs` = 1500) arrancan en paralelo al principio.
Los boundaries salen en orden de documento, así que cada uno solo espera a sus datos.

En el cliente, `streaming.js` se carga con `<script type="module" async>` en `<head>` para que
el coordinador se ejecute mientras el documento aún se está parseando. Cada boundary se hidrata
en cuanto llega su checkpoint. `index.js` (registro de componentes) va al final con
`fetchpriority="low"`.

Medido en local (Chrome headless):

| | `/` streaming | `/buffered` |
|---|---|---|
| Primer byte de HTML | ~60 ms | ~1,58 s |
| Tarjetas visibles e hidratadas | ~0,42 s | ~1,58 s |
| Tablero interactivo | ~1,52 s | ~1,54 s |
| First Contentful Paint | ~0,8 s | ~1,56 s |

Para ver los eventos en el navegador: `window.__WEBUI_STREAMING_DEBUG__ = true` antes de cargar y
escuchar `webui:boundary-hydrated` y `webui:hydration-complete`. Sin flag, quedan marcas
`performance.mark('webui:boundary:<id>')`.

Reglas: un `<boundary>` puede envolver un `<for>` entero, pero no estar dentro de uno
(`boundary-in-repeat`), y no se pueden anidar.

### ¿Hacen falta boundaries para hacer streaming?

Sí. Un boundary es el único punto en el que WebUI pausa el render y devuelve el control al
servidor. Sin boundaries, `Start()` renderiza la página entera de una vez y devuelve
`Done = true`. Como necesita el estado completo, primero hay que esperar a todos los datos, y el
resultado es igual que `/buffered`. Los boundaries también son lo que permite hidratar cada región
por separado. Sin boundaries, `streaming.js` no hace falta.

### `streaming.js` aparece como "potentially blocking"

Chrome marca así los scripts `async` del `<head>`. No bloquean el parser y se descargan en
paralelo, pero se ejecutan en cuanto llegan. Si llegan antes del primer pintado, su ejecución
lo retrasa. Es lo que pide WebUI: el coordinador tiene que ejecutarse mientras el HTML todavía
está llegando. Con `defer` esperaría al final del documento, es decir, al final del stream.

Lo que importa es su peso. `streaming.js` ocupa 16,5 KB (6,6 KB gzip), pero importa el chunk
compartido del framework (69 KB, 21,5 KB gzip). En la práctica se ejecutan unos 86 KB.

### Las esperas simuladas

`/` y `/buffered` usan las mismas esperas (`facts` 400 ms, `tasks` 1500 ms, en paralelo), que
representan consultas lentas. Las dos tardan ~1,5 s en completar la respuesta. La diferencia es
que `/` envía cada parte cuando está lista, y `/buffered` no envía nada hasta tenerlo todo. Sin
esperas, los boundaries se resuelven al instante y las dos rutas se comportan igual. Para
probar sin retraso:

```bash
WebUi__FactsDelayMs=0 WebUi__TasksDelayMs=0 dotnet run
```

### Proxies, CDN y `X-Accel-Buffering`

El streaming solo funciona si nada entre Kestrel y el navegador acumula la respuesta.

- `X-Accel-Buffering: no` va dirigida a nginx. Le pide que reenvíe cada flush sin acumularlo.
  nginx no la reenvía al navegador. En esta POC no hay nginx, así que no tiene efecto.
- Otros intermediarios no la usan y tienen su propia configuración. En Cloudflare, por ejemplo,
  lo que rompe el streaming son las funciones que reescriben el HTML (Email Obfuscation, Rocket
  Loader y similares) y la caché de la página. Se desactivan en Cloudflare, no en `Program.cs`.
- Si el intermediario es un Worker que hace de proxy, tiene que devolver el cuerpo tal cual, sin
  leerlo entero (`await response.text()`).
- `Cache-Control: no-store` evita que se cachee la página dinámica.

### Compresión

La compresión es compatible con el streaming. gzip y brotli permiten cerrar un bloque a mitad de
respuesta para que el navegador lo descomprima y lo pinte. Hace falta que el compresor se vacíe
en cada `FlushAsync`, y en ASP.NET Core lo hace.

Medido con `AddResponseCompression` activado (en una copia aparte; la POC no comprime):

| `Accept-Encoding` | Tarjetas | Tablero | Bytes enviados |
|---|---|---|---|
| `identity` (sin comprimir) | ~0,40 s | ~1,51 s | ~6,3 KB |
| `gzip` | ~0,41 s | ~1,51 s | ~2,9 KB |
| `br` | ~0,44 s | ~1,55 s | ~2,9 KB |

El resultado es el mismo con y sin `DisableBuffering()`. Lo que mantiene el streaming es el
`FlushAsync` tras cada paso.

- Cada flush corta el bloque comprimido, así que se comprime algo peor que de una vez. Con 3-5
  fragmentos por página la diferencia no es relevante.
- El HTML pesa poco. Donde la compresión aporta de verdad es en los `.js`, que son estáticos y se
  pueden precomprimir en el build, aparte del streaming.
- Si un intermediario tiene que modificar una respuesta ya comprimida, debe descomprimirla y
  volver a comprimirla, y en ese proceso puede acumularla.

## Estrategias de carga del CSS

Opción `--css` de `webui build` (solo afecta a las hojas de los componentes; el `<style>` de
`index.html` siempre va inline):

| Modo | Qué genera | HTML de esta página |
|---|---|---|
| `link` (por defecto) | `<link rel="preload" as="style">` en `<head>` y `<link rel="stylesheet">` en cada shadow root | 4,1 KB |
| `style` | `<style>` con el CSS completo en cada instancia | 6,3 KB |
| `module` | Un `<script type="importmap">` con el CSS en data-URI y `shadowrootadoptedstylesheets` en cada shadow root, más un `<style>` de respaldo | 8,2 KB |

- `--css-bundle` agrupa hojas compartidas en chunks (solo con `link` o `style`). Aquí no cambia
  nada porque ningún CSS se comparte entre componentes.
- `--css-public-base=/` pone un prefijo absoluto en los `href` del modo `link`. Sin él son
  relativos (`info-card.css`) y fallarían en rutas anidadas.
- Con streaming, en modo `link` los `preload` salen en el primer chunk, así que el CSS de los
  boundaries se descarga mientras el servidor sigue esperando datos.

## Ejecutar

```bash
cd poc/webui/client
npm install
npm run build

cd ../server
dotnet run
# http://localhost:5180
```

Solo cliente, sin .NET (servidor de desarrollo de WebUI con recarga en caliente):

```bash
cd poc/webui/client
npm run build:client
npm run dev
# http://localhost:3000
```

## Versiones

El paquete npm y el NuGet deben ir en la misma versión, porque el formato de `protocol.bin`
lo genera la CLI de npm y lo lee el runtime nativo de NuGet. NuGet va por detrás de npm
(0.0.26 frente a 0.0.30 a 8/10/2026), así que ambos están fijados en **0.0.26**.

## Observaciones

- El paquete NuGet apunta a net8.0/net9.0. Funciona sin problemas en net10.0.
- No hace falta Rust: la CLI se instala con npm y trae binarios nativos.
- Las hojas de estilo de los componentes se emiten con rutas relativas (`info-card.css`).
  En rutas anidadas habría que usar `--css-public-base=/` (ver "Estrategias de carga del CSS").
- El build genera sourcemaps enlazados (`*.js.map`) y el servidor los sirve como
  `application/json`. En producción quedan públicos: usa `sourcemap: 'external'` o bloquea `.map`.
- La interacción en cliente (marcar o añadir tareas) no se persiste en el servidor.
  Para eso haría falta un endpoint y un `fetch` desde el componente.
