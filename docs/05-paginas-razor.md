# 05 · Páginas Razor del prototipo

Detalle de cada fichero `.cshtml` de `src/SIREI.Web/Pages` y de lo que necesita para funcionar: modelo de página, servicios, vistas parciales, CSS y JavaScript.

El prototipo y cómo arrancarlo se describen en [src/README.md](../src/README.md). Qué es htmx se explica en [03-htmx.md](03-htmx.md).

## Mapa general

```
Pages/
├── _ViewImports.cshtml          usings y Tag Helpers para todas las páginas
├── _ViewStart.cshtml            layout por defecto: _Layout
├── Shared/
│   ├── _Layout.cshtml           cabecera, menú, pie, scripts comunes
│   └── _Aviso.cshtml            alerta verde de confirmación
├── Index.cshtml (+ .cs)         /                         Listado
├── _Kpis.cshtml                 tarjetas de resumen del listado
├── _Resultados.cshtml           tabla, paginación y estado vacío del listado
├── Incidencias/
│   ├── Nueva.cshtml (+ .cs)     /Incidencias/Nueva        Alta
│   ├── _ListaPersonas.cshtml    resultados del buscador de interesado
│   ├── Consulta.cshtml (+ .cs)  /Incidencias/{id}         Consulta / resolución
│   ├── _Mensaje.cshtml          un mensaje del registro
│   ├── _MensajeEnviado.cshtml   respuesta htmx al enviar un mensaje
│   ├── _Historial.cshtml        línea de tiempo de la incidencia
│   └── _BorradorGuardado.cshtml respuesta htmx al guardar el borrador
├── Estilos.cshtml               /Estilos                  Guía de estilos (estática)
└── Error.cshtml (+ .cs)         /Error                    Errores y 404
```

Convenciones:

- Las vistas parciales empiezan por `_`. Razor las busca primero en la carpeta de la página y después en `Pages/Shared`.
- Un *handler* es un método del modelo de página que responde a una URL con `?handler=Nombre`. Por ejemplo, `OnGetResultadosAsync` responde a `GET /?handler=Resultados`. Equivale a los eventos `btnX_Click` de Web Forms.
- Las respuestas htmx que actualizan varias zonas usan **swaps fuera de banda** (*out-of-band*). El fragmento principal va al destino de la petición y el resto lleva `hx-swap-oob` con el `id` del elemento que sustituye.

## Piezas comunes

### `_ViewImports.cshtml`

Importa para todas las páginas:

- los espacios de nombres `SIREI.Dominio`, `SIREI.Web.Infraestructura` y `SIREI.Web.Pages`;
- los Tag Helpers de ASP.NET Core (`asp-append-version`, `asp-antiforgery`…);
- los Tag Helpers propios del proyecto: `<icono>` y `<sirei-badge>`.

### `_ViewStart.cshtml`

Asigna `_Layout` como layout por defecto. Solo `Estilos.cshtml` lo anula (`Layout = null`).

### `Shared/_Layout.cshtml`

Esqueleto de todas las pantallas, salvo la guía de estilos.

| Zona | Detalle |
|---|---|
| `<head>` | Título `@ViewData["Title"] · SIREI`, favicon, fuentes Atkinson Hyperlegible (Google Fonts), `sirei.css` y `app.css`, sección opcional `Estilos` para el CSS propio de cada página |
| Token antiforgery | `<meta name="csrf-token">`, que `sirei.js` añade como cabecera `RequestVerificationToken` a cada petición htmx |
| Configuración htmx | `<meta name="htmx-config">`: sin estilos inyectados y sin desplazamiento automático al enfocar |
| Cabecera | Logo, badge "Entorno PRE", teléfono del CAU y botón del usuario actual |
| Menú lateral | Marca la opción activa con `aria-current="page"` según `ViewData["Seccion"]` (`"incidencias"` o `"nueva"`) |
| Cuerpo | `@RenderBody()`: cada página pinta su propio `<main id="main">` |
| Pie | Versión y enlaces legales |
| Secciones | `Dialogos` (después del pie) y `Scripts` (al final del `<body>`) |
| Región viva | `<div id="anuncio" aria-live="polite">`, oculta, para que el lector de pantalla lea los avisos |
| Scripts | `htmx.min.js` y `sirei.js` |

