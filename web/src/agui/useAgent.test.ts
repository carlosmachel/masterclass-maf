import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AguiEvent, RunAgentInput } from './types'

// Each run returns the next event script (or throws, like a dropped connection).
const scripts: (AguiEvent[] | Error)[] = []
const inputs: RunAgentInput[] = []
vi.mock('./client', () => ({
  async *runAgent(_url: string, input: RunAgentInput) {
    inputs.push(input)
    const r = scripts.shift() ?? []
    if (r instanceof Error) throw r
    yield* r
  },
}))
const { useAgent } = await import('./useAgent')

const ok = (runId: string): AguiEvent[] => [
  { type: 'RUN_STARTED', threadId: 'x', runId } as AguiEvent,
  { type: 'RUN_FINISHED', threadId: 'x', runId } as AguiEvent,
]
const broken = (runId: string): AguiEvent[] => [
  { type: 'RUN_STARTED', threadId: 'x', runId } as AguiEvent,
  { type: 'RUN_ERROR', message: 'StreamingError' } as AguiEvent,
]

describe('useAgent: session after a failed run', () => {
  beforeEach(() => {
    scripts.length = 0
    inputs.length = 0
  })

  it('keeps the session across runs that finish well', async () => {
    scripts.push(ok('r1'), ok('r2'))
    const agent = useAgent('/api/agent', {})
    await agent.send('first')
    await agent.send('second')
    expect(inputs[1]!.threadId).toBe(inputs[0]!.threadId)
    expect(inputs[1]!.parentRunId).toBe('r1')
  })

  // The server session may have been left with a tool call without a result: reusing it returns HTTP 400
  // ("tool_calls must be followed by tool messages"). The next question opens a new session.
  it.each([
    ['RUN_ERROR', broken('r1')],
    ['an exception', new Error('AG-UI: HTTP 502')],
  ])('opens a new session after %s', async (_, first) => {
    scripts.push(first, ok('r2'))
    const agent = useAgent('/api/agent', { filters: 'kept' })
    await agent.send('first')
    expect(agent.error.value).toBeTruthy()
    await agent.send('second')
    expect(inputs[1]!.threadId).not.toBe(inputs[0]!.threadId)
    expect(inputs[1]!.parentRunId).toBeUndefined()
    expect(inputs[1]!.state).toEqual({ filters: 'kept' })
    expect(agent.error.value).toBeNull()
  })
})
