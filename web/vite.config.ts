import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// In development the dashboard runs on Vite and forwards API calls to the server,
// so the auth cookie stays same-origin. The production build is served by the server.
const server = "http://localhost:5131";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": server,
      "/hubs": { target: server, ws: true },
    },
  },
  build: {
    outDir: "../src/Skjermvette.Server/wwwroot",
    emptyOutDir: true,
  },
});
