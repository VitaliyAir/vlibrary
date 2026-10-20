// The API client reports a 403 (the user has no Owner role for this app); AuthGate shows the "no access" screen.
const forbiddenListeners = new Set<() => void>();

export function onForbidden(listener: () => void): () => void {
  forbiddenListeners.add(listener);
  return () => {
    forbiddenListeners.delete(listener);
  };
}

export function notifyForbidden(): void {
  forbiddenListeners.forEach((listener) => listener());
}
