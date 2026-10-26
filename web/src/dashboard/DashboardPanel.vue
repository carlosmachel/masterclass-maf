<script setup lang="ts">
import { computed } from 'vue'
import ExecutedActions from './ExecutedActions.vue'
import AlertCard from './AlertCard.vue'
import FilterBar from './FilterBar.vue'
import ChartCard from './ChartCard.vue'
import KpiTile from './KpiTile.vue'
import ProgressPanel from './ProgressPanel.vue'
import { KPI_ORDER, type DashboardState } from './types'

const props = defineProps<{
  state: DashboardState
  working: boolean
  names: Record<number, string>
}>()
defineEmits<{
  cite: [document: string, alertTitle: string]
  removeStore: [storeId: number, store: string]
  restoreStore: [storeId: number]
  region: [region: string | null]
}>()

// Filter applied right away to what's already on the dashboard (removed store, region). Alerts and temperature
// charts carry storeId and region resolved on the server, so the dashboard is right even before
// (or without) the agent's reanalysis; KPIs and sales are redone by the data tools.
const excluded = computed(() => new Set(props.state.filters.excludedStoreIds))
const visible = (storeId?: number | null, region?: string | null) =>
  (storeId == null || !excluded.value.has(storeId)) &&
  (!props.state.filters.region || !region || region === props.state.filters.region)
const alerts = computed(() => props.state.alerts.filter((a) => visible(a.storeId, a.region)))

const kpis = computed(() =>
  KPI_ORDER.flatMap((key) => (props.state.kpis[key] ? [{ key, kpi: props.state.kpis[key]! }] : [])),
)
// Temperature first (the main story), then sales.
const charts = computed(() =>
  Object.values(props.state.charts)
    .filter((c) => visible(c.storeId, c.region))
    // With no points (e.g. no sales change above 15%), the chart would come out empty with a broken axis.
    .filter((c) => c.series.some((s) => s.points.length > 0))
    .sort((a, b) => (a.type === b.type ? a.id.localeCompare(b.id) : a.type === 'line' ? -1 : 1)),
)
const empty = computed(
  () =>
    kpis.value.length === 0 &&
    charts.value.length === 0 &&
    alerts.value.length === 0 &&
    props.state.actions.length === 0,
)
</script>

<template>
  <section class="dashboard">
    <FilterBar
      :filters="state.filters"
      :names="names"
      :locked="working"
      @region="(r) => $emit('region', r)"
      @restore="(id) => $emit('restoreStore', id)"
    />
    <ProgressPanel :progress="state.progress" :working="working" />
    <div v-if="empty" class="dashboard__empty">
      <div class="dashboard__ghosts" aria-hidden="true">
        <span v-for="i in 3" :key="i" :class="{ 'dashboard__ghost--pulsing': working }" />
      </div>
      <p>
        {{
          working
            ? 'Montando o painel…'
            : 'Indicadores, alertas e gráficos aparecem aqui conforme o agente analisa.'
        }}
      </p>
    </div>

    <template v-else>
      <h2 v-if="kpis.length" class="dashboard__section">Indicadores</h2>
      <TransitionGroup name="appear" tag="div" class="dashboard__kpis">
        <KpiTile v-for="{ key, kpi } in kpis" :key="key" :kpi="kpi" />
      </TransitionGroup>
    </template>

    <ExecutedActions :actions="state.actions" :names="names" />

    <h2 v-if="alerts.length" class="dashboard__section">Alertas</h2>
    <TransitionGroup name="appear" tag="div" class="dashboard__alerts">
      <AlertCard
        v-for="alert in alerts"
        :key="alert.id"
        :alert="alert"
        :locked="working"
        @cite="(doc) => $emit('cite', doc, alert.title)"
        @remove-store="$emit('removeStore', alert.storeId, alert.store)"
      />
    </TransitionGroup>

    <h2 v-if="charts.length" class="dashboard__section">Tendências</h2>
    <TransitionGroup name="appear" tag="div" class="dashboard__charts">
      <ChartCard v-for="chart in charts" :key="chart.id" :chart="chart" />
    </TransitionGroup>
  </section>
</template>

<style scoped>
.dashboard {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.dashboard__section {
  margin: 10px 0 -4px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}

/* Empty: the skeleton of the indicators, instead of a dashed box the size of the screen. */
.dashboard__empty {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 24px;
  color: var(--p-text-muted-color);
}

.dashboard__ghosts {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
}

.dashboard__ghosts span {
  height: 112px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  opacity: 0.6;
}

.dashboard__ghost--pulsing {
  animation: pulse 1.4s ease-in-out infinite;
}

@keyframes pulse {
  50% {
    opacity: 0.25;
  }
}

.dashboard__empty p {
  margin: 0;
  font-size: 15px;
}

.dashboard__kpis {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
}

.dashboard__alerts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(300px, 100%), 1fr));
  gap: 12px;
}

.dashboard__charts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

/* No hole in the grid: a chart left alone on the last row takes the full width. */
.dashboard__charts > :last-child:nth-child(odd) {
  grid-column: 1 / -1;
}

/* "Builds itself": each card comes in with a fade + short slide. */
.appear-enter-active {
  transition:
    opacity 0.45s ease,
    transform 0.45s ease;
}

.appear-enter-from {
  opacity: 0;
  transform: translateY(12px);
}

.appear-leave-active {
  transition: opacity 0.25s ease;
}

.appear-leave-to {
  opacity: 0;
}
</style>
