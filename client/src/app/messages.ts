// Temporary English text for the app shell, navigation and dashboard.
// CRM-4 moves these strings to client/src/i18n/{en,ar}.json.
export const shellMessages = {
  appName: 'Customer Support CRM',
  mainNavigation: 'Main navigation',
  nav: {
    dashboard: 'Dashboard',
    tickets: 'Tickets',
    customers: 'Customers',
    knowledgeBase: 'Knowledge base',
    reports: 'Reports',
    users: 'Users',
  },
  comingSoon: 'This area is coming soon.',
  welcome: (name: string) => `Welcome, ${name}`,
  apiStatus: 'API status',
  apiLoading: 'loading',
  apiUnavailable: 'unavailable',
}

export type NavigationId = keyof typeof shellMessages.nav
