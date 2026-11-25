import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { WakeLockService } from '../../core/wake-lock.service';
import { WorkoutSession } from './workout-session';

/**
 * Ekran treninga drži ekran telefona upaljenim dok trening traje. Sam servis je pokriven u
 * `wake-lock.service.spec.ts`; ovde je ono što je u ekranu: kad se traži, kad se otpušta, i oznaka.
 * Bez ovih provera brisanje `effect`-a ili obrnut uslov ostavljaju sve testove zelenim, a telefon
 * ostaje bez zaključavanja (ili ga drži posle izlaska sa ekrana).
 */
describe('WorkoutSession: ekran koji se ne gasi', () => {
  let fixture: ComponentFixture<WorkoutSession>;
  let http: HttpTestingController;
  let wakeLock: { hold: ReturnType<typeof vi.fn>; release: ReturnType<typeof vi.fn>; active: ReturnType<typeof signal<boolean>> };

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

  beforeEach(() => {
    wakeLock = {
      hold: vi.fn(async () => undefined),
      release: vi.fn(async () => undefined),
      active: signal(false),
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: WakeLockService, useValue: wakeLock },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function open(status: string): void {
    fixture = TestBed.createComponent(WorkoutSession);
    fixture.detectChanges();
    http.expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/')).flush(dto(status));
    fixture.detectChanges();
    TestBed.tick();
  }

  function text(): string {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('trening u toku traži da se ekran ne gasi', () => {
    open('InProgress');

    expect(wakeLock.hold).toHaveBeenCalled();
  });

  it('trening koji još nije počeo, završen ili preskočen ne drži ekran', () => {
    for (const status of ['Planned', 'Completed', 'Skipped']) {
      wakeLock.hold.mockClear();
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideHttpClient(),
          provideHttpClientTesting(),
          provideRouter([]),
          { provide: WakeLockService, useValue: wakeLock },
        ],
      });
      http = TestBed.inject(HttpTestingController);

      open(status);

      expect(wakeLock.hold, status).not.toHaveBeenCalled();
      http.verify();
    }
  });

  it('završetak treninga otpušta ekran', () => {
    open('InProgress');
    expect(wakeLock.hold).toHaveBeenCalled();
    wakeLock.release.mockClear();

    (fixture.componentInstance as unknown as { complete(): void }).complete();
    http
      .expectOne((request) => request.method === 'POST' && request.url.endsWith('/complete'))
      .flush({ sessionId: 's1', status: 'Completed', exercises: [], volumeAdjustments: [] });
    // Posle završetka se čita prozor ispravke.
    http.expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/')).flush(dto('Completed'));
    TestBed.tick();

    expect(wakeLock.release).toHaveBeenCalled();
  });

  it('izlazak sa ekrana otpušta ekran', () => {
    open('InProgress');
    wakeLock.release.mockClear();

    fixture.destroy();

    expect(wakeLock.release).toHaveBeenCalledTimes(1);
  });

  it('oznaka se prikazuje samo dok se ekran stvarno drži', () => {
    open('InProgress');

    expect(text()).not.toContain('Ekran ostaje upaljen');

    wakeLock.active.set(true);
    expect(text()).toContain('Ekran ostaje upaljen dok traje trening');

    wakeLock.active.set(false);
    expect(text()).not.toContain('Ekran ostaje upaljen');
  });
});
