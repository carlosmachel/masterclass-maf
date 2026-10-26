import type { JsonPatchOp } from './types'

// Minimal RFC 6902 to apply STATE_DELTA. Immutable: returns a new object.
export function applyPatch<T>(document: T, operations: JsonPatchOp[]): T {
  let root: unknown = structuredClone(document)
  for (const op of operations) {
    switch (op.op) {
      case 'add':
      case 'replace':
        root = setAt(root, op.path, structuredClone(op.value), op.op === 'add')
        break
      case 'remove':
        root = removeAt(root, op.path)
        break
      case 'copy':
        root = setAt(root, op.path, structuredClone(getAt(root, op.from)), true)
        break
      case 'move': {
        const value = getAt(root, op.from)
        root = setAt(removeAt(root, op.from), op.path, value, true)
        break
      }
      case 'test':
        if (JSON.stringify(getAt(root, op.path)) !== JSON.stringify(op.value)) {
          throw new Error(`JSON Patch: test failed at ${op.path}`)
        }
        break
    }
  }
  return root as T
}

function tokens(path: string): string[] {
  if (path === '') return []
  return path
    .slice(1)
    .split('/')
    .map((t) => t.replace(/~1/g, '/').replace(/~0/g, '~'))
}

function getAt(root: unknown, path: string): unknown {
  return tokens(path).reduce<unknown>((node, t) => (node as Record<string, unknown>)?.[t], root)
}

function parentOf(root: unknown, path: string): [Record<string, unknown> | unknown[], string] {
  const ts = tokens(path)
  const key = ts.pop()!
  const parent = ts.reduce<unknown>((node, t) => (node as Record<string, unknown>)[t], root)
  return [parent as Record<string, unknown> | unknown[], key]
}

function setAt(root: unknown, path: string, value: unknown, insert: boolean): unknown {
  if (path === '') return value
  const [parent, key] = parentOf(root, path)
  if (Array.isArray(parent)) {
    const i = key === '-' ? parent.length : Number(key)
    if (insert) parent.splice(i, 0, value)
    else parent[i] = value
  } else {
    parent[key] = value
  }
  return root
}

function removeAt(root: unknown, path: string): unknown {
  const [parent, key] = parentOf(root, path)
  if (Array.isArray(parent)) parent.splice(Number(key), 1)
  else delete parent[key]
  return root
}
