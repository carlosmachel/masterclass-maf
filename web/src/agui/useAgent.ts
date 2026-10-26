import { ref, shallowRef } from 'vue'
import { runAgent } from './client'
import { applyPatch } from './jsonPatch'
import { approvalResume } from './approval'
import type { AguiEvent, Interrupt, Message, ResumeEntry } from './types'

export type ToolCallView = {
  id: string
  name: string
  args: string // accumulated JSON from TOOL_CALL_ARGS
  result?: string
  status: 'streaming' | 'running' | 'done'
  startedAt: number // Date.now() at TOOL_CALL_START
  endedAt?: number // Date.now() at TOOL_CALL_RESULT
}

export type ChatItem =
  | { kind: 'text'; id: string; role: 'user' | 'assistant'; text: string }
  | { kind: 'tool'; id: string }
  | { kind: 'event'; id: string; text: string }

// Screen state of an AG-UI conversation. The session lives on the server (session store per threadId),
// so each run sends only the new messages + parentRunId, as the MAF docs say.
export function useAgent<S>(url: string, initialState: S) {
  const items = ref<ChatItem[]>([])
  const toolCalls = ref<Record<string, ToolCallView>>({})
  const state = shallowRef<S>(initialState) // the UI writes here too (filters): it goes in the next run
  const interrupts = ref<Interrupt[]>([])
  const running = ref(false)
  const error = ref<string | null>(null)

  let threadId: string | undefined
  let lastRunId: string | undefined
  // Route of the last run: an approval resume goes back to the same endpoint (agent or on-call workflow).
  let currentRoute = url

  function textItem(messageId: string) {
    return items.value.find((x) => x.kind === 'text' && x.id === messageId) as
      | Extract<ChatItem, { kind: 'text' }>
      | undefined
  }

  function apply(e: AguiEvent) {
    switch (e.type) {
      case 'RUN_STARTED':
        lastRunId = (e as { runId: string }).runId
        break
      case 'TEXT_MESSAGE_START':
        items.value.push({
          kind: 'text',
          id: (e as { messageId: string }).messageId,
          role: 'assistant',
          text: '',
        })
        break
      case 'TEXT_MESSAGE_CONTENT': {
        const { messageId, delta } = e as { messageId: string; delta: string }
        const item = textItem(messageId)
        if (item) item.text += delta
        break
      }
      case 'TOOL_CALL_START': {
        const { toolCallId, toolCallName } = e as { toolCallId: string; toolCallName: string }
        // On an approval resume the server re-sends the TOOL_CALL_START of the same call:
        // update the existing chip instead of creating another one.
        const existing = toolCalls.value[toolCallId]
        toolCalls.value[toolCallId] = {
          id: toolCallId,
          name: toolCallName,
          args: '',
          status: 'streaming',
          startedAt: existing?.startedAt ?? Date.now(),
        }
        if (!existing) items.value.push({ kind: 'tool', id: toolCallId })
        break
      }
      case 'TOOL_CALL_ARGS': {
        const { toolCallId, delta } = e as { toolCallId: string; delta: string }
        const call = toolCalls.value[toolCallId]
        if (call) call.args += delta
        break
      }
      case 'TOOL_CALL_END': {
        const call = toolCalls.value[(e as { toolCallId: string }).toolCallId]
        if (call) call.status = 'running'
        break
      }
      case 'TOOL_CALL_RESULT': {
        const { toolCallId, content } = e as { toolCallId: string; content: string }
        const call = toolCalls.value[toolCallId]
        if (call) Object.assign(call, { result: content, status: 'done', endedAt: Date.now() })
        break
      }
      case 'STATE_SNAPSHOT':
        state.value = (e as { snapshot: S }).snapshot
        break
      case 'STATE_DELTA':
        // A patch that doesn't match the local state can't break the run: log it and move on.
        try {
          state.value = applyPatch(
            state.value,
            (e as Extract<AguiEvent, { type: 'STATE_DELTA' }>).delta,
          )
        } catch (err) {
          console.warn('STATE_DELTA ignored', err)
        }
        break
      case 'RUN_FINISHED': {
        const outcome = (e as Extract<AguiEvent, { type: 'RUN_FINISHED' }>).outcome
        interrupts.value = outcome?.type === 'interrupt' ? outcome.interrupts : []
        break
      }
      case 'RUN_ERROR':
        error.value = (e as { message: string }).message
        break
    }
  }

  async function run(messages: Message[], resume?: ResumeEntry[], route = currentRoute) {
    threadId ??= crypto.randomUUID()
    currentRoute = route
    running.value = true
    error.value = null
    interrupts.value = []
    try {
      const events = runAgent(route, {
        threadId,
        runId: crypto.randomUUID(),
        parentRunId: lastRunId,
        state: state.value,
        messages,
        tools: [],
        context: [],
        forwardedProps: {},
        resume,
      })
      for await (const e of events) apply(e)
    } catch (err) {
      error.value = err instanceof Error ? err.message : String(err)
    } finally {
      running.value = false
      // A broken run can leave the server session with a tool call without a result, and reusing it
      // returns HTTP 400 ("tool_calls must be followed by tool messages"). The next question opens a new session;
      // the dashboard loses nothing, because the state goes from the front end on every run.
      if (error.value) threadId = lastRunId = undefined
    }
  }

  function send(text: string) {
    const msg: Message = { id: crypto.randomUUID(), role: 'user', content: text }
    items.value.push({ kind: 'text', id: msg.id, role: 'user', text })
    return run([msg], undefined, url)
  }

  // Something the user did in the UI (e.g. a dashboard filter). The agent gets the full description;
  // the chat shows only a discreet note, not a user message.
  // `route` sends the event to another AG-UI endpoint in the same conversation (e.g. the alarm goes to the on-call workflow).
  function sendEvent(forAgent: string, note: string, route = url) {
    const msg: Message = { id: crypto.randomUUID(), role: 'user', content: forAgent }
    items.value.push({ kind: 'event', id: msg.id, text: note })
    return run([msg], undefined, route)
  }

  // A run can stop with several approval requests; they're all answered in the same resume.
  function resolveInterrupts(decisions: { interrupt: Interrupt; approved: boolean }[]) {
    const resume = decisions.map(({ interrupt, approved }) =>
      approvalResume(interrupt, toolCalls.value[interrupt.toolCallId ?? ''], approved),
    )
    return run([], resume)
  }

  return {
    items,
    toolCalls,
    state,
    interrupts,
    running,
    error,
    send,
    sendEvent,
    run,
    resolveInterrupts,
  }
}
