import { InMemoryWebStorage, UserManager, WebStorageStateStore } from 'oidc-client-ts';

// Tokens live only in tab memory; the in-flight login state (PKCE) is kept in localStorage (the library default),
// so the round trip to Keycloak works. After a page reload the app redirects to Keycloak and comes straight back
// while the SSO session is alive.
export const userManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID,
  redirect_uri: `${window.location.origin}/`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: 'code',
  scope: 'openid profile',
  automaticSilentRenew: true,
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
});
