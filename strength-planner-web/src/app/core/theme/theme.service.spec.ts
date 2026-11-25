import { TestBed } from '@angular/core/testing';
import {
  THEME_COLORS,
  THEME_STORAGE_KEY,
  ThemeService,
  parseThemePreference,
} from './theme.service';

/**
 * Tema je jedna stvar koja mora da bude tačna pre nego što se išta iscrta, a jsdom ne zna ni za
 * `matchMedia`: zato se ovde postavlja lažan, pa se proverava šta servis radi sa sistemskom temom, sa
 * sačuvanim izborom, i sa pokvarenim prostorom za pamćenje.
 */
describe('ThemeService', () => {
  type Listener = (event: { matches: boolean }) => void;

  let systemDark: boolean;
  let listeners: Set<Listener>;
  let meta: HTMLMetaElement;
  const originalMatchMedia = window.matchMedia;

  function installMatchMedia(matches: boolean): void {
    systemDark = matches;
    listeners = new Set();
    window.matchMedia = vi.fn(
      () =>
        ({
          get matches() {
            return systemDark;
          },
          addEventListener: (_type: string, listener: Listener) => listeners.add(listener),
          removeEventListener: (_type: string, listener: Listener) => listeners.delete(listener),
        }) as unknown as MediaQueryList,
    );
  }

  function changeSystem(matches: boolean): void {
    systemDark = matches;
    listeners.forEach((listener) => listener({ matches }));
  }

  function create(): ThemeService {
    const service = TestBed.inject(ThemeService);
    TestBed.tick();
    return service;
  }

  const attribute = () => document.documentElement.getAttribute('data-theme');
  const barColor = () => meta.getAttribute('content');

  beforeEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
    meta = document.createElement('meta');
    meta.setAttribute('name', 'theme-color');
    meta.setAttribute('content', THEME_COLORS.light);
    document.head.appendChild(meta);
    installMatchMedia(false);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    meta.remove();
    document.documentElement.removeAttribute('data-theme');
    localStorage.removeItem(THEME_STORAGE_KEY);
    window.matchMedia = originalMatchMedia;
    vi.restoreAllMocks();
  });

  it('bez izbora prati sistem: svetao telefon daje svetlu temu i nijedan atribut', () => {
    const theme = create();

    expect(theme.preference()).toBe('system');
    expect(theme.effective()).toBe('light');
    expect(attribute()).toBeNull();
    expect(barColor()).toBe(THEME_COLORS.light);
  });

  it('bez izbora prati sistem: taman telefon daje tamnu temu, a atribut ostaje prazan (tema je u CSS-u)', () => {
    installMatchMedia(true);
    const theme = create();

    expect(theme.effective()).toBe('dark');
    expect(attribute()).toBeNull();
    expect(barColor()).toBe(THEME_COLORS.dark);
  });

  it('sačuvan izbor pobeđuje sistem, u oba smera', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    let theme = create();
    expect(theme.effective()).toBe('dark');
    expect(attribute()).toBe('dark');
    expect(barColor()).toBe(THEME_COLORS.dark);

    TestBed.resetTestingModule();
    installMatchMedia(true);
    localStorage.setItem(THEME_STORAGE_KEY, 'light');
    theme = create();
    expect(theme.effective()).toBe('light');
    expect(attribute()).toBe('light');
    expect(barColor()).toBe(THEME_COLORS.light);
  });

  it.each(['', 'purple', 'DARK', 'system', 'undefined'])('sačuvana vrednost „%s" znači: prati sistem', (stored) => {
    localStorage.setItem(THEME_STORAGE_KEY, stored);

    const theme = create();

    expect(theme.preference()).toBe('system');
    expect(attribute()).toBeNull();
  });

  it('izbor se primenjuje odmah i pamti', () => {
    const theme = create();

    theme.set('dark');
    TestBed.tick();

    expect(attribute()).toBe('dark');
    expect(theme.effective()).toBe('dark');
    expect(barColor()).toBe(THEME_COLORS.dark);
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('povratak na „kao sistem" skida atribut i ponovo sluša telefon', () => {
    installMatchMedia(true);
    localStorage.setItem(THEME_STORAGE_KEY, 'light');
    const theme = create();
    expect(attribute()).toBe('light');

    theme.set('system');
    TestBed.tick();

    expect(attribute()).toBeNull();
    expect(theme.effective()).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system');
  });

  it('promena teme na telefonu menja prikaz samo dok se prati sistem', () => {
    const theme = create();

    changeSystem(true);
    TestBed.tick();
    expect(theme.effective()).toBe('dark');
    expect(barColor()).toBe(THEME_COLORS.dark);

    theme.set('light');
    changeSystem(false);
    changeSystem(true);
    TestBed.tick();
    expect(theme.effective()).toBe('light');
    expect(attribute()).toBe('light');
  });

  it('uništen servis skida slušaoca sistemske teme', () => {
    create();
    expect(listeners.size).toBe(1);

    TestBed.resetTestingModule();

    expect(listeners.size).toBe(0);
  });

  it('prostor za pamćenje koji baca grešku ne ruši temu', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blokirano');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blokirano');
    });

    const theme = create();
    expect(theme.preference()).toBe('system');

    expect(() => theme.set('dark')).not.toThrow();
    TestBed.tick();
    expect(attribute()).toBe('dark');
  });

  it('bez meta oznake trake pregledača ništa ne puca', () => {
    meta.remove();

    const theme = create();

    expect(() => theme.set('dark')).not.toThrow();
    TestBed.tick();
    expect(attribute()).toBe('dark');
  });

  it('bez matchMedia (stariji pregledač) tema je svetla, a izbor i dalje radi', () => {
    window.matchMedia = undefined as unknown as typeof window.matchMedia;

    const theme = create();
    expect(theme.effective()).toBe('light');

    theme.set('dark');
    TestBed.tick();
    expect(theme.effective()).toBe('dark');
  });
});

describe('parseThemePreference', () => {
  it.each([
    ['light', 'light'],
    ['dark', 'dark'],
    ['system', 'system'],
    ['', 'system'],
    [null, 'system'],
    [undefined, 'system'],
    ['Dark', 'system'],
    ['sepia', 'system'],
  ])('%s -> %s', (raw, expected) => {
    expect(parseThemePreference(raw)).toBe(expected);
  });
});
