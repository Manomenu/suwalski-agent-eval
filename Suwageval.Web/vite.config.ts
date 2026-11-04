import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
    plugins: [react()],
    server: {
        port: 3230,
        strictPort: true,
        proxy: {
            "/api": {
                target: process.env["API_PROXY_TARGET"] ?? "http://localhost:6230",
                changeOrigin: true,
                rewrite: (path) => path.replace(/^\/api/, ""),
            },
        },
    },
});
