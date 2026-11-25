import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { FATIGUE_RATINGS, FatigueRating } from './fatigue-rating';
import { WorkoutSession } from './workout-session';

describe('FatigueRating', () => {
  let fixture: ComponentFixture<FatigueRating>;

  function create(value: number | null = null): void {
    fixture = TestBed.createComponent(FatigueRating);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
  }

  function options(): HTMLButtonElement[] {
    fixture.detectChanges();
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
  }

  function selected(): string[] {
    return options()
      .filter((option) => option.getAttribute('aria-pressed') === 'true')
      .map((option) => option.getAttribute('aria-label') ?? '');
  }

  it('nudi pet odgovora, od 1 do 5, i nijedan nije izabran', () => {
    create();

    // Čitač ekrana dobija „1, Sveže" (aria-label), jer su broj i naziv dva elementa bez razmaka između njih.
    expect(options().map((option) => option.getAttribute('aria-label'))).toEqual([
      '1, Sveže',
      '2, Dobro',
      '3, Umereno',
      '4, Jako umoran',
      '5, Iscrpljen',
    ]);
    expect(selected()).toEqual([]);
    expect(fixture.componentInstance.value()).toBeNull();
  });

  it('izabran odgovor se ispisuje sa nazivom, a krajevi skale stoje ispod dugmadi', () => {
    create();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.textContent).toContain('1 · Sveže');
    expect(root.textContent).toContain('5 · Iscrpljen');
    expect(root.querySelector('.rating__chosen')).toBeNull();

    options()[3].click();
    fixture.detectChanges();
    expect(root.querySelector('.rating__chosen')?.textContent).toContain('Izabrano: 4, Jako umoran');

    options()[3].click();
    fixture.detectChanges();
    expect(root.querySelector('.rating__chosen')).toBeNull();
  });

  it('granice su 1 i 5, kao na serveru', () => {
    expect(FATIGUE_RATINGS.map((option) => option.value)).toEqual([1, 2, 3, 4, 5]);
  });

  it('dodir bira odgovor, a drugi dodir drugog odgovora ga menja', () => {
    create();

    options()[3].click();
    expect(fixture.componentInstance.value()).toBe(4);
    expect(selected()).toEqual(['4, Jako umoran']);

    options()[1].click();
    expect(fixture.componentInstance.value()).toBe(2);
    expect(selected()).toEqual(['2, Dobro']);
  });

  it('ponovni dodir izabranog odgovora ga poništava: pitanje je neobavezno', () => {
    create(3);
    expect(selected()).toEqual(['3, Umereno']);

    options()[2].click();

    expect(fixture.componentInstance.value()).toBeNull();
    expect(selected()).toEqual([]);
  });

  it('je grupa sa nazivom, da čitač ekrana kaže na šta se odgovara', () => {
    create();

    const group = (fixture.nativeElement as HTMLElement).querySelector('[role="group"]')!;
    const labelId = group.getAttribute('aria-labelledby')!;

    expect((fixture.nativeElement as HTMLElement).querySelector(`#${labelId}`)?.textContent).toContain(
      'Kako se osećaš posle treninga?',
    );
  });
});

/**
 * Odgovor mora da stigne uz završetak, jer se ocena umora nedelje računa kad se završi njen poslednji trening.
 */
describe('WorkoutSession: odgovor o umoru', () => {
  let fixture: ComponentFixture<WorkoutSession>;
  let http: HttpTestingController;

  function dto(status: string) {
    return {
      id: 's1',
      weekNumber: 1,
      isDeload: false,
      isAutoDeload: false,
      dayLabel: 'Day A',
      status,
      exercisePlans: [],
    };
  }

  function open(status: string): void {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(WorkoutSession);
    fixture.detectChanges();
    http.expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/')).flush(dto(status));
    fixture.detectChanges();
    TestBed.tick();
  }

  function finish(): void {
    (fixture.componentInstance as unknown as { complete(): void }).complete();
  }

  afterEach(() => http.verify());

  it('izabran odgovor ide uz završetak treninga', () => {
    open('InProgress');
    const rating = (fixture.nativeElement as HTMLElement).querySelectorAll('app-fatigue-rating button');
    (rating[3] as HTMLButtonElement).click();
    fixture.detectChanges();

    finish();

    const request = http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/complete'));
    expect(request.request.body).toEqual({ perceivedFatigue: 4 });
    request.flush({ sessionId: 's1', status: 'Completed', exercises: [], volumeAdjustments: [] });
    http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.includes('/sessions/')).flush(dto('Completed'));
  });

  it('bez odgovora završetak ide kao i pre, bez polja', () => {
    open('InProgress');

    finish();

    const request = http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/complete'));
    expect(request.request.body).toEqual({});
    request.flush({ sessionId: 's1', status: 'Completed', exercises: [], volumeAdjustments: [] });
    http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.includes('/sessions/')).flush(dto('Completed'));
  });

  it('pitanje se postavlja samo dok trening traje', () => {
    for (const status of ['Planned', 'Completed', 'Skipped']) {
      TestBed.resetTestingModule();
      open(status);

      expect((fixture.nativeElement as HTMLElement).querySelector('app-fatigue-rating'), status).toBeNull();
      http.verify();
    }

    TestBed.resetTestingModule();
    open('InProgress');
    expect((fixture.nativeElement as HTMLElement).querySelector('app-fatigue-rating')).not.toBeNull();
  });
});
