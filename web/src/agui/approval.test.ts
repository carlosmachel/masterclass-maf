import { describe, expect, it } from 'vitest'
import { approvalResume } from './approval'

// Real format captured from MAF .NET 1.24.
const interrupt = {
  id: 'ficc_call_abc',
  reason: 'tool_call',
  message: 'Approval required for tool call: OpenMaintenanceTicket',
  toolCallId: 'call_abc',
}

describe('approvalResume', () => {
  it('includes toolCall with callId, name and arguments', () => {
    const resume = approvalResume(
      interrupt,
      { name: 'OpenMaintenanceTicket', args: '{"freezerId":"FRZ-07-2","priority":"ALTA"}' },
      true,
    )
    expect(resume).toEqual({
      interruptId: 'ficc_call_abc',
      status: 'resolved',
      payload: {
        approved: true,
        toolCall: {
          callId: 'call_abc',
          name: 'OpenMaintenanceTicket',
          arguments: { freezerId: 'FRZ-07-2', priority: 'ALTA' },
        },
      },
    })
  })

  it('rejection uses the same format with approved false', () => {
    const resume = approvalResume(interrupt, { name: 'OpenMaintenanceTicket', args: '{}' }, false)
    expect((resume.payload as { approved: boolean }).approved).toBe(false)
  })

  it('fails loudly if the tool call never arrived', () => {
    expect(() => approvalResume(interrupt, undefined, true)).toThrow(/no matching tool call/)
  })
})
