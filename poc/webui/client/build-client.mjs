// Bundles the hydration entry point with WebUI projection enabled.
// The projection manifest it writes is consumed by `webui build`.
import * as esbuild from 'esbuild';
import { esbuildProjection } from '@microsoft/webui/projection.js';

const watch = process.argv.includes('--watch');

const options = {
  entryPoints: ['src/index.ts'],
  outdir: 'dist',
  bundle: true,
  format: 'esm',
  splitting: true,
  minify: !watch,
  sourcemap: watch,
  metafile: true,
  plugins: [esbuildProjection()],
};

if (watch) {
  const context = await esbuild.context(options);
  await context.watch();
} else {
  await esbuild.build(options);
}
