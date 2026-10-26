<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import type { ToolCallView } from '@/agui/useAgent'

// An approval request for an action (AG-UI interrupt), in the tray above the input until the decision.
// decision: undefined = pending; true/false = already decided, waiting for the run's other requests.
const props = defineProps<{ call: ToolCallView; storeName?: string; decision?: boolean }>()
defineEmits<{ decide: [approve: boolean] }>()

const args = computed<Record<string, unknown>>(() => {
  try {
    return props.call.args ? JSON.parse(props.call.args) : {}
  } catch {
    return {}
  }
})

// Three actions ask for approval: ticket and order (analyst) and the on-call contingency plan (both together).
const kind = computed(() =>
  props.call.name === 'OpenMaintenanceTicket'
    ? 'ticket'
    : props.call.name === 'ExecuteContingencyPlan'
      ? 'plan'
      : 'order',
)
const title = computed(
  () =>
    ({
      ticket: 'Abrir chamado de manutenção',
      order: 'Pedido emergencial de reposição',
      plan: 'Plano de contingência: chamado + pedido ao CD',
    })[kind.value],
)
const icon = computed(
  () => ({ ticket: 'pi pi-ticket', order: 'pi pi-truck', plan: 'pi pi-shield' })[kind.value],
)
const target = computed(() =>
  kind.value === 'order'
    ? [props.storeName ?? `Loja ${args.value.storeId}`, args.value.category].filter(Boolean).join(' · ')
    : [args.value.freezerId, props.storeName].filter(Boolean).join(' · '),
)
const priority = computed(() =>
  kind.value === 'ticket'
    ? String(args.value.priority ?? '').toUpperCase()
    : kind.value === 'plan'
      ? 'ALTA'
      : '',
)
const text = computed(() =>
  String(
    {
      ticket: args.value.description,
      order: args.value.justification,
      plan: `${args.value.ticketDescription ?? ''} CD: ${args.value.dcProposal ?? ''}`,
    }[kind.value] ?? '',
  ),
)
</script>

<template>
  <article class="approval" :class="{ 'approval--decided': decision !== undefined }">
    <div class="approval__body">
      <div class="approval__title">
        <i :class="icon" />
        <h3>{{ title }}</h3>
        <span
          v-if="priority"
          class="approval__priority"
          :class="`approval__priority--${priority}`"
        >
          {{ priority }}
        </span>
      </div>
      <p class="approval__target">{{ target }}</p>
      <p class="approval__text" :title="text">{{ text }}</p>
    </div>
    <footer v-if="decision === undefined" class="approval__buttons">
      <Button label="Aprovar" icon="pi pi-check" size="small" @click="$emit('decide', true)" />
      <Button
        label="Rejeitar"
        icon="pi pi-times"
        severity="secondary"
        size="small"
        text
        @click="$emit('decide', false)"
      />
    </footer>
    <footer v-else class="approval__decision">
      <i :class="decision ? 'pi pi-check' : 'pi pi-times'" />
      {{ decision ? 'Aprovado' : 'Rejeitado' }}
    </footer>
  </article>
</template>

<style scoped>
.approval {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 4px 14px;
  padding: 10px 10px 10px 14px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  animation: arrive 0.35s ease;
}

.approval--decided {
  opacity: 0.55;
}

@keyframes arrive {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
}

.approval__body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.approval__title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.approval__title i {
  font-size: 16px;
  color: var(--p-text-muted-color);
}

.approval__title h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.approval__priority {
  padding: 0 8px;
  font-size: 12px;
  font-weight: 700;
  border: 1px solid currentColor;
  border-radius: 0;
}

.approval__priority--ALTA {
  color: var(--p-red-400);
}

.approval__priority--MEDIA {
  color: var(--p-amber-400);
}

.approval__priority--BAIXA {
  color: var(--p-text-muted-color);
}

.approval__target {
  margin: 0;
  font-size: 14px;
  color: var(--p-text-muted-color);
}

.approval__text {
  display: -webkit-box;
  margin: 2px 0 0;
  overflow: hidden;
  font-size: 14px;
  line-height: 1.45;
  color: var(--p-text-muted-color);
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
}

.approval__decision {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-right: 6px;
  font-size: 14px;
  color: var(--p-text-muted-color);
}

.approval__buttons {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
</style>
