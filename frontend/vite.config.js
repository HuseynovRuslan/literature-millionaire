import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // Üst qovluqdakı (PS Klub) postcss/tailwind konfiqurasiyası bura qarışmasın
  css: { postcss: {} },
  server: { port: 5180, host: true },
})
