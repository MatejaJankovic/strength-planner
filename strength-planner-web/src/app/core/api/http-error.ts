import { HttpErrorResponse } from '@angular/common/http';

/**
 * Opšti engleski naslovi koje server šalje uz grešku (Program.cs, mapiranje izuzetaka). U produkciji je to jedino
 * što stiže (`ProblemDetails.Detail` je isti kao `Title`, vidi `docs/security.md`), a vežbač ga je do sada čitao na
 * ekranu kao „Request conflicts with current state." Ovde dobijaju srpsku poruku po onome što znače. Naslov koji
 * nije na ovom spisku prolazi kakav jeste.
 */
const GENERIC_TITLES: Readonly<Record<string, string>> = {
  'Validation failed.': 'Uneti podaci nisu ispravni.',
  'Model validation failed.': 'Uneti podaci nisu ispravni.',
  'Invalid request.': 'Zahtev nije ispravan.',
  'Mesocycle generation failed.': 'Blok nije mogao da se napravi.',
  'Resource was not found.': 'Traženo ne postoji ili više nije dostupno.',
  'Request conflicts with current state.':
    'Radnja nije moguća u trenutnom stanju. Osveži ekran i pokušaj ponovo.',
  'Unauthorized.': 'Nisi prijavljen ili je prijava istekla.',
  'Unexpected server error.': 'Greška na serveru. Pokušaj ponovo.',
};

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
    const text = body.trim();
    // Stranica greške proxy-ja ispred API-ja (nginx dok se API ponovo podiže, ili prevelik
    // zahtev) nije poruka za vežbača, i ne sme se ispisati na ekranu.
    return error.status >= 500 || text.startsWith('<') ? fallback : text;
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
      return GENERIC_TITLES[record['title']] ?? record['title'];
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
