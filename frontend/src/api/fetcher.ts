/**
 * Custom fetch used by every generated client function.
 *
 * It injects the caller's identity headers on all requests, so no call site has to remember to.
 * In production these are a verified bearer token; here they are freely settable, which is exactly
 * why they carry no security value. See docs/DESIGN.md §1.
 */

export type Identity = { tenantId: string; userId: string };

let identity: Identity = { tenantId: 'building-101', userId: 'resident-101' };

export function setIdentity(next: Identity) {
  identity = next;
}

/** Thrown for any non-2xx response, carrying enough for the UI to branch on the reason. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    /** Machine-readable reason from the RFC 7807 body, e.g. "CapacityExceeded". */
    readonly code: string | undefined,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export const customFetch = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const response = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
      'X-Tenant-Id': identity.tenantId,
      'X-User-Id': identity.userId,
    },
  });

  if (!response.ok) {
    // Problem responses are JSON; a bare 400 from model binding may not be.
    const problem = await response.json().catch(() => null);
    throw new ApiError(
      response.status,
      problem?.code,
      problem?.detail ?? problem?.title ?? `Request failed (${response.status})`,
    );
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
};
