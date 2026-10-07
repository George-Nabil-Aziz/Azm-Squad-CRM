import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      // Must match applicationUrl of the "http" profile in server/src/Crm.Api/Properties/launchSettings.json
      // xfwd: send X-Forwarded-For so the API (audit log) records the browser address, not the proxy's.
      '/api': { target: 'http://localhost:5080', xfwd: true },
      // SignalR hubs (notifications, live chat) need WebSocket proxying too.
      '/hubs': { target: 'http://localhost:5080', ws: true, xfwd: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