**Dependencias:**

- `IUsuarioActualServicio` (nombre, iniciales y rol de la cabecera)
- `IAntiforgery` (token)
- Tag Helper `<icono>`

### `Shared/_Aviso.cshtml`

Alerta verde con icono y botón "Cerrar aviso". Su modelo es el record `Aviso` (`Infraestructura/Aviso.cs`).

| Propiedad | Efecto |
|---|---|
| `Texto` | `null` pinta solo un hueco oculto `<div id="aviso" hidden>`, donde htmx colocará después el aviso |
| `FueraDeBanda` | Añade `hx-swap-oob="true"` para reemplazar el aviso desde una respuesta htmx |
| `Autofoco` | `data-autofoco="true"`: recibe el foco al cargar la página. Se usa tras una redirección, para que el lector de pantalla lo lea |

El cierre lo gestiona `sirei.js` (`[data-cerrar-aviso]`).

**Lo usan:** `Index`, `_Resultados`, `Consulta` y `_BorradorGuardado`.

## Listado · `Index.cshtml`

**Ruta:** `/` (también "Inicio" e "Incidencias" del menú)
**Modelo:** `IndexModel`
**Servicio:** `IIncidenciaServicio`

### Qué pinta

1. Cabecera de la bandeja y botón "Nueva incidencia".
2. Tarjetas de resumen (`_Kpis`).
3. Hueco del aviso (`_Aviso`).
4. Tarjeta con:
   - el formulario de filtros `#filtros`;
   - el resumen `#resumen` ("Mostrando 1–5 de 10 incidencias"), que es una región `aria-live`;
   - el contenedor `#resultados`, con `_Resultados` dentro.
5. Un elemento oculto que refresca los resultados cada 30 s, salvo si el foco está en la tabla o en las tarjetas.

### Modelo `IndexModel`

| Miembro | Uso |
|---|---|
| `Filtro` (`FiltroIncidencias`) | Se rellena desde la URL: `texto`, `estado`, `prioridad`, `vip`, `tam`, `pagina` |
| `Resultado`, `Resumen` | Página de filas y contadores, pedidos al servicio |
| `AvisoPendiente` (`[TempData]`) | Aviso que sobrevive a una redirección (camino sin JavaScript) |
| `EsFragmento`, `ActualizarResumen`, `AvisoTexto` | Indican a las parciales qué swaps fuera de banda añadir |
| `TextoResumen` | Texto del resumen |

### Handlers

| Handler | Petición | Respuesta |
|---|---|---|
| `OnGetAsync` | Carga normal de la página | Página completa |
| `OnGetResultadosAsync(auto)` | htmx: filtros, paginación, filas por página y refresco automático (`auto=true`) | `_Resultados`, más `#resumen` fuera de banda; si `auto`, también `_Kpis` |
| `OnPostAsignarAsync(id)` | Botón "Asignarme" | Con htmx: `_Resultados`, más resumen, tarjetas, aviso y anuncio fuera de banda. Sin JavaScript: redirección al listado con el aviso en TempData |

### Interacción

- **Filtros:** el formulario lleva `hx-get` al handler `Resultados`. Se dispara con `change` en los desplegables y la casilla, y con `input` (300 ms) en el buscador.
- **Tarjetas y "Restablecer filtros":** botones con `data-preset`. `sirei.js` rellena el formulario y lanza la petición.
- **Sin JavaScript:** el formulario es un `GET` normal, con un botón "Aplicar filtros" dentro de `<noscript>`.

### Parciales

#### `_Kpis.cshtml`

