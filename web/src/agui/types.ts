// The subset of the AG-UI protocol the demo uses, hand-written from the spec
// (docs.ag-ui.com/concepts/events and /sdk/js/core/types). Fields in camelCase; `type` in SCREAMING_SNAKE.

export type Role = 'user' | 'assistant' | 'system' | 'developer' | 'tool'

export type ToolCall = {
  id: string
  type: 'function'
  function: { name: string; arguments: string }
}

export type Message =
  | { id: string; role: 'user' | 'system' | 'developer'; content: string }
  | { id: string; role: 'assistant'; content?: string; toolCalls?: ToolCall[] }
  | { id: string; role: 'tool'; content: string; toolCallId: string; error?: string }

export type ResumeEntry = {
  interruptId: string
  status: 'resolved' | 'cancelled'
  payload?: unknown
}

export type RunAgentInput = {
  threadId: string
  runId: string
  parentRunId?: string
  state?: unknown
  messages: Message[]
  tools: { name: string; description: string; parameters: unknown }[]
  context: { description: string; value: string }[]
  forwardedProps: unknown
  resume?: ResumeEntry[]
}

// Approval interrupt as MAF .NET 1.24 actually emits it:
// reason "tool_call", toolCallId = the TOOL_CALL_START id, responseSchema { approved: boolean }.
export type Interrupt = {
  id: string
  reason: string
  message?: string
  toolCallId?: string
  responseSchema?: unknown
  metadata?: Record<string, unknown>
}

// Payload AGUI.Server recognizes as a tool approval. Without `toolCall`, the resume becomes a generic
// InterruptResponseContent, MAF doesn't run the tool and the model gets a tool call with no response.
export type ApprovalResumePayload = {
  approved: boolean
  toolCall: { callId: string; name: string; arguments: Record<string, unknown> }
}

export type JsonPatchOp =
  | { op: 'add' | 'replace' | 'test'; path: string; value: unknown }
  | { op: 'remove'; path: string }
  | { op: 'move' | 'copy'; from: string; path: string }

export type AguiEvent =
  | { type: 'RUN_STARTED'; threadId: string; runId: string; parentRunId?: string }
  | {
      type: 'RUN_FINISHED'
      threadId?: string
      runId?: string
      outcome?: { type: 'success' } | { type: 'interrupt'; interrupts: Interrupt[] }
      result?: unknown
    }
  | { type: 'RUN_ERROR'; message: string; code?: string }
  | { type: 'STEP_STARTED' | 'STEP_FINISHED'; stepName: string }
  | { type: 'TEXT_MESSAGE_START'; messageId: string; role: Role }
  | { type: 'TEXT_MESSAGE_CONTENT'; messageId: string; delta: string }
  | { type: 'TEXT_MESSAGE_END'; messageId: string }
  | { type: 'TEXT_MESSAGE_CHUNK'; messageId?: string; role?: Role; delta?: string }
  | { type: 'TOOL_CALL_START'; toolCallId: string; toolCallName: string; parentMessageId?: string }
  | { type: 'TOOL_CALL_ARGS'; toolCallId: string; delta: string }
  | { type: 'TOOL_CALL_END'; toolCallId: string }
  | {
      type: 'TOOL_CALL_RESULT'
      messageId: string
      toolCallId: string
      content: string
      role?: 'tool'
    }
  | { type: 'STATE_SNAPSHOT'; snapshot: unknown }
  | { type: 'STATE_DELTA'; delta: JsonPatchOp[] }
  | { type: 'MESSAGES_SNAPSHOT'; messages: Message[] }
  | { type: 'CUSTOM'; name: string; value: unknown }
  | { type: 'RAW'; event: unknown; source?: string }
  // Events the demo doesn't handle (reasoning, activity, subagent…) land here and are ignored.
  | { type: string; [field: string]: unknown }
