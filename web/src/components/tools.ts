// How each agent tool shows up in the chat: a Portuguese name (on-screen text), an icon and the argument
// that identifies the call.

type ToolLabel = { name: string; icon: string }

const LABELS: Record<string, ToolLabel> = {
  GetFreezerStatus: { name: 'Status dos freezers', icon: 'pi pi-box' },
  GetTemperatureHistory: { name: 'Histórico de temperatura', icon: 'pi pi-chart-line' },
  GetWeeklySales: { name: 'Vendas da semana', icon: 'pi pi-chart-bar' },
  GetCriticalStock: { name: 'Estoque crítico', icon: 'pi pi-exclamation-triangle' },
  ListMaintenanceTickets: { name: 'Chamados de manutenção', icon: 'pi pi-wrench' },
  SearchKnowledgeBase: { name: 'Base de conhecimento', icon: 'pi pi-book' },
  OpenMaintenanceTicket: { name: 'Abrir chamado', icon: 'pi pi-ticket' },
  CreateEmergencyOrder: { name: 'Pedido emergencial', icon: 'pi pi-truck' },
  PublishAlerts: { name: 'Publicar alertas no painel', icon: 'pi pi-bell' },
  RequestDcProposal: { name: 'Agente do CD · A2A', icon: 'pi pi-sitemap' },
  ExecuteContingencyPlan: { name: 'Plano de contingência', icon: 'pi pi-shield' },
  todos_add: { name: 'Plano: adicionar etapas', icon: 'pi pi-list-check' },
  todos_complete: { name: 'Plano: concluir etapas', icon: 'pi pi-list-check' },
}

export function toolLabel(toolName: string): ToolLabel {
  return LABELS[toolName] ?? { name: toolName, icon: 'pi pi-cog' }
}

// The most informative argument to show next to the name (e.g. "FRZ-07-2", "loja 4 · Bebidas").
export function argsSummary(argsJson: string): string {
  let args: Record<string, unknown>
  try {
    args = argsJson ? JSON.parse(argsJson) : {}
  } catch {
    return '' // still arriving (partial TOOL_CALL_ARGS)
  }
  const parts: string[] = []
  if (args.freezerId) parts.push(String(args.freezerId))
  if (args.storeId) parts.push(`loja ${args.storeId}`)
  if (args.category) parts.push(String(args.category))
  if (args.region) parts.push(String(args.region))
  if (Array.isArray(args.excludedStoreIds) && args.excludedStoreIds.length > 0) {
    parts.push(`sem loja ${args.excludedStoreIds.join(', ')}`)
  }
  if (args.priority) parts.push(String(args.priority))
  for (const text of [args.question, args.request]) {
    if (typeof text === 'string') parts.push(`“${text.length > 48 ? text.slice(0, 47) + '…' : text}”`)
  }
  return parts.join(' · ')
}

export function duration(start: number, end?: number): string {
  if (end === undefined) return ''
  const s = (end - start) / 1000
  return s < 1 ? `${Math.round(s * 1000)} ms` : `${s.toFixed(1).replace('.', ',')} s`
}

// Readable JSON for the debug panel; text that isn't JSON comes back as it was.
export function prettyJson(text: string | undefined): string {
  if (!text) return ''
  try {
    return JSON.stringify(JSON.parse(text), null, 2)
  } catch {
    return text
  }
}

export const REJECTED = 'Tool call invocation rejected.'

// Documents Prompt Shields quarantined in a SearchKnowledgeBase result
// (src/Ipe.Agent/Security/DocumentShield.cs marks the passage with "blocked": true).
export function blockedDocuments(result: string | undefined): string[] {
  if (!result) return []
  try {
    const passages: unknown = JSON.parse(result)
    if (!Array.isArray(passages)) return []
    return [
      ...new Set(
        passages
          .filter((p): p is { document: string; blocked: true } => p?.blocked === true)
          .map((p) => p.document),
      ),
    ]
  } catch {
    return []
  }
}
