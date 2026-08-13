import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
    exercises = TestBed.inject(ExerciseService);
    mesocycles = TestBed.inject(MesocycleService);
    macrocycles = TestBed.inject(MacrocycleService);
    oneRepMaxes = TestBed.inject(OneRepMaxService);
  });

  /** Puni svaki keš nečim prepoznatljivim, zaobilazeći HTTP. */
  function fillCaches(): void {
    setSignal(exercises, 'exercisesSignal', [{ id: 'e1', name: 'Tajna vežba' }]);
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

  it('odjava prazni SVE keševe korisnika', () => {
    fillCaches();
    expect(readCaches().every(isEmpty)).toBe(false);

    auth.logout();

    readCaches().forEach((value, index) =>
      expect(isEmpty(value), `keš ${index} nije ispražnjen pri odjavi`).toBe(true),
    );
  });

  it('odjava briše token iz localStorage', () => {
    TestBed.inject(AuthTokenStorage).setToken('token-prethodnog-korisnika');
    expect(localStorage.getItem('strength-planner.token')).not.toBeNull();

    auth.logout();

    expect(localStorage.getItem('strength-planner.token')).toBeNull();
    expect(auth.currentUser()).toBeNull();
  });

  it('prijava novog korisnika prazni keševe prethodnog', () => {
    // Bez ovoga bi korisnik koji se prijavi odmah posle drugog (bez osvežavanja stranice)
    // zatekao tuđe podatke na ekranu.
    fillCaches();

    handleAuthenticated(auth, {
      token: 'novi-token',
      userId: 'u2',
      email: 'drugi@primer.com',
    });

    readCaches().forEach((value, index) =>
      expect(isEmpty(value), `keš ${index} nije ispražnjen pri prijavi`).toBe(true),
    );
  });
});

/** Upisuje u privatni signal servisa bez pravog HTTP poziva. */
function setSignal(service: object, field: string, value: unknown): void {
  const signal = (service as Record<string, { set(next: unknown): void }>)[field];
  signal.set(value);
}

function handleAuthenticated(auth: AuthService, response: unknown): void {
  (auth as unknown as { handleAuthenticated(value: unknown): void }).handleAuthenticated(response);
}
