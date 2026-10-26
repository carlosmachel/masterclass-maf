import type { ApprovalResumePayload, Interrupt, ResumeEntry } from './types'

type ToolCallData = { name: string; args: string }

// Builds the approval resume from the interrupt and the tool call that raised it
// (name and arguments arrived earlier, in the TOOL_CALL_START / TOOL_CALL_ARGS events).
export function approvalResume(
  interrupt: Interrupt,
  toolCall: ToolCallData | undefined,
  approved: boolean,
): ResumeEntry {
  if (!interrupt.toolCallId || !toolCall) {
    throw new Error(`Interrupt ${interrupt.id} has no matching tool call.`)
  }
  const payload: ApprovalResumePayload = {
    approved,
    toolCall: {
      callId: interrupt.toolCallId,
      name: toolCall.name,
      arguments: toolCall.args ? JSON.parse(toolCall.args) : {},
    },
  }
  return { interruptId: interrupt.id, status: 'resolved', payload }
}
