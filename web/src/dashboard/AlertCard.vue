<script setup lang="ts">
import { ref } from 'vue'
import type { Alert } from './types'

defineProps<{ alert: Alert; locked?: boolean }>()
defineEmits<{ cite: [document: string]; removeStore: [] }>()

// The summary fits in 2 lines; the rest opens on click (the detail is already in the charts and the chat).
const open = ref(false)
</script>

<template>
  <article class="alert" :class="`alert--${alert.severity}`">
    <header class="alert__header">
      <span class="alert__badge">
        <i class="alert__dot" />
        {{ alert.severity === 'critical' ? 'Crítico' : 'Atenção' }}
      </span>
      <span class="alert__store">{{ alert.store }}</span>
      <button
        type="button"
        class="alert__remove"
        :disabled="locked"
        title="Remover esta loja do painel (o agente reanalisa)"
        aria-label="Remover esta loja do painel"
        @click="$emit('removeStore')"
      >
        <i class="pi pi-eye-slash" />
      </button>
    </header>
    <h3 class="alert__title">{{ alert.title }}</h3>
    <p
      class="alert__summary"
      :class="{ 'alert__summary--open': open }"
      :title="open ? undefined : 'Clique para ler tudo'"
      @click="open = !open"
    >
      {{ alert.summary }}
    </p>
    <footer v-if="alert.citations.length" class="alert__citations">
      <button
        v-for="doc in alert.citations"
        :key="doc"
        type="button"
        class="alert__citation"
        :title="`Perguntar ao agente o que ${doc} diz`"
        @click="$emit('cite', doc)"
      >
        <i class="pi pi-file" />
        {{ doc.replace(/\.md$/, '') }}
      </button>
    </footer>
  </article>
</template>

<style scoped>
.alert {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 16px 12px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.alert--warning {
  --cor: var(--p-amber-400);
}

.alert--critical {
  --cor: var(--p-red-400);
  border-color: color-mix(in srgb, var(--p-red-400) 40%, var(--p-content-border-color));
}

.alert__header {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
}

.alert__badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 1px 9px 1px 7px;
  font-weight: 600;
  color: var(--cor);
  background: color-mix(in srgb, var(--cor) 14%, transparent);
  border-radius: 0;
}

.alert__dot {
  width: 6px;
  height: 6px;
  background: var(--cor);
  border-radius: 50%;
}

.alert__store {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--p-text-muted-color);
}

.alert__remove {
  display: inline-flex;
  align-items: center;
  margin-left: auto;
  padding: 6px;
  font: inherit;
  font-size: 13px;
  color: var(--p-text-muted-color);
  background: transparent;
  border: 0;
  border-radius: 0;
  cursor: pointer;
  opacity: 0.45;
  transition: opacity 0.15s;
}

.alert:hover .alert__remove,
.alert__remove:focus-visible {
  opacity: 1;
}

.alert__remove:hover:not(:disabled) {
  color: var(--p-text-color);
  background: var(--p-content-hover-background);
}

.alert__remove:disabled {
  cursor: default;
  opacity: 0.2;
}

.alert__title {
  margin: 0;
  font-size: 17px;
  font-weight: 600;
  line-height: 1.3;
}

.alert__summary {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  font-size: 15px;
  line-height: 1.5;
  color: var(--p-text-muted-color);
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  cursor: pointer;
}

.alert__summary--open {
  display: block;
  color: var(--p-text-color);
}

.alert__citations {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
  padding-top: 4px;
}

.alert__citation {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  max-width: 100%;
  padding: 1px 8px;
  overflow: hidden;
  font: inherit;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--p-text-muted-color);
  background: var(--p-surface-900);
  border: 0;
  border-radius: 0;
  cursor: pointer;
}

.alert__citation:hover {
  color: var(--p-text-color);
  background: var(--p-content-hover-background);
}

.alert__citation i {
  font-size: 11px;
}
</style>
