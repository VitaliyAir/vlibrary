import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthProvider } from 'react-oidc-context';
import type { User } from 'oidc-client-ts';
import { BrowserRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App } from './App';
import { AuthGate } from './auth/AuthGate';
import { userManager } from './auth/oidcConfig';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 10_000, retry: 1, refetchOnWindowFocus: false },
  },
});

// After the return from Keycloak, drop ?code=...&state=... and go back to the page the user came from.
const restoreLocation = (user: User | void) => {
  const returnTo = typeof user?.state === 'string' && user.state.startsWith('/') ? user.state : '/';
  window.history.replaceState({}, document.title, returnTo);
};

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider userManager={userManager} onSigninCallback={restoreLocation}>
      <AuthGate>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </QueryClientProvider>
      </AuthGate>
    </AuthProvider>
  </StrictMode>,
);