`<section id="kpis">` con las cuatro tarjetas: pendientes, en curso, críticas y VIP.

- Cada tarjeta es un `<button data-preset='{…}'>` que aplica un filtro.
- En respuestas htmx sale con `hx-swap-oob="true"`.

**Modelo:** `IndexModel`.

#### `_Resultados.cshtml`

Contenido de `#resultados`. **Modelo:** `IndexModel`.

- **`#pagina-actual`:** campo oculto con la página. Lo incluyen el refresco y "Asignarme" para no volver a la página 1.
- **Con resultados:**
  - Tabla con `<caption>` oculto y cabeceras con `scope`. Estado y prioridad con `<sirei-badge>`; badge VIP con `<icono nombre="star">`.
  - "Asignarme" solo en las pendientes. Es un `<form method="post">` con `hx-post`, así que también funciona sin JavaScript. Tras asignar, el foco va a "Ver" de esa fila (`data-foco="#ver-1507|#resumen"`).
  - Navegación de paginación:
    - el desplegable "Filas por página" está asociado al formulario de filtros mediante `form="filtros"`;
    - los botones de página y Anterior/Siguiente llevan `hx-get` y `hx-vals='{"pagina": n}'`.
- **Sin resultados:** estado vacío con "Restablecer filtros", y un campo oculto `tam` que conserva las filas por página.
- **Solo en respuestas htmx:** `#resumen`, `_Kpis`, `_Aviso` y `#anuncio` fuera de banda.

### Dependencias

| Tipo | Elementos |
|---|---|
| Servicios | `IIncidenciaServicio` (`BuscarAsync`, `ResumenAsync`, `AsignarmeAsync`) |
| Dominio | `FiltroIncidencias`, `FiltroEstado`, `Prioridad`, `EstadoIncidencia`, `ResultadoBusqueda`, `ResumenIncidencias` |
| Parciales | `_Kpis`, `_Resultados`, `_Aviso` |
| Infraestructura | `Presentacion.Fecha`, `HtmxExtensions.EsHtmx`, `Aviso` |
| Tag Helpers | `<icono>`, `<sirei-badge>` |
| CSS | `sirei.css`, `app.css` (`.filtro-buscar`: dos columnas en escritorio, una en móvil) |
| JS | `htmx`, `sirei.js` (presets, foco tras el swap, avisos) |

## Nueva incidencia · `Incidencias/Nueva.cshtml`

**Ruta:** `/Incidencias/Nueva` (confirmación: `/Incidencias/Nueva?registrada=1513`)
**Modelo:** `NuevaModel`
**Servicios:** `IIncidenciaServicio`, `IPersonaServicio`, `ICatalogoServicio`, `IUsuarioActualServicio`

### Qué pinta

**Formulario.** Si no hay `registrada`, se muestra:

1. Ruta de navegación y título.
2. Resumen de errores `#resumen-errores`, siempre presente pero oculto si no hay errores. Tiene un enlace por campo.
3. Formulario `multipart/form-data` en cuatro bloques:
   - **Qué ocurre:** título y descripción. La descripción tiene una barra de formato decorativa y un contador de caracteres.
   - **Clasificación:**
     - categoría, que sale de `ICatalogoServicio`;
     - prioridad, por defecto Normal.
   - **Personas:**
     - el solicitante es el usuario actual;
     - el interesado se cambia con el botón "Cambiar" y el panel `#panel-interesado`, que incluye un buscador.
   - **Adjuntos:** zona de arrastrar y soltar, lista de ficheros y mensaje de error. Las filas de la lista se crean desde un `<template id="tpl-fichero">`.
4. Panel de ayuda lateral.

**Confirmación.** Si hay `registrada`, se muestra una tarjeta con el número, el título, la prioridad y el interesado. Tiene tres enlaces: ver la incidencia, registrar otra y volver al listado. Recibe el foco al cargar.

### Modelo `NuevaModel`

