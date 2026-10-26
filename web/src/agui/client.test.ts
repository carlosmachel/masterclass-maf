import http from 'node:http'
import type { AddressInfo } from 'node:net'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { runAgent } from './client'
import { applyPatch } from './jsonPatch'
import type { AguiEvent } from './types'

const ev = (o: unknown) => `data: ${JSON.stringify(o)}\n\n`

// Fake SSE server: keepalive, one event split across two writes, snapshot + delta and an interrupt.
const server = http.createServer((_req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/event-stream' })
  res.write(': keepalive\n\n')
  res.write(ev({ type: 'RUN_STARTED', threadId: 't', runId: 'r1' }))
  const snapshot = ev({
    type: 'STATE_SNAPSHOT',
    snapshot: { filters: { excludedStoreIds: [] }, alerts: [{ id: 'a4' }, { id: 'a13' }] },
  })
  res.write(snapshot.slice(0, 20))
  setTimeout(() => {
    res.write(snapshot.slice(20))
    res.write(
      ev({
        type: 'STATE_DELTA',
        delta: [
          { op: 'add', path: '/filters/excludedStoreIds/-', value: 4 },
          { op: 'remove', path: '/alerts/0' },
        ],
      }),
    )
    res.write(
      ev({
        type: 'RUN_FINISHED',
        outcome: { type: 'interrupt', interrupts: [{ id: 'i1', reason: 'tool_call' }] },
      }),
    )
    res.end()
  }, 20)
})

let url = ''
beforeAll(async () => {
  await new Promise<void>((ok) => server.listen(0, ok))
  url = `http://localhost:${(server.address() as AddressInfo).port}/`
})
afterAll(() => server.close())

describe('runAgent', () => {
  it('reads the SSE, ignores keepalives and reassembles a split event', async () => {
    const events: AguiEvent[] = []
    for await (const e of runAgent(url, {
      threadId: 't',
      runId: 'r1',
      messages: [],
      tools: [],
      context: [],
      forwardedProps: {},
    })) {
      events.push(e)
    }

    expect(events.map((e) => e.type)).toEqual([
      'RUN_STARTED',
      'STATE_SNAPSHOT',
      'STATE_DELTA',
      'RUN_FINISHED',
    ])

    const snapshot = (events[1] as { snapshot: unknown }).snapshot
    const state = applyPatch(
      snapshot,
      (events[2] as Extract<AguiEvent, { type: 'STATE_DELTA' }>).delta,
    )
    expect(state).toEqual({ filters: { excludedStoreIds: [4] }, alerts: [{ id: 'a13' }] })
  })
})

describe('applyPatch', () => {
  it('does not change the original document', () => {
    const original = { a: [1, 2] }
    applyPatch(original, [{ op: 'replace', path: '/a/0', value: 9 }])
    expect(original).toEqual({ a: [1, 2] })
  })

  it('move and copy', () => {
    const doc = { from: { x: 1 }, to: {} as Record<string, unknown> }
    expect(applyPatch(doc, [{ op: 'move', from: '/from/x', path: '/to/x' }])).toEqual({
      from: {},
      to: { x: 1 },
    })
    expect(applyPatch(doc, [{ op: 'copy', from: '/from/x', path: '/to/y' }])).toEqual({
      from: { x: 1 },
      to: { y: 1 },
    })
  })
})
