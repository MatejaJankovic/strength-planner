import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthTokenStorage } from './auth-token-storage';
import { ExerciseService } from '../api/exercise.service';
import { MacrocycleService } from '../api/macrocycle.service';
import { MesocycleService } from '../api/mesocycle.service';
import { OneRepMaxService } from '../api/one-rep-max.service';

/**
 * Servisi koji keširaju korisničke podatke žive na nivou cele aplikacije, pa promena
 * identiteta bez osvežavanja stranice ostavlja podatke prethodnog korisnika u memoriji.
 *
 * Ovo je već jednom promaklo: `OneRepMaxService` nije imao `reset()`, pa su na deljenom
 * računaru maksimumi prethodnog korisnika ostajali vidljivi sledećem.
 */
describe('AuthService — čišćenje keševa pri promeni identiteta', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let exercises: ExerciseService;
  let mesocycles: MesocycleService;
  let macrocycles: MacrocycleService;
  let oneRepMaxes: OneRepMaxService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // logout() preusmerava na /login; bez te rute router baca neuhvaćeno odbijanje.
        provideRouter([{ path: 'login', children: [] }]),
      ],
    });

    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    exercises = TestBed.inject(ExerciseService);
    mesocycles = TestBed.inject(MesocycleService);
    macrocycles = TestBed.inject(MacrocycleService);
    oneRepMaxes = TestBed.inject(OneRepMaxService);
  });

  afterEach(() => http.verify());

  /** Puni svaki keš nečim prepoznatljivim. Vežbe idu kroz pravi HTTP put. */
  function fillCaches(): void {
    exercises.load().subscribe();
    http.expectOne((request) => request.url.endsWith('/exercises')).flush([
      { id: 'e1', name: 'Tajna vežba' },
    ]);

    setSignal(mesocycles, 'activeSignal', { id: 'm1', name: 'Tajni plan' });
    setSignal(macrocycles, 'activeSignal', { id: 'p1', name: 'Tajni dugoročni plan' });
    setSignal(oneRepMaxes, 'oneRepMaxesSignal', [{ exerciseId: 'e1', valueKg: 200 }]);
  }

  function readCaches(): unknown[] {
    return [
      exercises.exercises(),
      mesocycles.active(),
      macrocycles.active(),
      oneRepMaxes.oneRepMaxes(),
    ];
  }

  function isEmpty(value: unknown): boolean {
    return value === null || (Array.isArray(value) && value.length === 0);
  }

  function expectAllCachesEmpty(when: string): void {
    readCaches().forEach((value, index) =>
      expect(isEmpty(value), `keš ${index} nije ispražnjen ${when}`).toBe(true),
    );
  }

  it('odjava prazni SVE keševe korisnika', () => {
    fillCaches();
    readCaches().forEach((value, index) =>
      expect(isEmpty(value), `keš ${index} nije napunjen pre provere`).toBe(false),
    );

    auth.logout();

    expectAllCachesEmpty('pri odjavi');
  });

  /**
   * `ExerciseService.reset()` radi dve stvari: prazni signal i pušta keširani
   * `shareReplay(1)` zahtev. Provera samo signala je propuštala drugu — bez nje bi
   * sledeći korisnik dobio spisak vežbi prethodnog iz replay-a, bez ijednog HTTP poziva.
   */
  it('odjava pušta i keširani zahtev za vežbama, ne samo signal', () => {
    fillCaches();

    auth.logout();

    exercises.load().subscribe();
    http.expectOne((request) => request.url.endsWith('/exercises')).flush([]);
  });

  it('odjava briše token iz localStorage', () => {
    TestBed.inject(AuthTokenStorage).setToken('token-prethodnog-korisnika');
    expect(localStorage.getItem('strength-planner.token')).not.toBeNull();

    auth.logout();

    expect(localStorage.getItem('strength-planner.token')).toBeNull();
    expect(auth.currentUser()).toBeNull();
  });

  /**
   * Ide kroz pravi `login()`, a ne kroz internu metodu — inače test ne bi primetio da je
   * neko iz prijave izostavio čišćenje keševa.
   */
  it('prijava novog korisnika prazni keševe prethodnog', () => {
    fillCaches();

    auth.login({ email: 'drugi@primer.com', password: 'DovoljnoDugaLozinka1' }).subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/login')).flush({
      userId: 'u2',
      email: 'drugi@primer.com',
      token: 'novi-token',
      expiresAt: new Date().toISOString(),
    });

    expectAllCachesEmpty('pri prijavi');
    expect(localStorage.getItem('strength-planner.token')).toBe('novi-token');
  });

  it('registracija takođe prazni keševe prethodnog korisnika', () => {
    fillCaches();

    auth
      .register({
        email: 'treci@primer.com',
        password: 'DovoljnoDugaLozinka1',
        age: 30,
        bodyweightKg: 80,
        experienceLevel: 1,
      })
      .subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/register')).flush({
      userId: 'u3',
      email: 'treci@primer.com',
      token: 'token-trojke',
      expiresAt: new Date().toISOString(),
    });

    expectAllCachesEmpty('pri registraciji');
  });
});

/** Upisuje u privatni signal servisa bez pravog HTTP poziva. */
function setSignal(service: object, field: string, value: unknown): void {
  const signal = (service as Record<string, { set(next: unknown): void }>)[field];
  signal.set(value);
}