| Miembro | Uso |
|---|---|
| `Input` (`EntradaNueva`) | Campos del formulario, con DataAnnotations: `[Required]` con el mensaje en español y `[StringLength]` |
| `Adjuntos` (`List<IFormFile>`) | Ficheros subidos |
| `MaxFicheros`, `MaxBytes` | Límites: 3 ficheros y 20 MB. Se pasan al HTML en `data-max-*` para que el navegador aplique los mismos límites |
| `ErrorTitulo`, `ErrorDescripcion`, `ErrorCategoria` | Textos de error compartidos por el servidor y la vista |
| `Interesado`, `Personas`, `Registrada`, `ErrorFicheros`, `Intentado` | Estado para pintar la página |
| `TieneError(campo)` | Consulta `ModelState["Input.Campo"]` |

### Handlers

| Handler | Petición | Respuesta |
|---|---|---|
| `OnGetAsync(registrada)` | Formulario vacío o confirmación | Página |
| `OnPostAsync` | Envío del formulario | Si hay errores: la página con errores y el foco en el resumen. Si es válido: crea la incidencia y redirige a `?registrada={id}` (patrón PRG) |
| `OnGetPersonasAsync(q)` | htmx: buscador del interesado | `_ListaPersonas` |

**Validación en el servidor:**

- campos obligatorios;
- que la categoría exista en el catálogo;
- que el interesado exista (si no, se usa el usuario actual);
- número y tamaño total de los adjuntos.

### Parcial `_ListaPersonas.cshtml`

`<li>` de cada persona con un botón "Seleccionar" que lleva `data-persona`, `data-nombre` y `data-iniciales`. Si la petición trae `q`, añade fuera de banda el número de resultados (`#f-buscar-res`, región viva).

**Modelo:** `NuevaModel`.

### Interacción (`nueva.js`)

- **Validación en el navegador:** se repite antes de enviar, para no perder los adjuntos.
  - Al enviar con errores, muestra el resumen y le da el foco.
  - Después, recalcula los errores al escribir.
  - Mantiene al día `aria-invalid` y `aria-describedby`.
- **Interesado:**
  - abre y cierra el panel (`aria-expanded`);
  - Intro en el buscador no envía el formulario;
  - "Seleccionar" actualiza el campo oculto, devuelve el foco a "Cambiar" y lo anuncia.
- **Adjuntos:**
  - las selecciones sucesivas se acumulan (con `DataTransfer`);
  - se comprueban los límites y se admite arrastrar y soltar;
  - cada fichero se puede quitar; al hacerlo, el foco pasa al siguiente botón "Quitar".

### Dependencias

| Tipo | Elementos |
|---|---|
| Servicios | `IIncidenciaServicio` (`CrearAsync`, `ObtenerAsync`), `IPersonaServicio`, `ICatalogoServicio`, `IUsuarioActualServicio` |
| Dominio | `NuevaIncidencia`, `Adjunto`, `Persona`, `Prioridad`, `IncidenciaDetalle` |
| Parciales | `_ListaPersonas` |
| Infraestructura | `Presentacion.Plural`, `Presentacion.Tamano` |
| Tag Helpers | `<icono>` |
| CSS | `sirei.css`, `app.css`, `formulario.css` (editor, zona de adjuntos, números de paso) |
| JS | `htmx` (buscador), `sirei.js` (contador, enlaces del resumen de errores, autofoco), `nueva.js` |

## Consulta / resolución · `Incidencias/Consulta.cshtml`

**Ruta:** `/Incidencias/{id:int}`; si el id no existe, devuelve 404
**Modelo:** `ConsultaModel`
**Servicios:** `IIncidenciaServicio`, `TimeProvider` (para escribir "hoy a las 10:20")

### Qué pinta

