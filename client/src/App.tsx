import { QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { BrowserRouter } from 'react-router'
import { AppRoutes } from '@/app/AppRoutes'
import { createQueryClient } from '@/app/query-client'
import { getAccessToken, subscribeToSession } from '@/auth/session'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'

function App() {
  const [queryClient] = useState(createQueryClient)

  // Signed out (button, expired or rejected token): forget every cached server response,
  // so the next user never sees the previous user's data.
  useEffect(
    () =>
      subscribeToSession(() => {
        if (getAccessToken() === null) queryClient.clear()
      }),
    [queryClient],
  )

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
      <ApiErrorToaster />
    </QueryClientProvider>
  )
}

export default App
