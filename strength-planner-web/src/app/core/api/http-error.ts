import { HttpErrorResponse } from '@angular/common/http';

/**
 * Pulls a human-readable message out of an HTTP error, tolerating the several
 * shapes ASP.NET Core can return: a plain string body, ProblemDetails
 * ({ title }), a custom { message }, an Identity error array
 * ([{ code, description }]) or a ModelState dictionary ({ errors: { field: [] } }).
 * Falls back to the caller's message when nothing usable is found.
 */
export function extractErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  // Network / CORS failure (no HTTP response): `error.error` is a low-level
  // browser error like "Failed to fetch" — not worth showing. Use the fallback.
  if (error.status === 0) {
    return fallback;
  }

  const body = error.error;

  if (typeof body === 'string' && body.trim()) {
    return body.trim();
  }

  if (body && typeof body === 'object') {
    const record = body as Record<string, unknown>;

    if (typeof record['message'] === 'string') {
      return record['message'];
    }

    const fromErrors = collectMessages(record['errors']);
    if (fromErrors.length) {
      return fromErrors.join(' ');
    }

    if (typeof record['title'] === 'string') {
      return record['title'];
    }
  }

  return fallback;
}

function collectMessages(errors: unknown): string[] {
  if (!errors) {
    return [];
  }

  // Identity: [{ code, description }, ...]
  if (Array.isArray(errors)) {
    return errors
      .map((item) =>
        item && typeof item === 'object'
          ? (item as Record<string, unknown>)['description']
          : item,
      )
      .filter((message): message is string => typeof message === 'string');
  }

  // ModelState: { field: [messages], ... }
  if (typeof errors === 'object') {
    return Object.values(errors as Record<string, unknown>)
      .flatMap((value) => (Array.isArray(value) ? value : [value]))
      .filter((message): message is string => typeof message === 'string');
  }

  return [];
}
