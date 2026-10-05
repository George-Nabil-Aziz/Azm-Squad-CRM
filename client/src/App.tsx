import { QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BrowserRouter } from 'react-router'
import { AppRoutes } from '@/app/AppRoutes'
import { createQueryClient } from '@/app/query-client'
import { getAccessToken, subscribeToSession } from '@/auth/session'
import { ApiErrorToaster } from '@/components/ApiErrorToaster'
import { DirectionProvider } from '@/components/ui/direction'

function App() {
  const [queryClient] = useState(createQueryClient)
  // Re-renders on every language change; Radix primitives (menus, sheets, …) read the direction from here.
  const { i18n } = useTranslation()

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
    <DirectionProvider dir={i18n.dir()}>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
        <ApiErrorToaster />
      </QueryClientProvider>
    </DirectionProvider>
  )
}

export default App
