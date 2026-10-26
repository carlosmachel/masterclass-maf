<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ToolCallView } from '@/agui/useAgent'
import { REJECTED, blockedDocuments, duration, prettyJson, argsSummary, toolLabel } from './tools'

const props = defineProps<{ call: ToolCallView; awaitingApproval?: boolean; debug?: boolean }>()

const open = ref(false)
const expanded = computed(() => open.value || props.debug)

const info = computed(() => toolLabel(props.call.name))
const summary = computed(() => argsSummary(props.call.args))
const rejected = computed(() => props.call.result === REJECTED)
const blocked = computed(() => blockedDocuments(props.call.result))

type Status = 'running' | 'awaiting' | 'done' | 'rejected'
const status = computed<Status>(() => {
  if (rejected.value) return 'rejected'
  if (props.awaitingApproval) return 'awaiting'
  return props.call.status === 'done' ? 'done' : 'running'
})

const statusIcon: Record<Status, string> = {
  running: 'pi pi-spin pi-spinner',
  awaiting: 'pi pi-shield',
  done: 'pi pi-check',
  rejected: 'pi pi-times',
}
const statusText: Record<Status, string> = {
  running: 'executando',
  awaiting: 'aguardando aprovação',
  done: 'concluída',
  rejected: 'rejeitada',
}
</script>

<template>
  <div class="tool">
    <button
      type="button"
      class="tool__chip"
      :class="`tool__chip--${status}`"
      :aria-expanded="expanded"
      @click="open = !open"
    >
      <i :class="statusIcon[status]" class="tool__status" />
      <i :class="info.icon" class="tool__icon" />
      <span class="tool__name">{{ info.name }}</span>
      <span v-if="summary" class="tool__summary">{{ summary }}</span>
      <span v-if="status === 'awaiting' || status === 'rejected'" class="tool__label">
        {{ statusText[status] }}
      </span>
      <span v-else-if="call.endedAt" class="tool__time">{{ duration(call.startedAt, call.endedAt) }}</span>
      <i class="pi tool__arrow" :class="expanded ? 'pi-chevron-up' : 'pi-chevron-down'" />
    </button>
    <p v-if="blocked.length" class="tool__shield">
      <i class="pi pi-shield" />
      Prompt Shields bloqueou {{ blocked.join(', ') }}: instruções escondidas para a IA
    </p>

    <div v-if="expanded" class="tool__debug">
      <dl class="tool__meta">
        <dt>tool</dt>
        <dd>{{ call.name }}</dd>
        <dt>toolCallId</dt>
        <dd>{{ call.id }}</dd>
        <dt>estado</dt>
        <dd>
          {{ statusText[status]
          }}<template v-if="call.endedAt"> em {{ duration(call.startedAt, call.endedAt) }}</template>
        </dd>
      </dl>
      <p class="tool__section">Argumentos</p>
      <pre class="tool__json">{{ prettyJson(call.args) || '{}' }}</pre>
      <template v-if="call.result !== undefined">
        <p class="tool__section">Resultado</p>
        <pre class="tool__json tool__json--result">{{ prettyJson(call.result) }}</pre>
      </template>
    </div>
  </div>
</template>

<style scoped>
.tool {
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-self: stretch;
}

.tool__chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  align-self: flex-start;
  max-width: 100%;
  padding: 4px 12px;
  font: inherit;
  font-size: 15px;
  color: var(--p-text-color);
  background: transparent;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  cursor: pointer;
  transition:
    background 0.15s,
    border-color 0.15s;
}

.tool__chip:hover {
  background: var(--p-content-hover-background);
}

.tool__chip:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 2px;
}

.tool__status {
  font-size: 13px;
}

.tool__chip--running .tool__status {
  color: var(--p-primary-color);
}

.tool__chip--done .tool__status {
  color: var(--p-green-400);
}

.tool__chip--awaiting {
  border-color: var(--p-amber-400);
}

.tool__chip--awaiting .tool__status,
.tool__chip--awaiting .tool__label {
  color: var(--p-amber-400);
}

.tool__chip--rejected {
  opacity: 0.7;
}

.tool__chip--rejected .tool__name {
  text-decoration: line-through;
}

.tool__shield {
  display: flex;
  align-items: center;
  gap: 8px;
  align-self: flex-start;
  margin: 0;
  padding: 4px 12px;
  font-size: 14px;
  color: var(--p-red-300);
  background: color-mix(in srgb, var(--p-red-500) 12%, transparent);
  border: 1px solid var(--p-red-700);
  border-radius: 0;
}

.tool__icon {
  font-size: 14px;
  color: var(--p-text-muted-color);
}

.tool__name {
  font-weight: 600;
  white-space: nowrap;
}

.tool__summary {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--p-text-muted-color);
}

.tool__time,
.tool__label {
  font-size: 13px;
  white-space: nowrap;
  color: var(--p-text-muted-color);
}

.tool__arrow {
  font-size: 11px;
  color: var(--p-text-muted-color);
}

.tool__debug {
  padding: 12px 14px;
  font-size: 13px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.tool__meta {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 2px 12px;
  margin: 0 0 8px;
  font-family: var(--font-mono);
}

.tool__meta dt {
  color: var(--p-text-muted-color);
}

.tool__meta dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.tool__section {
  margin: 8px 0 4px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}

.tool__json {
  max-height: 240px;
  margin: 0;
  padding: 8px 10px;
  overflow: auto;
  font-family: var(--font-mono);
  font-size: 12px;
  line-height: 1.45;
  white-space: pre;
  background: var(--p-surface-900);
  border-radius: 0;
}
</style>
