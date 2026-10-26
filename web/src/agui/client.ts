import type { AguiEvent, RunAgentInput } from './types'

// An AG-UI run is just this: POST with RunAgentInput as JSON, response as Server-Sent Events,
// one JSON event per "data:" block. Lines starting with ":" are keepalives and are ignored.
export async function* runAgent(
  url: string,
  input: RunAgentInput,
  signal?: AbortSignal,
): AsyncGenerator<AguiEvent> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok || !response.body) {
    throw new Error(`AG-UI: HTTP ${response.status} ${response.statusText}`)
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  while (true) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += value.replace(/\r\n/g, '\n')

    let end: number
    while ((end = buffer.indexOf('\n\n')) >= 0) {
      const block = buffer.slice(0, end)
      buffer = buffer.slice(end + 2)
      const data = block
        .split('\n')
        .filter((line) => line.startsWith('data:'))
        .map((line) => line.slice(5).trimStart())
        .join('\n')
      if (data) yield JSON.parse(data) as AguiEvent
    }
  }
}
