import { useEffect, useState } from 'react'
import { getHealth } from './api/health'
import { useIsAuthenticated } from './auth/useIsAuthenticated'
import { ApiErrorToaster } from './components/ApiErrorToaster'
import { CurrentUserPanel } from './features/auth/CurrentUserPanel'
import { LoginForm } from './features/auth/LoginForm'

type ApiState = 'loading' | 'unavailable' | string

function App() {
  const [apiStatus, setApiStatus] = useState<ApiState>('loading')
  const isAuthenticated = useIsAuthenticated()

  useEffect(() => {
    const controller = new AbortController()
    getHealth(controller.signal)
      .then((health) => setApiStatus(health.status))
      .catch(() => {
        if (!controller.signal.aborted) setApiStatus('unavailable')
      })
    return () => controller.abort()
  }, [])

  // Temporary placeholder text: i18n arrives in CRM-4, layout + routing in CRM-3.
  return (
    <>
      <main>
        <h1>Customer Support CRM</h1>
        <p>
          API status: <strong>{apiStatus}</strong>
        </p>
        {isAuthenticated ? <CurrentUserPanel /> : <LoginForm />}
      </main>
      <ApiErrorToaster />
    </>
  )
}

export default App
