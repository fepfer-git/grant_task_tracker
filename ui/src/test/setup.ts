// Runs before every UI test file (vite.config.ts `setupFiles`).
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Each test starts with an empty page. Vitest globals are off, so Testing Library can't register this itself.
afterEach(() => {
  cleanup()
})
