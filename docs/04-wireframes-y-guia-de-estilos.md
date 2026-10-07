# 04 · Wireframes y guía de estilos

Un **wireframe** es un prototipo de pantalla para validar estructura y comportamiento antes de programar. Estos son interactivos: se pueden usar.

Enlace: **https://claude.ai/artifact/CdtFYo1ytRwCyHa7vQ3K82** (cómo abrirlo en el [README](README.md)).

## Las cuatro hojas

### 1 · Listado
- **Tarjetas de resumen** que filtran al pulsarlas: pendientes, en curso, críticas, VIP.
- **Filtros** que se aplican al instante y estado vacío cuando no hay resultados.
- **Tabla:** prioridad con color y texto; badge **VIP** en lugar de colores de fila sin leyenda; el título es el enlace a la consulta.
- **"Asignarme"** en las pendientes y paginación completa.

### 2 · Nueva incidencia
- **Formulario en cuatro bloques numerados:** qué ocurre, clasificación, personas, adjuntos.
- **Resumen de errores** con enlaces a cada campo, y error en el propio campo.
- **Buscador desplegable** para cambiar el interesado.
- **Adjuntos** que comprueban el máximo de 3 ficheros y 20 MB.
- **Pantalla de confirmación** al registrar.

### 3 · Consulta / resolución
- **Pestañas** que separan "Comunicación con el usuario" de "Trabajo interno CAU".
- **Mensajes** con estado vacío; la papelera junto a "enviar" desaparece.
- **Aviso de cambios sin guardar** antes de salir.
- **"Finalizar"** exige la solución técnica y confirma el comentario que recibirá el usuario.
- **Panel lateral** con personas e historial.

### 4 · Guía de estilos
Colores con su ratio de contraste, tipografía, espaciado, radios, botones, campos, badges, alertas, iconos y cómo trasladarlo a Razor Pages.

## Decisiones de diseño

- **Accesibilidad WCAG 2.1 AA:**
  - Contraste de texto de 4,5:1 o más.
  - Foco visible de 3 px (el contorno que marca el elemento activo al navegar con teclado).
  - Zonas pulsables de 44 px o más.
  - Nunca se transmite información solo con color.
- **Tipografía Atkinson Hyperlegible,** diseñada para personas con baja visión.
- **Verde corporativo oscurecido** (`#1B6B3A`) para que el texto blanco encima sea legible. El naranja se reserva para marcar el entorno PRE.
- **Tokens en variables CSS.** Los *tokens* son valores de diseño con nombre (`--c-primary`, `--r-md`…). Todas las pantallas comparten el mismo bloque `sirei.css`, que puede pasar tal cual a `wwwroot/css/`.
- **Iconos Lucide,** un set libre de licencia ISC.

## Supuestos a validar

- Las filas amarillas del original se han interpretado como **VIP**.
- Las **categorías** del alta son ejemplos.
- La **extensión del interesado** aparece como `[EXT]`.
- Los botones del **editor de texto** son decorativos.
- **"Gestión de formularios"** aparece en el menú pero aún no tiene pantalla.
