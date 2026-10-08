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
│   ├── data/state.json        Estado para `npm run dev` (sin .NET)
│   └── build-client.mjs       esbuild + proyección de WebUI
└── server/                    ASP.NET Core (net10.0) + Microsoft.WebUI
    └── Program.cs             Carga protocol.bin y renderiza con estado C#
```

## Flujo

1. `webui build` compila las plantillas HTML a `dist/protocol.bin`.
2. ASP.NET Core carga el protocolo una vez y, en cada petición, serializa el
   estado a JSON y llama a `WebUIHandler.Render(...)`.
3. En el navegador solo se descarga JS para `task-board`; `info-card` queda como HTML estático.

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
  En rutas anidadas habría que añadir un `<base href>` o usar `--css=style` o `--css=module`.
- La interacción en cliente (marcar o añadir tareas) no se persiste en el servidor.
  Para eso haría falta un endpoint y un `fetch` desde el componente.