1. Ruta de navegación, número y título.
2. Badges de estado, prioridad y categoría, más la línea "Enviada… · Actualizada…". La fecha de actualización está en `#actualizada`.
3. Hueco del aviso (`_Aviso`).
4. Columna principal:
   - **Descripción del usuario** y adjuntos originales.
   - **Pestañas** (`role="tablist"`):
     - *Comunicación con el usuario:* registro de mensajes `#log` (`role="log"`), formulario de nuevo mensaje y comentario de cierre.
     - *Trabajo interno CAU:* notas internas con "Guardar notas", solución técnica (obligatoria para finalizar) y ficheros de la solución.
5. Columna lateral:
   - **Personas:** interesado con botón "Llamar", solicitante y persona asignada.
   - **Historial** (`_Historial`).
6. Barra inferior fija:
   - "Volver al listado";
   - badge "Cambios sin guardar";
   - "Guardar borrador" y "Finalizar incidencia". Estos dos botones se ocultan si la incidencia está finalizada.
7. `<form id="f-gestion">`, vacío y oculto. Los campos de cierre, notas, solución y ficheros se asocian a él con `form="f-gestion"`, aunque estén repartidos por las pestañas.
8. **Sección `Dialogos`:** dos `<dialog>` nativos.
   - `#dlg-finalizar`: muestra el comentario de cierre antes de confirmar.
   - `#dlg-salir`: aparece al salir con cambios sin guardar.

### Modelo `ConsultaModel`

| Miembro | Uso |
|---|---|
| `Id` (`[FromRoute]`) | Id de la URL |
| `Incidencia` (`IncidenciaDetalle`) | Datos completos |
| `AvisoPendiente` (`[TempData]`), `AvisoTexto` | Aviso tras una redirección o en una respuesta htmx |
| `Pestana`, `ErrorSolucion`, `Sucio` | Estado al volver a pintar tras un error de validación sin JavaScript |
| `HistorialFueraDeBanda` | `_Historial` sale con `hx-swap-oob` |
| `Finalizada`, `Actualizada`, `EstadoNotas`, `Ahora` | Textos calculados |

### Handlers

| Handler | Petición | Respuesta |
|---|---|---|
| `OnGetAsync` | Carga normal | Página |
| `OnPostMensajeAsync(texto)` | htmx: enviar mensaje (`hx-swap="beforeend"` en `#log`) | `_MensajeEnviado`. Sin JavaScript: redirección. Si la incidencia está finalizada o el texto viene vacío: 400 |
| `OnPostNotasAsync(notas)` | htmx: "Guardar notas" | Texto "Notas guardadas a las HH:mm", que va a `#f-notas-hint` |
| `OnPostBorradorAsync(cierre, notas, sol, salir)` | htmx: "Guardar borrador", desde la barra o desde el diálogo de salida | `_BorradorGuardado` |
| `OnPostFinalizarAsync(cierre, notas, sol, ficherosSolucion)` | Envío normal de `#f-gestion` desde el diálogo | Redirección a la consulta con el aviso "Incidencia finalizada". Si falta la solución, vuelve a pintar la página en la pestaña interna con el error |

### Parciales

#### `_Mensaje.cshtml`

Un mensaje: autor, hora y burbuja.

- **Modelo:** la tupla `(Mensaje, DateTimeOffset Ahora)`.
- **Lo usan:** `Consulta` y `_MensajeEnviado`.

#### `_MensajeEnviado.cshtml`

Respuesta htmx tras enviar un mensaje:

- el mensaje nuevo, que se añade al final de `#log`, de modo que el lector solo anuncia ese mensaje;
- si es el primero, `#sin-mensajes` con `hx-swap-oob="delete"` para quitar el estado vacío;
- `#actualizada` e historial, fuera de banda.

#### `_Historial.cshtml`

`<ol id="historial" class="timeline">` con los eventos, del más reciente al más antiguo. Cuando `HistorialFueraDeBanda` es verdadero, sale con `hx-swap-oob="true"`.

#### `_BorradorGuardado.cshtml`

Respuesta htmx tras guardar el borrador:

- el historial, que es el destino de la petición (`hx-swap="outerHTML"`);
- aviso, `#actualizada` y `#f-notas-hint`, fuera de banda.

