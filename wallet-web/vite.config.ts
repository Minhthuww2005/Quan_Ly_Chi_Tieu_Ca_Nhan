import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    proxy: {
      '/hubs': { target: 'http://localhost:5081', changeOrigin: true, ws: true },
      '/api': {
        target: 'http://localhost:5081',
        changeOrigin: true,
      },
    },
  },
})
