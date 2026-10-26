import { onBeforeUnmount, onMounted, type Ref } from 'vue'

// Keeps the scroll at the bottom while the content grows (streaming text, animated cards),
// but only if the user was already near the bottom: whoever scrolled up to read isn't pulled back.
export function useStickToBottom(
  container: Ref<HTMLElement | null>,
  content: Ref<HTMLElement | null>,
) {
  const MARGIN = 120
  let nearBottom = true
  let observer: ResizeObserver | undefined

  const onScroll = () => {
    const el = container.value
    if (el) nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight < MARGIN
  }

  onMounted(() => {
    container.value?.addEventListener('scroll', onScroll, { passive: true })
    observer = new ResizeObserver(() => {
      const el = container.value
      if (el && nearBottom) el.scrollTop = el.scrollHeight
    })
    if (content.value) observer.observe(content.value)
  })

  onBeforeUnmount(() => {
    container.value?.removeEventListener('scroll', onScroll)
    observer?.disconnect()
  })
}
