import { useEffect, useState } from 'react'
import { getHealth } from './api/health'

type ApiState = 'loading' | 'unavailable' | string

function App() {
  const [apiStatus, setApiStatus] = useState<ApiState>('loading')

  useEffect(() => {
    const controller = new AbortController()
    getHealth(controller.signal)
      .then((health) => setApiStatus(health.status))
      .catch(() => {
        if (!controller.signal.aborted) setApiStatus('unavailable')
      })
    return () => controller.abort()
  }, [])

  // Temporary placeholder text: i18n arrives in CRM-4, layout in CRM-3.
  return (
    <main>
      <h1>Customer Support CRM</h1>
      <p>
        API status: <strong>{apiStatus}</strong>
      </p>
    </main>
  )
}

export default App
