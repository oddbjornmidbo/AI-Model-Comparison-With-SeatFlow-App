import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The dev server forwards /api to the .NET backend so the browser sees a single origin.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5080",
    },
  },
});
