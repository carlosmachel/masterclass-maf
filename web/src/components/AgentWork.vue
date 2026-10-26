<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ToolCallView } from '@/agui/useAgent'
import ToolCallChip from './ToolCallChip.vue'
import { summarizeWork, type WorkStep } from './conversation'
import { blockedDocuments, toolLabel } from './tools'

// The agent's work in a turn (narrations + queries) on a single line that opens up for the details.
// The live step-by-step is in the dashboard's progress panel; this is the record.
const props = defineProps<{
  steps: WorkStep[]
  toolCalls: Record<string, ToolCallView>
  debug?: boolean
}>()

const open = ref(false)
const expanded = computed(() => open.value || props.debug)

const summary = computed(() => summarizeWork(props.steps, props.toolCalls, blockedDocuments))
const title = computed(() => {
  const { queries, seconds, inProgress } = summary.value
  if (inProgress) return `${toolLabel(inProgress).name}…`
  return `${queries} ${queries === 1 ? 'consulta' : 'consultas'} · ${seconds ? `${seconds} s` : '< 1 s'}`
})
// Icons of the tools used, without repeats: gives an idea of what was queried without opening it.
const icons = computed(() => [
  ...new Set(
    props.steps.flatMap((s) =>
      s.type === 'tool' && props.toolCalls[s.id] ? [toolLabel(props.toolCalls[s.id]!.name).icon] : [],
    ),
  ),
])
</script>

<template>
  <section class="work" :class="{ 'work--open': expanded }">
    <button type="button" class="work__header" :aria-expanded="expanded" @click="open = !open">
      <i
        class="work__status"
        :class="summary.inProgress ? 'pi pi-spin pi-spinner' : 'pi pi-check'"
      />
      <span class="work__title">{{ title }}</span>
      <span class="work__icons" aria-hidden="true">
        <i v-for="icon in icons" :key="icon" :class="icon" />
      </span>
      <i class="pi work__arrow" :class="expanded ? 'pi-chevron-up' : 'pi-chevron-down'" />
    </button>
    <!-- The Prompt Shields block is a stage moment: it stays in sight even with the block closed. -->
    <p v-if="summary.blocked.length && !expanded" class="work__shield">
      <i class="pi pi-shield" />
      Prompt Shields bloqueou {{ summary.blocked.join(', ') }}
    </p>

    <ol v-if="expanded" class="work__steps">
      <li v-for="step in steps" :key="step.id">
        <p v-if="step.type === 'narration'" class="work__narration">{{ step.text }}</p>
        <ToolCallChip v-else-if="toolCalls[step.id]" :call="toolCalls[step.id]!" :debug="debug" />
      </li>
    </ol>
  </section>
</template>

<style scoped>
.work {
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-self: stretch;
}

.work__header {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  align-self: flex-start;
  padding: 6px 12px 6px 10px;
  font: inherit;
  font-size: 15px;
  color: var(--p-text-muted-color);
  background: transparent;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  cursor: pointer;
  transition:
    background 0.15s,
    color 0.15s;
}

.work__header:hover,
.work--open .work__header {
  color: var(--p-text-color);
  background: var(--p-content-hover-background);
}

.work__header:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 2px;
}

.work__status {
  font-size: 13px;
  color: var(--p-green-400);
}

.work__status.pi-spinner {
  color: var(--p-primary-color);
}

.work__title {
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.work__icons {
  display: inline-flex;
  gap: 8px;
  font-size: 13px;
  opacity: 0.7;
}

.work__arrow {
  font-size: 11px;
}

.work__shield {
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

.work__steps {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 4px 0 4px 14px;
  list-style: none;
  border-left: 2px solid var(--p-content-border-color);
}

.work__narration {
  margin: 0;
  font-size: 14px;
  line-height: 1.45;
  color: var(--p-text-muted-color);
}
</style>
