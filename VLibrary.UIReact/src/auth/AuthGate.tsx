import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useAuth } from 'react-oidc-context';
import { onForbidden } from './authEvents';

interface AuthGateProps {
  children: ReactNode;
}

export function AuthGate({ children }: AuthGateProps) {
  const auth = useAuth();
  const [forbidden, setForbidden] = useState(false);
  const redirectStarted = useRef(false);

  useEffect(() => onForbidden(() => setForbidden(true)), []);

  useEffect(() => {
    if (auth.isLoading || auth.isAuthenticated || auth.activeNavigator || auth.error || redirectStarted.current) {
      return;
    }
    redirectStarted.current = true;
    // The current path travels through Keycloak as login state and is restored after the callback.
    void auth.signinRedirect({ state: window.location.pathname + window.location.search });
  }, [auth]);

  if (auth.error) {
    return (
      <div className="auth-screen">
        <p>Sign-in failed: {auth.error.message}</p>
        <button type="button" onClick={() => void auth.signinRedirect()}>
          Try again
        </button>
      </div>
    );
  }

  if (!auth.isAuthenticated) {
    return <div className="auth-screen">Redirecting to sign-in…</div>;
  }

  if (forbidden) {
    return (
      <div className="auth-screen">
        <p>Your user has no access to VLibrary. Please contact the administrator.</p>
        <button type="button" onClick={() => void auth.signoutRedirect()}>
          Sign out
        </button>
      </div>
    );
  }

  return <>{children}</>;
}
