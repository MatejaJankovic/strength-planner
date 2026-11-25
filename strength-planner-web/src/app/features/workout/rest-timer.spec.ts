import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RestTimer } from './rest-timer';
import { RestTimerService } from './rest-timer.service';

const STORAGE_KEY = 'strength-planner.rest-target';

describe('RestTimer', () => {
  let fixture: ComponentFixture<RestTimer>;
  let timer: RestTimerService;

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  function buttons(): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
  }

  function buttonWithText(label: string): HTMLButtonElement {
    const found = buttons().find((button) => button.textContent?.trim() === label);
    if (!found) {
      throw new Error(`Nema dugmeta „${label}"`);
    }
    return found;
  }

  beforeEach(async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-25T10:00:00Z'));
    localStorage.removeItem(STORAGE_KEY);
    Object.defineProperty(navigator, 'vibrate', { value: vi.fn(() => true), configurable: true });

    await TestBed.configureTestingModule({ imports: [RestTimer] }).compileComponents();
    timer = TestBed.inject(RestTimerService);
    fixture = TestBed.createComponent(RestTimer);
    fixture.detectChanges();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.removeItem(STORAGE_KEY);
    Reflect.deleteProperty(navigator, 'vibrate');
  });

  it('ne prikazuje ništa dok pauza ne krene', () => {
    expect(text()).toBe('');
  });

  it('prikazuje pauzu koja teče', () => {
    timer.start();
    vi.advanceTimersByTime(65_000);
    fixture.detectChanges();

    expect(text()).toContain('1:05');
  });

  function chooserButton(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[aria-label="Cilj pauze"]',
    )!;
  }

  it('ponuđeni ciljevi su skriveni dok se ne zatraže: traka je zalepljena uz vrh i mora da bude niska', () => {
    timer.start();
    fixture.detectChanges();

    expect(chooserButton().getAttribute('aria-expanded')).toBe('false');
    expect((fixture.nativeElement as HTMLElement).querySelector('.rest__targets')).toBeNull();
  });

  it('ponuđeni ciljevi: izabran je označen, dodir ga menja i pamti, a izbor se zatvara', () => {
    timer.start();
    fixture.detectChanges();

    chooserButton().click();
    fixture.detectChanges();

    expect(chooserButton().getAttribute('aria-expanded')).toBe('true');
    expect(buttonWithText('Bez cilja').getAttribute('aria-pressed')).toBe('true');
    expect(buttonWithText('2:00').getAttribute('aria-pressed')).toBe('false');

    buttonWithText('2:00').click();
    fixture.detectChanges();

    expect(timer.targetSeconds()).toBe(120);
    expect(localStorage.getItem(STORAGE_KEY)).toBe('120');
    expect(text()).toContain('/ 2:00');
    expect((fixture.nativeElement as HTMLElement).querySelector('.rest__targets')).toBeNull();

    // Opet otvoren, izabran je upravo taj cilj.
    chooserButton().click();
    fixture.detectChanges();
    expect(buttonWithText('2:00').getAttribute('aria-pressed')).toBe('true');
    expect(buttonWithText('Bez cilja').getAttribute('aria-pressed')).toBe('false');
  });

  it('kad cilj istekne, to piše, a brojanje ide dalje', () => {
    timer.setTarget(90);
    timer.start();
    vi.advanceTimersByTime(95_000);
    fixture.detectChanges();

    expect(text()).toContain('Pauza je istekla.');
    expect(text()).toContain('1:35');
  });

  it('„ispočetka" vraća brojanje na nulu, a „zatvori" skriva traku', () => {
    timer.start();
    vi.advanceTimersByTime(30_000);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[aria-label="Pauza ispočetka"]')!
      .click();
    fixture.detectChanges();
    expect(text()).toContain('0:00');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[aria-label="Zatvori tajmer"]')!
      .click();
    fixture.detectChanges();

    expect(timer.running()).toBe(false);
    expect(text()).toBe('');
  });
});
