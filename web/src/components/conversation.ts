import type { ChatItem, ToolCallView } from '@/agui/useAgent'

// The conversation as the user reads it: the question, the agent's work (narrations + queries) in one block,
// the actions (which ask for approval) in plain sight and the final answer. Items come from useAgent in stream order.

export const ACTIONS = new Set(['OpenMaintenanceTicket', 'CreateEmergencyOrder', 'ExecuteContingencyPlan'])
// Outside the work block, in plain sight: the actions and the conversation with the DC agent (A2A), the step 6 scene.
const IN_SIGHT = new Set([...ACTIONS, 'RequestDcProposal'])

export type WorkStep = { type: 'narration'; id: string; text: string } | { type: 'tool'; id: string }

export type Block =
  | { type: 'user'; id: string; text: string }
  | { type: 'answer'; id: string; text: string }
  | { type: 'narration'; id: string; text: string }
  | { type: 'event'; id: string; text: string }
  | { type: 'work'; id: string; steps: WorkStep[] }
  | { type: 'action'; id: string }

export function groupConversation(
  items: ChatItem[],
  toolCalls: Record<string, ToolCallView>,
): Block[] {
  const blocks: Block[] = []
  let work: WorkStep[] = []

  // A narration without any query (e.g. "vou abrir os chamados" before the actions) stays as loose text.
  function close() {
    if (work.some((s) => s.type === 'tool')) blocks.push({ type: 'work', id: work[0]!.id, steps: work })
    else for (const s of work) if (s.type === 'narration') blocks.push({ ...s })
    work = []
  }

  items.forEach((item, i) => {
    if (item.kind === 'event') {
      close()
      blocks.push({ type: 'event', id: item.id, text: item.text })
    } else if (item.kind === 'text' && item.role === 'user') {
      close()
      blocks.push({ type: 'user', id: item.id, text: item.text })
    } else if (item.kind === 'text') {
      if (!item.text.trim()) return
      // Text followed by a tool is a narration of the work; the turn's last text is the answer.
      if (items[i + 1]?.kind === 'tool') work.push({ type: 'narration', id: item.id, text: item.text })
      else {
        close()
        blocks.push({ type: 'answer', id: item.id, text: item.text })
      }
    } else if (IN_SIGHT.has(toolCalls[item.id]?.name ?? '')) {
      close()
      blocks.push({ type: 'action', id: item.id })
    } else {
      work.push({ type: 'tool', id: item.id })
    }
  })
  close()
  return blocks
}

export type WorkSummary = {
  queries: number
  seconds: number // from the start of the first query to the end of the last one
  inProgress?: string // name of the tool running now
  blocked: string[] // documents quarantined by Prompt Shields
}

export function summarizeWork(
  steps: WorkStep[],
  toolCalls: Record<string, ToolCallView>,
  blockedIn: (result?: string) => string[],
): WorkSummary {
  const calls = steps.flatMap((s) => (s.type === 'tool' && toolCalls[s.id] ? [toolCalls[s.id]!] : []))
  const start = Math.min(...calls.map((c) => c.startedAt))
  const end = Math.max(...calls.map((c) => c.endedAt ?? c.startedAt))
  const running = calls.filter((c) => c.status !== 'done')
  return {
    queries: calls.length,
    seconds: calls.length ? Math.max(0, Math.round((end - start) / 1000)) : 0,
    inProgress: running[running.length - 1]?.name,
    blocked: [...new Set(calls.flatMap((c) => blockedIn(c.result)))],
  }
}
