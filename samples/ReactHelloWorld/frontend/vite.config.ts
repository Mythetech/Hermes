import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    dedupe: ['@hermes/bridge'],
  },
  server: {
    port: 5176,
    strictPort: true,
  },
});
