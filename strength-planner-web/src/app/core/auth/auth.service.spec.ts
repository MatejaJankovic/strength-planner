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

  /** Puni svaki keš nečim prepoznatljivim. Vežbe i slika idu kroz pravi HTTP put. */
  function fillCaches(): void {
    exercises.load().subscribe();
    http.expectOne((request) => request.url.endsWith('/exercises')).flush([
      { id: 'e1', name: 'Tajna vežba' },
    ]);

    // Slika profila je isto korisnički keš: `blob:` URL ostaje upotrebljiv dok se ne
    // poništi, pa bi bez čišćenja lice prethodnog korisnika stajalo na profilu narednog.
    auth.loadAvatar().subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/avatar')).flush(
      new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], { type: 'image/jpeg' }),
    );

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
      auth.avatarUrl(),
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
        displayName: 'Treći',
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

  /**
   * Slika je peti keš vezan za korisnika, i prvi koji nije samo signal u memoriji: dok
   * `blob:` URL nije poništen, pregledač i dalje isporučuje sadržaj sa te adrese.
   *
   * Komentar uz `resetUserCaches` traži da svaki nov korisnički keš bude naveden i ovde.
   * Ovaj test je taj upis.
   */
  it('odjava poništava blob URL slike, ne samo referencu na njega', () => {
    const revoked: string[] = [];
    const originalRevoke = URL.revokeObjectURL;
    URL.revokeObjectURL = (url: string) => {
      revoked.push(url);
      originalRevoke.call(URL, url);
    };

    try {
      fillCaches();
      const url = auth.avatarUrl();
      expect(url, 'slika nije napunjena pre provere').not.toBeNull();

      auth.logout();

      expect(auth.avatarUrl()).toBeNull();
      expect(revoked, 'blob URL nije poništen, samo je referenca obrisana').toContain(url!);
    } finally {
      URL.revokeObjectURL = originalRevoke;
    }
  });

  it('otpremanje nove slike pušta prethodni blob, da se ne gomilaju u memoriji', () => {
    const revoked: string[] = [];
    const originalRevoke = URL.revokeObjectURL;
    URL.revokeObjectURL = (url: string) => {
      revoked.push(url);
      originalRevoke.call(URL, url);
    };

    try {
      auth.loadAvatar().subscribe();
      http.expectOne((request) => request.url.endsWith('/auth/avatar')).flush(
        new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], { type: 'image/jpeg' }),
      );
      const first = auth.avatarUrl()!;

      // Otpremanje je jedini put kojim slika na serveru postaje druga, pa je i jedini koji
      // sme da natera nov zahtev.
      auth.uploadAvatar(new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'nova.png')).subscribe();
      http
        .expectOne((candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'PUT')
        .flush({ id: 'u1', email: 'ja@primer.com', hasAvatar: true });

      auth.loadAvatar().subscribe();
      http
        .expectOne((candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'GET')
        .flush(new Blob([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], { type: 'image/png' }));

      expect(revoked).toContain(first);
      expect(auth.avatarUrl()).not.toBe(first);
    } finally {
      URL.revokeObjectURL = originalRevoke;
    }
  });

  /**
   * API na sve odgovore šalje `Cache-Control: no-store`, pa keš pregledača ne pomaže:
   * profil → izmena → profil bila su tri preuzimanja slike do dva megabajta.
   */
  it('drugo čitanje slike ne šalje nov zahtev', () => {
    auth.loadAvatar().subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/avatar')).flush(
      new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], { type: 'image/jpeg' }),
    );
    const first = auth.avatarUrl();

    let second: string | null = 'nije pozvano';
    auth.loadAvatar().subscribe((url) => (second = url));

    // Nema drugog zahteva - `afterEach` sa `http.verify()` pada ako se pošalje.
    expect(second).toBe(first);
  });

  it('nalog bez slike se ne pita dva puta', () => {
    // "Nema sliku" je isto tako odgovor: bez pamćenja te činjenice svaki ekran bi ponovo
    // dobijao 404.
    auth.loadAvatar().subscribe();
    http
      .expectOne((request) => request.url.endsWith('/auth/avatar'))
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(auth.avatarUrl()).toBeNull();

    let second: string | null | undefined;
    auth.loadAvatar().subscribe((url) => (second = url));

    expect(second).toBeNull();
  });

  it('404 posle prethodno dohvaćene slike gasi prikaz', () => {
    // Slika obrisana u drugom tabu: bez čišćenja na 404 ostajala je stara na ekranu, jer
    // je greška prolazila do pozivaoca i mapiranje se nije izvršilo.
    auth.loadAvatar().subscribe();
    http.expectOne((request) => request.url.endsWith('/auth/avatar')).flush(
      new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], { type: 'image/jpeg' }),
    );
    expect(auth.avatarUrl()).not.toBeNull();

    // Otpremanje poništava keš, pa naredno čitanje ponovo pita server.
    auth.uploadAvatar(new File([new Uint8Array([0xff, 0xd8, 0xff])], 'x.jpg')).subscribe();
    http
      .expectOne((candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'PUT')
      .flush({ id: 'u1', email: 'ja@primer.com', hasAvatar: true });

    auth.loadAvatar().subscribe();
    http
      .expectOne((candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'GET')
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(auth.avatarUrl()).toBeNull();
  });

  it('otpremanje slike ide kao multipart, ne kao base64 u JSON-u', () => {
    auth.uploadAvatar(new File([new Uint8Array([0xff, 0xd8, 0xff])], 'lice.jpg')).subscribe();

    const request = http.expectOne(
      (candidate) => candidate.url.endsWith('/auth/avatar') && candidate.method === 'PUT',
    );

    expect(request.request.body).toBeInstanceOf(FormData);
    request.flush({ id: 'u1', email: 'ja@primer.com', hasAvatar: true });
  });
});

/** Upisuje u privatni signal servisa bez pravog HTTP poziva. */
function setSignal(service: object, field: string, value: unknown): void {
  const signal = (service as Record<string, { set(next: unknown): void }>)[field];
  signal.set(value);
}
