import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';

/** `system` prati podešavanje telefona; `light` i `dark` ga zaobilaze. */
export type ThemePreference = 'system' | 'light' | 'dark';

/** Tema koja se stvarno prikazuje. */
export type EffectiveTheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'strength-planner.theme';

/**
 * Boja trake pregledača (`<meta name="theme-color">`). Ista dva broja stoje u `public/theme-init.js`, koji
 * ih postavlja pre prvog iscrtavanja; `scripts/check-theme-contrast.py` proverava da se ne razilaze.
 */
export const THEME_COLORS: Readonly<Record<EffectiveTheme, string>> = {
  light: '#0e5c63',
  dark: '#0f1215',
};

export const THEME_OPTIONS: readonly { value: ThemePreference; label: string }[] = [
  { value: 'system', label: 'Kao sistem' },
  { value: 'light', label: 'Svetla' },
  { value: 'dark', label: 'Tamna' },
];

/** Sve što nije `light` ili `dark` (prazno, pokvareno, iz neke buduće verzije) znači „prati sistem". */
export function parseThemePreference(raw: string | null | undefined): ThemePreference {
  return raw === 'light' || raw === 'dark' ? raw : 'system';
}

/**
 * Tema aplikacije: svetla, tamna, ili kao na telefonu.
 *
 * Izbor se čuva u pregledaču (jedan po telefonu, nije podatak naloga) i primenjuje atributom `data-theme`
 * na `<html>`; bez atributa tema prati sistem (`prefers-color-scheme` u `styles.scss`). Isti atribut
 * postavlja i `public/theme-init.js` pre nego što se išta iscrta, da se pri otvaranju ne vidi kratak
 * bljesak svetle teme na tamnom telefonu; ovaj servis ga samo usklađuje kad se aplikacija pokrene.
 *
 * `effective` je tema koja se stvarno vidi: ko crta po platnu (grafikon) ili čita boje iz CSS-a mora da
 * se iscrta ponovo kad se promeni.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly systemQuery =
    this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)') ?? null;

  private readonly systemDark = signal(this.systemQuery?.matches ?? false);
  private readonly preferenceSignal = signal<ThemePreference>(this.read());

  readonly preference = this.preferenceSignal.asReadonly();

  readonly effective = computed<EffectiveTheme>(() => {
    const preference = this.preferenceSignal();
    if (preference === 'system') {
      return this.systemDark() ? 'dark' : 'light';
    }

    return preference;
  });

  constructor() {
    const onSystemChange = (event: MediaQueryListEvent) => this.systemDark.set(event.matches);

    // `?.` i na metodama: stariji Safari (do 14) ima `matchMedia`, ali bez `addEventListener` na rezultatu.
    this.systemQuery?.addEventListener?.('change', onSystemChange);
    inject(DestroyRef).onDestroy(() => this.systemQuery?.removeEventListener?.('change', onSystemChange));

    effect(() => this.apply(this.preferenceSignal(), this.effective()));
  }

  set(preference: ThemePreference): void {
    this.preferenceSignal.set(preference);

    try {
      this.document.defaultView?.localStorage?.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // Privatan prozor ili blokiran prostor: izbor važi do kraja ove sesije.
    }
  }

  private apply(preference: ThemePreference, effective: EffectiveTheme): void {
    const root = this.document.documentElement;

    if (preference === 'system') {
      root.removeAttribute('data-theme');
    } else {
      root.setAttribute('data-theme', preference);
    }

    this.document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', THEME_COLORS[effective]);
  }

  private read(): ThemePreference {
    try {
      return parseThemePreference(this.document.defaultView?.localStorage?.getItem(THEME_STORAGE_KEY));
    } catch {
      return 'system';
    }
  }
}
