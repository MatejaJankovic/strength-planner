import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { WakeLockService } from '../../core/wake-lock.service';
import { RestTimerService } from './rest-timer.service';
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

/**
 * Pomagala na ekranu treninga: pauza između serija, ploče za šipku i zagrevanje. Same računice su u
 * svojim datotekama; ovde se proverava da ih ekran zaista poziva, i kad ih ne sme: pauza koja
 * krene dok se ispravlja završen trening, ili zagrevanje posle prve radne serije, ne bi pokvarila
 * nijedan test računice.
 */
describe('WorkoutSession: pomagala', () => {
  let fixture: ComponentFixture<WorkoutSession>;
  let http: HttpTestingController;
  let restTimer: RestTimerService;

  const wakeLock = {
    hold: vi.fn(async () => undefined),
    release: vi.fn(async () => undefined),
    active: signal(false),
  };

  function plan(overrides: Record<string, unknown> = {}) {
    return {
      id: 'p1',
      exerciseId: 'e1',
      exerciseName: 'Bench Press',
      order: 1,
      targetSets: 3,
      prescribedSets: 3,
      repRangeMin: 5,
      repRangeMax: 8,
      targetRir: 2,
      targetWeightKg: 100,
      weightStepKg: 2.5,
      equipment: 'Barbell',
      exerciseType: 'Compound',
      isBodyweight: false,
      bodyweightLoadKg: 0,
      setLogs: [],
      ...overrides,
    };
  }

  function session(status: string, plans: unknown[], extra: Record<string, unknown> = {}) {
    return {
      id: 's1',
      weekNumber: 1,
      isDeload: false,
      isAutoDeload: false,
      dayLabel: 'Day A',
      status,
      exercisePlans: plans,
      ...extra,
    };
  }

  function open(dto: object): void {
    fixture = TestBed.createComponent(WorkoutSession);
    fixture.detectChanges();
    http.expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/')).flush(dto);
    fixture.detectChanges();
    TestBed.tick();
  }

  function text(): string {
    fixture.detectChanges();
    return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  }

  function saveSet(planDto: ReturnType<typeof plan>): void {
    (fixture.componentInstance as unknown as { submitSet(p: unknown): void }).submitSet(planDto);
    http
      .expectOne((request) => request.method === 'POST' && request.url.endsWith('/exercise-plans/p1/sets'))
      .flush({
        id: 'set-1',
        exercisePlanId: 'p1',
        setNumber: 1,
        weightKg: 100,
        reps: 5,
        rir: 2,
        isFailure: false,
        bodyweightLoadKg: 0,
        performedAt: new Date().toISOString(),
      });
  }

  beforeEach(() => {
    wakeLock.hold.mockClear();
    wakeLock.release.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: WakeLockService, useValue: wakeLock },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    restTimer = TestBed.inject(RestTimerService);
  });

  afterEach(() => {
    restTimer.stop();
    http.verify();
  });

  describe('pauza između serija', () => {
    it('počinje kad se sačuva serija na treningu u toku', () => {
      const bench = plan();
      open(session('InProgress', [bench]));
      expect(restTimer.running()).toBe(false);
      expect(text()).not.toContain('Pauza između serija');

      saveSet(bench);

      expect(restTimer.running()).toBe(true);
      expect(text()).toContain('0:00');
    });

    it('ne počinje dok se ispravlja završen trening', () => {
      const bench = plan();
      open(
        session('Completed', [bench], {
          correction: { canCorrect: true, mode: 'Recalculate', revisionPending: true },
        }),
      );

      saveSet(bench);

      expect(restTimer.running()).toBe(false);
    });

    it('prestaje kad se trening završi', () => {
      const bench = plan();
      open(session('InProgress', [bench]));
      saveSet(bench);
      expect(restTimer.running()).toBe(true);

      (fixture.componentInstance as unknown as { complete(): void }).complete();
      http
        .expectOne((request) => request.method === 'POST' && request.url.endsWith('/complete'))
        .flush({ sessionId: 's1', status: 'Completed', exercises: [], volumeAdjustments: [] });
      http
        .expectOne((request) => request.method === 'GET' && request.url.includes('/sessions/'))
        .flush(session('Completed', [bench]));
      TestBed.tick();

      expect(restTimer.running()).toBe(false);
    });

    it('prestaje kad se napusti ekran', () => {
      const bench = plan();
      open(session('InProgress', [bench]));
      saveSet(bench);
      expect(restTimer.running()).toBe(true);

      fixture.destroy();

      expect(restTimer.running()).toBe(false);
    });
  });

  describe('pauza i izmena serije, i serija koja nije sačuvana', () => {
    it('izmena već sačuvane serije ne počinje novu pauzu', () => {
      const logged = {
        id: 'set-0',
        exercisePlanId: 'p1',
        setNumber: 1,
        weightKg: 100,
        reps: 5,
        rir: 2,
        isFailure: false,
        bodyweightLoadKg: 0,
        performedAt: new Date().toISOString(),
      };
      const bench = plan({ setLogs: [logged] });
      open(session('InProgress', [bench]));
      expect(restTimer.running()).toBe(false);

      const screen = fixture.componentInstance as unknown as {
        editSet(planId: string, set: typeof logged): void;
        submitSet(p: unknown): void;
      };
      screen.editSet('p1', logged);
      screen.submitSet(bench);
      http
        .expectOne((request) => request.method === 'PUT' && request.url.endsWith('/sets/set-0'))
        .flush({ ...logged, reps: 6 });

      expect(restTimer.running()).toBe(false);
    });

    it('serija koja nije sačuvana ne zaustavlja pauzu: serija je odrađena, samo nije upisana', () => {
      const bench = plan();
      open(session('InProgress', [bench]));

      (fixture.componentInstance as unknown as { submitSet(p: unknown): void }).submitSet(bench);
      http
        .expectOne((request) => request.method === 'POST' && request.url.endsWith('/exercise-plans/p1/sets'))
        .flush({ message: 'greška' }, { status: 500, statusText: 'Server Error' });

      expect(restTimer.running()).toBe(true);
    });
  });

  describe('ploče', () => {
    it('piše ploče za vežbu sa šipkom, po težini koja se unosi', () => {
      open(session('InProgress', [plan()]));

      expect(text()).toContain('Po strani (šipka 20 kg): 25 + 15.');
    });

    it('piše ih i dok se trening tek sprema, da se šipka može pripremiti', () => {
      open(session('Planned', [plan()]));

      expect(text()).toContain('Po strani (šipka 20 kg): 25 + 15.');
    });

    it('ne piše ih na završenom ni na preskočenom treningu, ni dok se ispravlja', () => {
      for (const status of ['Completed', 'Skipped']) {
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
        restTimer = TestBed.inject(RestTimerService);

        open(
          session(status, [plan()], {
            correction: { canCorrect: true, mode: 'Recalculate', revisionPending: true },
          }),
        );

        expect(text(), status).not.toContain('Po strani');
        http.verify();
      }
    });

    it('ne piše ih za pregib ni skull crusher, koji su „Barbell" ali se rade lakom šipkom', () => {
      open(
        session('InProgress', [
          plan({ id: 'p2', exerciseName: 'Barbell Curl', exerciseType: 'Isolation', targetWeightKg: 25 }),
        ]),
      );

      expect(text()).not.toContain('Po strani');
    });

    it('ne piše ih kad server ne pošalje tip vežbe', () => {
      open(session('InProgress', [plan({ exerciseType: undefined })]));

      expect(text()).not.toContain('Po strani');
    });

    it('ne piše ih za bučicu ni za mašinu', () => {
      open(
        session('InProgress', [
          plan({ id: 'p2', exerciseName: 'Curl', equipment: 'Dumbbell', weightStepKg: 2 }),
          plan({ id: 'p3', exerciseName: 'Leg Press', equipment: 'Machine' }),
        ]),
      );

      expect(text()).not.toContain('Po strani');
    });
  });

  describe('zagrevanje', () => {
    it('predlaže se pre prve radne serije, na treningu u toku', () => {
      open(session('InProgress', [plan()]));

      expect(text()).toContain('Zagrevanje');
      expect(text()).toContain('40 kg × 5 · 60 kg × 3 · 80 kg × 1');
    });

    it('ne predlaže se pre početka treninga ni posle završetka', () => {
      open(session('Planned', [plan()]));
      expect(text()).not.toContain('Zagrevanje');
    });

    it('ne predlaže se na završenom ni na preskočenom treningu, ni uz vežbu bez serija', () => {
      for (const status of ['Completed', 'Skipped']) {
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
        restTimer = TestBed.inject(RestTimerService);

        open(session(status, [plan()]));

        expect(text(), status).not.toContain('Zagrevanje');
        http.verify();
      }
    });

    it('nestaje kad je prva radna serija sačuvana', () => {
      const bench = plan();
      open(session('InProgress', [bench]));
      expect(text()).toContain('Zagrevanje');

      saveSet(bench);

      expect(text()).not.toContain('Zagrevanje');
    });

    it('ne predlaže se za izolaciju sa šipkom (pregib, skull crusher)', () => {
      open(
        session('InProgress', [
          plan({ exerciseName: 'Barbell Curl', exerciseType: 'Isolation', targetWeightKg: 50 }),
        ]),
      );

      expect(text()).not.toContain('Zagrevanje');
    });

    it('ne predlaže se za vežbu bez šipke', () => {
      open(session('InProgress', [plan({ equipment: 'Dumbbell', weightStepKg: 2 })]));

      expect(text()).not.toContain('Zagrevanje');
    });
  });
});
