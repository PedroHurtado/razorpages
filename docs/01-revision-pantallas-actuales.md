# 01 · Revisión de las pantallas actuales

Análisis de las capturas del 29/09/2026, tomadas en el entorno PRE (preproducción) con SIREI v1.4.

## Listado de incidencias (`GListadoIncidencias.aspx`)

- **Datos que no cuadran:**
  - La incidencia 1507 aparece como *Pendiente* en el listado y como *En curso* en la consulta. Hay que confirmar que el listado se refresca.
  - María del Yermo figura a la vez como técnica asignada y como interesada.
- **Colores de fila sin leyenda:** hay filas naranjas y amarillas y no se sabe qué significan.
- **La prioridad no destaca:** "Crítica" y "Alta" se ven igual que "Normal".
- **Paginación incompleta:** hay 251 incidencias (unas 26 páginas), pero solo se ofrecen del 1 al 10 y "…", sin anterior ni siguiente.
- **Columna inútil:** "Fecha resolución" siempre está vacía con el filtro por defecto.
- **Iconos de acción sin texto** ni nombre accesible.

## Nueva incidencia (`GNuevaIncidencia.aspx`)

- **Maquetación desequilibrada:** gran hueco vacío bajo "Título".
- **Obligatoriedad poco clara:** solo "Título" lleva asterisco.
- **Prioridad elegida por el usuario:** tenderá a subirla.
- **Adjuntos sin estilo:** se usa el botón de fichero nativo del navegador.
- **Editor de texto con demasiadas opciones:** fuentes, colores, tablas e imágenes. Las imágenes podrían saltarse el límite de adjuntos.

## Consulta de incidencia (`GConsultaIncidencia.aspx`)

- **Se puede perder trabajo:** solo existe "Guardar notas internas". Lo demás se guarda al finalizar, y con "Volver" se pierde sin aviso.
- **Papelera junto a "enviar"** en los mensajes, sin confirmación.
- **Faltan datos:** a quién está asignada, adjuntos originales, historial de cambios.
- **Iconos de teléfono sin etiqueta.**

## Coherencia entre pantallas

- "4000" frente a "4.000" caracteres restantes.
- "3 ficheros y 20 MB" frente a "3 ficheros, 20 MB".
- "Prioridad" frente a "Tipo Prioridad".
- Etiquetas con y sin dos puntos.

## Accesibilidad

El pie muestra el sello **WCAG 2.1 AA** (WCAG son las pautas internacionales de accesibilidad web; el nivel AA es el que exige a la Administración el RD 1112/2018). Sin embargo:

- **Contraste insuficiente:** el texto blanco sobre el naranja de la cabecera y sobre los botones verdes no llega al mínimo de 4,5:1 (relación de luminosidad entre texto y fondo).
- **Etiquetas demasiado pequeñas.**
- **Iconos sin nombre accesible**, es decir, sin texto que pueda leer un lector de pantalla.

Antes de mantener el sello conviene pasar una auditoría con herramientas automáticas como axe o WAVE.
