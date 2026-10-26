// Shared dashboard state (AG-UI state). Mirrors src/Ipe.Agent/Dashboard/DashboardState.cs.
// kpis and charts are keyed by name: each server tool updates only its own part via JSON Patch.

export type Severity = 'ok' | 'warning' | 'critical'

export type Kpi = {
  label: string
  value: string
  detail?: string | null
  trend?: 'up' | 'down' | 'flat' | null
  severity: Severity
}

export type Chart = {
  id: string
  type: 'line' | 'bar'
  title: string
  series: { name: string; points: { x: string; y: number }[] }[]
  band?: { min: number; max: number } | null
  threshold?: number | null
  storeId?: number | null
  region?: string | null
}

export type Alert = {
  id: string
  storeId: number
  store: string
  region?: string | null
  title: string
  severity: Exclude<Severity, 'ok'>
  summary: string
  citations: string[]
}

// An approved and executed action (result of OpenMaintenanceTicket / CreateEmergencyOrder).
export type Action = {
  actionId: number
  type: 'TICKET' | 'EMERGENCY_ORDER'
  storeId: number
  reference: string | null
  message: string
}

// Progress step: one per data tool call (key = toolCallId). The server opens it as
// "running" when the tool is called and closes it with "done" + a summary when it finishes.
export type Step = {
  label: string
  status: 'running' | 'done'
  detail?: string | null
  order: number
}

export type DashboardState = {
  filters: { region: string | null; excludedStoreIds: number[] }
  kpis: Partial<Record<'freezers' | 'sales' | 'stock', Kpi>>
  charts: Record<string, Chart>
  alerts: Alert[]
  actions: Action[]
  progress: Record<string, Step>
  report: string
  // Demo controls, read by the server from RunAgentInput.state (ModelSelector, SecurityState).
  model: 'foundry' | 'local'
  security: { promptShields: boolean }
}

export const initialState = (): DashboardState => ({
  filters: { region: null, excludedStoreIds: [] },
  kpis: {},
  charts: {},
  alerts: [],
  actions: [],
  progress: {},
  report: '',
  model: 'foundry',
  security: { promptShields: true },
})

export const REGIONS = ['Sudeste', 'Sul', 'Nordeste', 'Centro-Oeste', 'Norte'] as const

// Fixed KPI order on the dashboard, regardless of the order in which the tools finish.
export const KPI_ORDER = ['freezers', 'stock', 'sales'] as const
