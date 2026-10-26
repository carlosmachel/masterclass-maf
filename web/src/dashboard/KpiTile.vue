<script setup lang="ts">
import type { Kpi } from './types'

defineProps<{ kpi: Kpi }>()

const icons = { up: 'pi pi-arrow-up-right', down: 'pi pi-arrow-down-right', flat: 'pi pi-minus' }
</script>

<template>
  <article class="kpi" :class="`kpi--${kpi.severity}`">
    <span class="kpi__label">
      <i class="kpi__dot" />
      {{ kpi.label }}
    </span>
    <span class="kpi__value">
      {{ kpi.value }}
      <i v-if="kpi.trend" :class="icons[kpi.trend]" class="kpi__trend" />
    </span>
    <span v-if="kpi.detail" class="kpi__detail">{{ kpi.detail }}</span>
  </article>
</template>

<style scoped>
/* Color only for severity: a dot next to the label; the number stays neutral. */
.kpi {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 16px 18px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.kpi--ok {
  --cor: var(--p-surface-500);
}

.kpi--warning {
  --cor: var(--p-amber-400);
}

.kpi--critical {
  --cor: var(--p-red-400);
}

.kpi__label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--p-text-muted-color);
}

.kpi__dot {
  width: 8px;
  height: 8px;
  background: var(--cor);
  border-radius: 50%;
}

.kpi__value {
  display: flex;
  align-items: baseline;
  gap: 8px;
  font-family: var(--font-display);
  font-size: 38px;
  font-weight: 800;
  letter-spacing: -0.02em;
  line-height: 1.1;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.kpi__trend {
  font-size: 18px;
  color: var(--p-text-muted-color);
}

.kpi__detail {
  overflow: hidden;
  font-size: 14px;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--p-text-muted-color);
}
</style>
