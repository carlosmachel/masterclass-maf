<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { Step } from './types'

// Moment 2: what the agent is doing right now. During the run the list stays open; when it ends it becomes
// a summary line that can be expanded.
const props = defineProps<{ progress: Record<string, Step>; working: boolean }>()

const steps = computed(() =>
  Object.entries(props.progress).sort(([, a], [, b]) => a.order - b.order),
)
const done = computed(() => steps.value.filter(([, s]) => s.status === 'done').length)
// Between one tool and the next the model is thinking (before the 1st tool, or writing alerts and the summary):
// without this line the screen looks stuck for 10–20 s.
const reasoning = computed(
  () => props.working && !steps.value.some(([, s]) => s.status === 'running'),
)
const title = computed(() =>
  props.working
    ? 'O agente está trabalhando' +
      (steps.value.length ? ` · ${done.value}/${steps.value.length} etapas` : '')
    : `Análise concluída · ${steps.value.length} etapas · ${elapsed.value} s`,
)

// Duration measured in the browser: from the start to the end of the agent's work.
const startedAt = ref(Date.now())
const elapsed = ref(0)

const open = ref(true)
watch(
  () => props.working,
  (now) => {
    open.value = now
    if (now) startedAt.value = Date.now()
    else elapsed.value = Math.max(1, Math.round((Date.now() - startedAt.value) / 1000))
  },
)
</script>

<template>
  <section
    v-if="steps.length || working"
    class="progress"
    :class="{ 'progress--finished': !working }"
  >
    <button type="button" class="progress__header" :aria-expanded="open" @click="open = !open">
      <i
        :class="working ? 'pi pi-spin pi-spinner' : 'pi pi-check-circle'"
        class="progress__icon"
      />
      <span>{{ title }}</span>
      <i class="pi progress__arrow" :class="open ? 'pi-chevron-up' : 'pi-chevron-down'" />
    </button>
    <TransitionGroup v-if="open" name="appear" tag="ol" class="progress__list">
      <li v-for="[id, step] in steps" :key="id" class="step" :class="`step--${step.status}`">
        <i
          :class="step.status === 'done' ? 'pi pi-check' : 'pi pi-spin pi-spinner'"
          class="step__icon"
        />
        <span class="step__label"
          >{{ step.label }}{{ step.status === 'running' ? '…' : '' }}</span
        >
        <span v-if="step.detail" class="step__detail">{{ step.detail }}</span>
      </li>
      <li v-if="reasoning" key="reasoning" class="step step--running step--reasoning">
        <i class="pi pi-sparkles step__icon" />
        <span class="step__label">Raciocinando…</span>
      </li>
    </TransitionGroup>
  </section>
</template>

<style scoped>
.progress {
  padding: 10px 14px;
  background: var(--p-content-background);
  border: 1px solid var(--p-primary-color);
  border-radius: 0;
  transition: border-color 0.3s;
}

.progress--finished {
  border-color: var(--p-content-border-color);
}

.progress__header {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 2px 0;
  font: inherit;
  font-size: 15px;
  font-weight: 600;
  color: var(--p-text-color);
  text-align: left;
  background: transparent;
  border: 0;
  cursor: pointer;
}

.progress__icon {
  color: var(--p-primary-color);
}

.progress--finished .progress__icon {
  color: var(--p-green-400);
}

.progress__arrow {
  margin-left: auto;
  font-size: 11px;
  color: var(--p-text-muted-color);
}

.progress__list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(320px, 100%), 1fr));
  gap: 4px 16px;
  margin: 8px 0 2px;
  padding: 0;
  list-style: none;
}

.step {
  display: flex;
  align-items: baseline;
  gap: 8px;
  min-width: 0;
  font-size: 14px;
}

.step__icon {
  font-size: 12px;
  color: var(--p-primary-color);
}

.step--done .step__icon {
  color: var(--p-green-400);
}

.step__label {
  white-space: nowrap;
}

.step--running .step__label {
  font-weight: 600;
}

.step--reasoning .step__label {
  font-weight: 400;
  color: var(--p-text-muted-color);
  animation: pulse 1.6s ease-in-out infinite;
}

@keyframes pulse {
  50% {
    opacity: 0.45;
  }
}

.step__detail {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--p-text-muted-color);
}

.appear-enter-active {
  transition:
    opacity 0.3s ease,
    transform 0.3s ease;
}

.appear-enter-from {
  opacity: 0;
  transform: translateX(-6px);
}
</style>
