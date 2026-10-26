import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vitest/config'

// Testes unitários do TypeScript puro (cliente AG-UI); as telas são verificadas à mão.
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