### Interacción (`consulta.js`)

- **Pestañas:** se cambian con clic, con las flechas izquierda y derecha, y con Inicio y Fin. Solo la pestaña activa recibe el tabulador (`tabindex`).
- **Cambios sin guardar:**
  - Cualquier campo con `data-sucio` muestra el badge y activa "Guardar borrador".
  - "Volver al listado" abre `#dlg-salir`.
  - Si se cierra o recarga la pestaña, avisa `beforeunload`.
- **Diálogos:** usan `showModal()`, así que el foco queda atrapado, Esc los cierra y el fondo queda inerte. Al cerrar, el foco vuelve al botón que los abrió.
- **Mensajes:** "Enviar" solo se activa con texto y "Descartar" solo aparece con texto. Tras enviar, se vacía el campo y el registro baja hasta el último mensaje.
- **Finalizar:** si falta la solución, abre la pestaña interna, marca el error (también en la pestaña: "1 error") y lleva el foco al campo. Si está completa, abre `#dlg-finalizar` con la vista previa del comentario.
- **Ficheros de la solución:** máximo 3 (los que sobran se descartan). La lista se muestra en un texto de ayuda vivo.

### Dependencias

| Tipo | Elementos |
|---|---|
| Servicios | `IIncidenciaServicio` (`ObtenerAsync`, `EnviarMensajeAsync`, `GuardarNotasAsync`, `GuardarBorradorAsync`, `FinalizarAsync`), `TimeProvider` |
| Dominio | `IncidenciaDetalle`, `Mensaje`, `EventoHistorial`, `Adjunto`, `GestionIncidencia`, `EstadoIncidencia` |
| Parciales | `_Aviso`, `_Mensaje`, `_Historial`, `_MensajeEnviado`, `_BorradorGuardado` |
| Infraestructura | `Presentacion` (fechas, horas, tamaños, clase de prioridad), `HtmxExtensions.EsHtmx`, `Aviso` |
| Tag Helpers | `<icono>`, `<sirei-badge>` |
| CSS | `sirei.css`, `app.css`, `consulta.css` (pestañas, burbujas, línea de tiempo, `<dialog>`, foco del botón de adjuntar) |
| JS | `htmx`, `sirei.js` (contadores, foco tras el swap, avisos), `consulta.js` |

## Guía de estilos · `Estilos.cshtml`

**Ruta:** `/Estilos`. No tiene modelo de página ni servicios.

Es una copia literal del tablero "Guía de estilos" de los wireframes:

- principios de accesibilidad, colores con su ratio de contraste, tipografía, espaciado, botones, campos, badges, iconos y paso a Razor Pages;
- no usa el layout (`Layout = null`), porque es un documento de referencia sin menú;
- tiene su propio `<head>` con `sirei.css`, `app.css` y `guia.css`;
- los iconos van como SVG en línea, no con `<icono>`.

**Dependencias:** solo CSS.

## Error · `Error.cshtml`

**Ruta:** `/Error`
**Modelo:** `ErrorModel`

- Muestra una tarjeta con el título, la explicación y "Volver al listado".
- `Program.cs` la usa para dos cosas:
  - las excepciones en producción (`UseExceptionHandler`);
  - los códigos de estado (`UseStatusCodePagesWithReExecute("/Error", "?codigo={0}")`).
- Con `codigo=404` muestra "No se encuentra la página".

**Dependencias:** solo el layout.

## Dependencias transversales

### Servicios registrados (`AddServiciosFake`)

Todos son *singleton*: un único almacén en memoria para toda la aplicación, que vuelve a los datos de ejemplo en cada arranque.

