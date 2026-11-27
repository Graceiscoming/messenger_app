import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Dev server proxies REST and SignalR (websocket) to the API on :5080.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5080', changeOrigin: true, ws: true },
    },
  },
})
