import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// The UI tests run west of UTC (DESIGN.md §9). A "YYYY-MM-DD" date turned into a JavaScript Date is read as
// midnight UTC, which is the previous day here, so that mistake fails the tests. Set before the test processes
// start, so they inherit it.
process.env.TZ = 'America/Los_Angeles'

/** The API's development address (the `http` profile in the API's launchSettings.json). */
const apiUrl = 'http://localhost:5038'

export default defineConfig({
  plugins: [react()],
  server: {
    // The dev server forwards API calls, so the browser sees one origin and the API needs no CORS setup.
    proxy: {
      '/grants': apiUrl,
      '/tasks': apiUrl,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    unstubGlobals: true,
    restoreMocks: true,
  },
})
