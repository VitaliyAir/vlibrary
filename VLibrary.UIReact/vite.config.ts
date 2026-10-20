import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Ports are chosen not to clash with VGoalsPlanner (API 5016/7135, UI 5173):
// VLibrary API = http://localhost:5080 (VLibrary.Api launch profile), UI = http://127.0.0.1:5175.
// Requests to /api are proxied to the API, so no CORS setup is needed for the dev server.
export default defineConfig({
  plugins: [react()],
  server: {
    // Explicit IPv4: on this machine "localhost" resolves to ::1 and Node would listen on the IPv6 loopback only,
    // so http://127.0.0.1:5175 would not answer.
    host: '127.0.0.1',
    port: 5175,
    // Fail with a clear error instead of silently moving to another port.
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