| Interfaz (`SIREI.Dominio`) | Implementación fake | Páginas que la usan |
|---|---|---|
| `IIncidenciaServicio` | `IncidenciaServicioFake` | Index, Nueva, Consulta |
| `IPersonaServicio` | `PersonaServicioFake` | Nueva |
| `ICatalogoServicio` | `CatalogoServicioFake` | Nueva |
| `IUsuarioActualServicio` | `UsuarioActualServicioFake` | Layout, Nueva |
| `TimeProvider` | `TimeProvider.System` | Consulta, servicio de incidencias |

### Infraestructura de `SIREI.Web`

| Pieza | Función | Dónde se usa |
|---|---|---|
| `TagHelpers/IconoTagHelper` | `<icono nombre="plus" class="i i-sm" />` → SVG de Lucide con `aria-hidden="true"` | Todas las páginas con layout |
| `TagHelpers/SireiBadgeTagHelper` | `<sirei-badge estado="…" />` o `prioridad="…"` → badge con su color y siempre con texto | `_Resultados`, `Consulta` |
| `Infraestructura/Presentacion` | Formatos en español: fechas, "hoy a las…", horas, tamaños ("1,5 MB"), plurales y clases CSS de estado y prioridad | Index, Nueva, Consulta y sus parciales |
| `Infraestructura/HtmxExtensions` | `Request.EsHtmx()`: distingue una petición htmx de una normal | Index, Consulta |
| `Infraestructura/Aviso` | Modelo de `_Aviso` | Index, Consulta y sus parciales |

### CSS (`wwwroot/css`)

| Fichero | Contenido | Páginas |
|---|---|---|
| `sirei.css` | Bloque común de los wireframes, **sin cambios**: tokens `--c-*`, `--r-*`, `--shadow-*` y componentes | Todas |
| `app.css` | Añadidos de la implementación: `[hidden]` con prioridad, `aria-busy` y el ajuste del buscador en móvil | Todas |
| `formulario.css` | Editor, zona de adjuntos, título de sección y número de paso | Nueva |
| `consulta.css` | Pestañas, lista de definición, burbujas, línea de tiempo, `<dialog>` nativo | Consulta |
| `guia.css` | Muestras de color y tokens | Estilos |

### JavaScript (`wwwroot/js`)

| Fichero | Contenido | Páginas |
|---|---|---|
| `lib/htmx/htmx.min.js` | htmx 2.0.11, servido desde el propio proyecto | Todas las de layout |
| `sirei.js` | Varias funciones comunes: <ul><li>token antiforgery en htmx</li><li>`aria-busy` durante las peticiones</li><li>foco tras el swap (`data-foco`, con alternativas separadas por `\|`)</li><li>no repetir en voz alta una región viva que no cambia</li><li>cerrar avisos</li><li>contadores de caracteres (`data-contador`)</li><li>presets del listado</li><li>enlaces del resumen de errores</li><li>autofoco</li></ul> | Todas las de layout |
| `nueva.js` | Validación, interesado y adjuntos | Nueva |
| `consulta.js` | Pestañas, cambios sin guardar, diálogos, mensajes y finalización | Consulta |

### Atributos `data-*` que conectan el HTML con el JavaScript

| Atributo | Significado |
|---|---|
| `data-foco="#a\|#b"` | Tras el swap de htmx, da el foco al primero de esos elementos que exista |
| `data-preset='{"estado":"Pendiente"}'` | Aplica ese filtro en el listado |
| `data-autofoco="true"` | Recibe el foco al cargar la página |
| `data-cerrar-aviso` | Cierra el aviso que lo contiene |
| `data-contador="id"` y `data-plantilla="… {n} …"` | Actualiza el contador de caracteres |
| `data-obligatorio="id-error"` y `data-describedby="ids"` | Campo obligatorio de la nueva incidencia |
| `data-sucio` | Editar este campo marca la consulta como "con cambios sin guardar" |
| `data-cerrar-dialogo` | Cierra el `<dialog>` que lo contiene |

Razor no elimina un atributo `data-*` cuando su valor es `null`. Por eso los atributos booleanos se escriben siempre con `"true"` o `"false"`, y el JavaScript compara con `'true'`.
