<script setup lang="ts">
import { computed } from 'vue'
import Select from 'primevue/select'
import { REGIONS, type DashboardState } from './types'

const props = defineProps<{
  filters: DashboardState['filters']
  names: Record<number, string>
  locked: boolean
}>()
const emit = defineEmits<{ region: [region: string | null]; restore: [storeId: number] }>()

const options = [
  { label: 'Todas as regiões', value: null },
  ...REGIONS.map((r) => ({ label: r, value: r })),
]
const region = computed({
  get: () => props.filters.region,
  set: (v: string | null) => emit('region', v),
})
</script>

<template>
  <div class="filters">
    <i class="pi pi-filter filters__icon" />
    <Select
      v-model="region"
      :options="options"
      option-label="label"
      option-value="value"
      placeholder="Todas as regiões"
      :disabled="locked"
      size="small"
      class="filters__region"
    />
    <span v-for="id in filters.excludedStoreIds" :key="id" class="filters__excluded">
      <i class="pi pi-eye-slash" />
      {{ names[id] ?? `Loja ${id}` }}
      <button
        type="button"
        :disabled="locked"
        title="Voltar a loja para o painel"
        @click="emit('restore', id)"
      >
        <i class="pi pi-times" />
      </button>
    </span>
  </div>
</template>

<style scoped>
.filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.filters__icon {
  color: var(--p-text-muted-color);
}

.filters__region {
  min-width: 190px;
}

.filters__excluded {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 4px 2px 10px;
  font-size: 14px;
  color: var(--p-text-muted-color);
  border: 1px dashed var(--p-content-border-color);
  border-radius: 0;
}

.filters__excluded button {
  display: inline-flex;
  padding: 4px;
  color: inherit;
  background: transparent;
  border: 0;
  border-radius: 0;
  cursor: pointer;
}

.filters__excluded button:hover:not(:disabled) {
  color: var(--p-text-color);
  background: var(--p-content-hover-background);
}
</style>
