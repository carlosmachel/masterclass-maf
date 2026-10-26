import { fileURLToPath, URL } from 'node:url'
import process from 'node:process'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Aspire injects the .NET agent URL (WithReference) as services__agent__http__0.
const agentUrl =
  process.env.services__agent__http__0 ?? process.env.AGENT_URL ?? 'http://localhost:5000'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: Number(process.env.PORT) || 4280,
    // The browser talks to /api/agent (same origin, no CORS); Vite forwards it to MapAGUIServer.
    proxy: {
      '/api/agent': {
        target: agentUrl,
        changeOrigin: true,
        rewrite: () => '/',
      },
      '/api/oncall': {
        target: agentUrl,
        changeOrigin: true,
        rewrite: () => '/oncall',
      },
      // MCP (Fase 16): fixed URL for Claude Code (.mcp.json); the agent port changes on every Aspire run.
      '/api/mcp': {
        target: agentUrl,
        changeOrigin: true,
        rewrite: () => '/mcp',
      },
    },
  },
})
