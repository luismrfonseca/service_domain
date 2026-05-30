import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5284',
        changeOrigin: true,
        secure: false
      }
    }
  },
  build: {
    outDir: process.env.VERCEL || process.env.CI ? 'dist' : '../service_domain_api/src/ServiceDomain.Api/wwwroot',
    emptyOutDir: true
  }
})
