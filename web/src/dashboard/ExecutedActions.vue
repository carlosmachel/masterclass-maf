<script setup lang="ts">
import type { Action } from './types'

defineProps<{ actions: Action[]; names: Record<number, string> }>()
</script>

<template>
  <section v-if="actions.length" class="actions">
    <h3 class="actions__title"><i class="pi pi-check-circle" /> Ações executadas</h3>
    <TransitionGroup name="appear" tag="ul" class="actions__list">
      <li v-for="action in actions" :key="action.actionId" class="action">
        <i :class="action.type === 'TICKET' ? 'pi pi-ticket' : 'pi pi-truck'" />
        <span class="action__number">#{{ action.actionId }}</span>
        <span class="action__text">{{ action.message }}</span>
        <span class="action__store">{{ names[action.storeId] ?? `loja ${action.storeId}` }}</span>
      </li>
    </TransitionGroup>
  </section>
</template>

<style scoped>
.actions {
  padding: 14px 16px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.actions__title {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 0 8px;
  font-size: 16px;
  font-weight: 600;
}

.actions__title i {
  color: var(--p-green-400);
}

.actions__list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.action {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 15px;
}

.action i {
  color: var(--p-text-muted-color);
}

.action__number {
  font-weight: 650;
  font-variant-numeric: tabular-nums;
}

.action__store {
  margin-left: auto;
  font-size: 13px;
  color: var(--p-text-muted-color);
}

.appear-enter-active {
  transition:
    opacity 0.45s ease,
    transform 0.45s ease;
}

.appear-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
</style>
