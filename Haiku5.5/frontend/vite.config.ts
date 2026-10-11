import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API runs on :5190 (see backend/src/SeatFlow.Api/Properties/launchSettings.json).
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5190',
    },
  },
});
