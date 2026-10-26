// Thin fetch wrapper: JSON in/out, ProblemDetails -> ApiError, Keycloak Bearer token on every request.

import { notifyForbidden } from '../auth/authEvents';
import { userManager } from '../auth/oidcConfig';

const baseUrl = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '');

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

type Query = Record<string, string | number | boolean | null | undefined>;

export function withQuery(path: string, query: Query): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value));
    }
  }
  const text = params.toString();
  return text ? `${path}?${text}` : path;
}

async function sendWithToken(url: string, init: RequestInit, forceRenew: boolean): Promise<Response> {
  const user = forceRenew ? await userManager.signinSilent() : await userManager.getUser();
  const headers = new Headers(init.headers);
  if (user?.access_token) {
    headers.set('Authorization', `Bearer ${user.access_token}`);
  }
  return fetch(url, { ...init, headers });
}

// Adds the Bearer token; on 401 renews the token once and retries, otherwise sends the user to sign in.
async function authorizedFetch(url: string, init: RequestInit): Promise<Response> {
  const response = await sendWithToken(url, init, false);
  if (response.status !== 401) {
    return response;
  }

  try {
    const retried = await sendWithToken(url, init, true);
    if (retried.status !== 401) {
      return retried;
    }
  } catch {
    // Token renewal failed - fall through to a fresh sign-in below.
  }

  await userManager.signinRedirect({ state: window.location.pathname + window.location.search });
  return new Promise<Response>(() => {});
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  // FormData (file upload) is sent as multipart; the browser sets the Content-Type with the boundary.
  const isForm = body instanceof FormData;
  const response = await authorizedFetch(`${baseUrl}/api/v1${path}`, {
    method,
    headers: body === undefined || isForm ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  });

  if (response.status === 403) {
    notifyForbidden();
    throw new ApiError('No access to this application', 403);
  }

  if (!response.ok) {
    let problem: ProblemDetails = {};
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // not JSON
    }
    const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new ApiError(
      validation || problem.detail || problem.title || `${response.status} ${response.statusText}`,
      response.status,
      problem.errors,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/** A binary GET with the token (cover images: an <img src> cannot send the Bearer header). Null on 404. */
async function getBlob(path: string): Promise<Blob | null> {
  const response = await authorizedFetch(`${baseUrl}/api/v1${path}`, { method: 'GET' });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(`${response.status} ${response.statusText}`, response.status);
  }
  return response.blob();
}

/** A GET whose body the caller reads as a stream (backup). Throws ApiError when not ok. */
async function getStream(path: string): Promise<Response> {
  const response = await authorizedFetch(`${baseUrl}/api/v1${path}`, { method: 'GET' });
  if (!response.ok || !response.body) {
    let detail = '';
    try {
      const problem = (await response.json()) as ProblemDetails;
      detail = problem.detail || problem.title || '';
    } catch {
      // not JSON
    }
    throw new ApiError(detail || `${response.status} ${response.statusText}`, response.status);
  }
  return response;
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T = void>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T = void>(path: string, body?: unknown) => request<T>('PUT', path, body),
  delete: <T = void>(path: string) => request<T>('DELETE', path),
  /** multipart/form-data POST (book upload). */
  upload: <T>(path: string, form: FormData) => request<T>('POST', path, form),
  blob: getBlob,
  stream: getStream,
};
