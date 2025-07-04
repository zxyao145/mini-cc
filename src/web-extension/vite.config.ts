import path from "path";
import { defineConfig } from 'vite'
import webExtension, { readJsonFile } from "vite-plugin-web-extension";

function root(...paths: string[]): string {
  return path.resolve(__dirname, ...paths);
}

function generateManifest() {
  const manifest = readJsonFile("src/manifest.json");
  const pkg = readJsonFile("package.json");
  return {
    name: pkg.name,
    description: pkg.description,
    version: pkg.version,
    ...manifest,
  };
}
export default defineConfig({
  plugins: [
    webExtension({
      manifest: generateManifest,
      watchFilePaths: ["package.json", root("src/manifest.json")],
      browser: process.env.TARGET || "chrome",
    }),
  ],
  build: {
    outDir: root("dist"),
    emptyOutDir: true,
  },
  // build: {
  //   outDir: 'dist',
  //   sourcemap: true,
  //   minify: false,
  //   rollupOptions: {
  //     output: {
  //       entryFileNames: '[name].js',
  //       chunkFileNames: '[name].js',
  //       assetFileNames: '[name].[ext]'
  //     }
  //   }
  // }
})