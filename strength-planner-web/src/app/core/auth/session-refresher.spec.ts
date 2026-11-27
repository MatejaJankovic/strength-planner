import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RELOAD_APP } from './app-reload';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { AuthTokenStorage } from './auth-token-storage';
import { REFRESH_LOCK_WAIT_MS, REFRESH_TIMEOUT_MS, SessionRefresher } from './session-refresher';

/**
 * Pristupni token traje sat vremena, a trening duže. Ovi testovi drže ono zbog čega obnavljanje
 * postoji: istekao token ne sme da odjavi vežbača, osim kada prijave zaista više nema.
 */
describe('obnavljanje prijave', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let storage: AuthTokenStorage;
  let router: Router;
  let reloads: number;

  /** Token sa `sub` claim-om, onakav kakav server izdaje po obliku. */
  function tokenFor(account: string, issue: number): string {
    return `header.${btoa(JSON.stringify({ sub: account, issue }))}.signature`;
  }

  const stale = tokenFor('lifter', 1);
  const renewed = tokenFor('lifter', 2);

  beforeEach(() => {
    localStorage.clear();
    reloads = 0;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'login', children: [] },
          { path: 'workout', children: [] },
        ]),
        { provide: RELOAD_APP, useValue: () => reloads++ },
      ],
    });

    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    storage = TestBed.inject(AuthTokenStorage);
    router = TestBed.inject(Router);
    TestBed.inject(AuthService);
    storage.save(stale, 'refresh-1');
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      // Zahtev koji ostane da visi inače obori i sve testove posle ovog.
      TestBed.resetTestingModule();
    }
  });

  /** Pušta obećanja iza `firstValueFrom` da se razreše, pa zahtev koji sledi bude poslat. */
  const settle = () => new Promise<void>((resolve) => setTimeout(resolve));

  function send(url = '/api/sessions'): { values: unknown[]; errors: HttpErrorResponse[] } {
    const outcome = { values: [] as unknown[], errors: [] as HttpErrorResponse[] };
    client.get(url).subscribe({
      next: (value) => outcome.values.push(value),
      error: (error: HttpErrorResponse) => outcome.errors.push(error),
    });
    return outcome;
  }

  function refuse(request: TestRequest): void {
    request.flush(null, { status: 401, statusText: 'Unauthorized' });
  }

  function expectRefresh(): TestRequest {
    return http.expectOne((request) => request.url.endsWith('/auth/refresh'));
  }

  function grant(request: TestRequest, token = renewed, refreshToken = 'refresh-2'): void {
    request.flush({ userId: 'lifter', email: 'a@primer.com', token, expiresAt: '', refreshToken });
  }

  /** Ono što uradi drugi tab: piše pravo u skladište, a ovaj tab o tome još ništa ne zna. */
  function otherTabStored(token: string, refreshToken: string): void {
    localStorage.setItem('strength-planner.refresh-token', refreshToken);
    localStorage.setItem('strength-planner.token', token);
  }

  function signedIn(): boolean {
    return storage.accessToken() !== null && storage.refreshToken() !== null;
  }

  it('istekao token se obnavlja i zahtev ponavlja sa novim', async () => {
    const outcome = send();
    const first = http.expectOne('/api/sessions');
    expect(first.request.headers.get('Authorization')).toBe(`Bearer ${stale}`);
    refuse(first);

    const refresh = expectRefresh();
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(refresh.request.headers.has('Authorization')).toBe(false);
    grant(refresh);
    await settle();

    const repeated = http.expectOne('/api/sessions');
    expect(repeated.request.headers.get('Authorization')).toBe(`Bearer ${renewed}`);
    repeated.flush(['trening']);

    expect(outcome.values).toEqual([['trening']]);
    expect(outcome.errors).toEqual([]);
    expect(storage.refreshToken()).toBe('refresh-2');
  });

  it('upis serije koji naiđe na istekao token šalje se ponovo, sa istim telom', async () => {
    client.post('/api/exercise-plans/p1/sets', { reps: 8 }).subscribe();
    refuse(http.expectOne('/api/exercise-plans/p1/sets'));
    grant(expectRefresh());
    await settle();

    const repeated = http.expectOne('/api/exercise-plans/p1/sets');
    expect(repeated.request.method).toBe('POST');
    expect(repeated.request.body).toEqual({ reps: 8 });
    repeated.flush({});
  });

  it('više odbijenih zahteva deli jedno obnavljanje', async () => {
    const one = send('/api/a');
    const two = send('/api/b');
    refuse(http.expectOne('/api/a'));
    refuse(http.expectOne('/api/b'));

    // `expectOne` pada da su poslata dva obnavljanja: drugo bi server video kao krađu.
    grant(expectRefresh());
    await settle();

    http.expectOne('/api/a').flush(1);
    http.expectOne('/api/b').flush(2);
    expect(one.values).toEqual([1]);
    expect(two.values).toEqual([2]);
  });

  it('zahtev odbijen posle obnavljanja koristi već obnovljen token', async () => {
    send('/api/a');
    send('/api/b');
    const late = http.expectOne('/api/b');
    refuse(http.expectOne('/api/a'));
    grant(expectRefresh());
    await settle();
    http.expectOne('/api/a').flush(1);

    // Odgovor na zahtev poslat sa starim tokenom stiže tek sada.
    refuse(late);
    await settle();

    const repeated = http.expectOne('/api/b');
    expect(repeated.request.headers.get('Authorization')).toBe(`Bearer ${renewed}`);
    repeated.flush(2);
  });

  it.each([
    ['bez mreže', 0],
    ['server ne radi', 502],
    ['previše zahteva', 429],
  ])('neuspelo obnavljanje (%s) ne odjavljuje', async (_, status) => {
    const outcome = send();
    refuse(http.expectOne('/api/sessions'));
    const refresh = expectRefresh();
    if (status === 0) {
      refresh.error(new ProgressEvent('error'));
    } else {
      refresh.flush(null, { status, statusText: 'x' });
    }
    await settle();

    expect(signedIn()).toBe(true);
    expect(outcome.errors.map((error) => error.status)).toEqual([status]);

    // Sledeći pokušaj ponovo obnavlja, istim tokenom.
    send();
    refuse(http.expectOne('/api/sessions'));
    const again = expectRefresh();
    expect(again.request.body).toEqual({ refreshToken: 'refresh-1' });
    grant(again);
    await settle();
    http.expectOne('/api/sessions').flush([]);
  });

  it('obnavljanje bez odgovora se napušta posle roka i ne odjavljuje', async () => {
    vi.useFakeTimers();
    try {
      const outcome = send();
      refuse(http.expectOne('/api/sessions'));
      const refresh = expectRefresh();

      await vi.advanceTimersByTimeAsync(REFRESH_TIMEOUT_MS + 1);

      expect(refresh.cancelled).toBe(true);
      expect(outcome.errors.map((error) => error.status)).toEqual([0]);
      expect(signedIn()).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it('odbijeno obnavljanje odjavljuje i vodi na prijavu', async () => {
    const outcome = send();
    refuse(http.expectOne('/api/sessions'));
    refuse(expectRefresh());
    await settle();

    expect(signedIn()).toBe(false);
    expect(outcome.errors.map((error) => error.status)).toEqual([401]);
    expect(router.url).toBe('/login');
  });

  it('token iz ranije verzije, bez tokena za obnavljanje, odjavljuje', async () => {
    localStorage.removeItem('strength-planner.refresh-token');

    send();
    refuse(http.expectOne('/api/sessions'));
    await settle();

    expect(storage.accessToken()).toBeNull();
  });

  it('ako i obnovljen token bude odbijen, odjavljuje umesto da se vrti', async () => {
    send();
    refuse(http.expectOne('/api/sessions'));
    grant(expectRefresh());
    await settle();
    refuse(http.expectOne('/api/sessions'));
    await settle();

    expect(signedIn()).toBe(false);
  });

  it('token koji je drugi tab već obnovio koristi se bez novog obnavljanja', async () => {
    send();
    const first = http.expectOne('/api/sessions');
    otherTabStored(renewed, 'refresh-2');
    refuse(first);
    await settle();

    const repeated = http.expectOne('/api/sessions');
    expect(repeated.request.headers.get('Authorization')).toBe(`Bearer ${renewed}`);
    repeated.flush([]);
  });

  it('zahtev se ne ponavlja tuđim tokenom kada se u međuvremenu prijavio drugi nalog', async () => {
    const outcome = send();
    const first = http.expectOne('/api/sessions');
    otherTabStored(tokenFor('neko-drugi', 1), 'refresh-drugog');
    refuse(first);
    await settle();

    expect(outcome.errors.map((error) => error.status)).toEqual([401]);
    // Prijava drugog naloga ostaje.
    expect(storage.refreshToken()).toBe('refresh-drugog');
  });

  it('odjava tokom obnavljanja ne biva poništena odgovorom koji stigne posle', async () => {
    send();
    refuse(http.expectOne('/api/sessions'));
    const refresh = expectRefresh();

    TestBed.inject(AuthService).logout();
    http.expectOne((request) => request.url.endsWith('/auth/logout')).flush(null);
    grant(refresh);
    await settle();

    expect(storage.accessToken()).toBeNull();
    expect(storage.refreshToken()).toBeNull();
  });

  it('401 na prijavi ostaje poruka o pogrešnim podacima', () => {
    const errors: number[] = [];
    client.post('/api/auth/login', {}).subscribe({ error: (error) => errors.push(error.status) });
    refuse(http.expectOne('/api/auth/login'));

    expect(errors).toEqual([401]);
    expect(signedIn()).toBe(true);
  });

  it.each([500, 0])(
    'ponovljen zahtev koji padne iz drugog razloga (%s) ne odjavljuje',
    async (status) => {
      const outcome = send();
      refuse(http.expectOne('/api/sessions'));
      grant(expectRefresh());
      await settle();
      const repeated = http.expectOne('/api/sessions');
      if (status === 0) {
        repeated.error(new ProgressEvent('error'));
      } else {
        repeated.flush(null, { status, statusText: 'x' });
      }

      expect(signedIn()).toBe(true);
      expect(outcome.errors.map((error) => error.status)).toEqual([status]);
    },
  );

  it('zakasneo 401 na ponovljen zahtev ne gasi noviju prijavu', async () => {
    send();
    refuse(http.expectOne('/api/sessions'));
    grant(expectRefresh());
    await settle();
    const repeated = http.expectOne('/api/sessions');

    otherTabStored(tokenFor('lifter', 3), 'refresh-3');
    refuse(repeated);

    expect(storage.refreshToken()).toBe('refresh-3');
  });

  it('odbijeno obnavljanje posle kog već postoji nov par ponavlja zahtev njime', async () => {
    send();
    refuse(http.expectOne('/api/sessions'));
    const refresh = expectRefresh();

    // Na primer promena lozinke u drugom tabu: stara prijava je ugašena, nova već sačuvana.
    otherTabStored(tokenFor('lifter', 3), 'refresh-3');
    refuse(refresh);
    await settle();

    const repeated = http.expectOne('/api/sessions');
    expect(repeated.request.headers.get('Authorization')).toBe(`Bearer ${tokenFor('lifter', 3)}`);
    repeated.flush([]);
    expect(signedIn()).toBe(true);
  });

  it('odbijeno staro obnavljanje ne odjavljuje nalog koji se u međuvremenu prijavio', async () => {
    send();
    refuse(http.expectOne('/api/sessions'));
    const refresh = expectRefresh();

    otherTabStored(tokenFor('neko-drugi', 1), 'refresh-drugog');
    refuse(refresh);
    await settle();

    expect(storage.refreshToken()).toBe('refresh-drugog');
  });

  it('401 na zahtev poslat bez tokena vodi na prijavu', async () => {
    storage.clear();

    send();
    refuse(http.expectOne('/api/sessions'));
    await settle();

    expect(router.url).toBe('/login');
  });

  // --- brava među tabovima ---------------------------------------------------

  /**
   * jsdom nema Web Locks, pa se podmeće brava koja radi ono što prava: jedan po jedan, a čekanje
   * se može prekinuti signalom.
   */
  function fakeLocks() {
    const names: string[] = [];
    let queue: Promise<unknown> = Promise.resolve();
    const request = (
      name: string,
      options: { signal: AbortSignal },
      work: () => Promise<string>,
    ): Promise<string> => {
      names.push(name);
      const ahead = queue;
      const turn = new Promise<string>((resolve, reject) => {
        options.signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')));
        void ahead.then(() => {
          if (!options.signal.aborted) {
            work().then(resolve, reject);
          }
        });
      });
      // Ko odustane od čekanja ne oslobađa bravu: sledeći i dalje čeka one ispred.
      queue = Promise.all([ahead, turn.catch(() => undefined)]);
      return turn;
    };

    Object.defineProperty(navigator, 'locks', { configurable: true, value: { request } });

    return {
      names,
      /** Drži bravu kao drugi tab, dok se ne pusti. */
      holdByAnotherTab(): () => void {
        let release!: () => void;
        const held = new Promise<void>((resolve) => (release = resolve));
        queue = queue.then(() => held);
        return release;
      },
      remove: () => delete (navigator as { locks?: unknown }).locks,
    };
  }

  describe('sa Web Locks', () => {
    let locks: ReturnType<typeof fakeLocks>;

    beforeEach(() => (locks = fakeLocks()));
    afterEach(() => locks.remove());

    it('obnavljanje čeka bravu i ne šalje ništa ako je drugi tab već obnovio', async () => {
      const release = locks.holdByAnotherTab();
      send();
      refuse(http.expectOne('/api/sessions'));
      await settle();
      http.expectNone((request) => request.url.endsWith('/auth/refresh'));

      otherTabStored(renewed, 'refresh-2');
      release();
      await settle();

      const repeated = http.expectOne('/api/sessions');
      expect(repeated.request.headers.get('Authorization')).toBe(`Bearer ${renewed}`);
      repeated.flush([]);
      expect(locks.names).toEqual(['strength-planner.session-refresh']);
    });

    it('obnavljanje pod bravom šalje se i čuva kao i bez nje', async () => {
      send();
      refuse(http.expectOne('/api/sessions'));
      await settle();
      grant(expectRefresh());
      await settle();

      http.expectOne('/api/sessions').flush([]);
      expect(storage.refreshToken()).toBe('refresh-2');
      expect(locks.names).toHaveLength(1);
    });

    it('brava koja se ne oslobađa napušta se posle roka, bez odjave', async () => {
      vi.useFakeTimers();
      try {
        locks.holdByAnotherTab();
        const outcome = send();
        refuse(http.expectOne('/api/sessions'));

        await vi.advanceTimersByTimeAsync(REFRESH_LOCK_WAIT_MS + 1);

        expect(outcome.errors.map((error) => error.status)).toEqual([0]);
        expect(signedIn()).toBe(true);
        // Sledeći pokušaj ne nasleđuje napušteno čekanje.
        const again = send();
        refuse(http.expectOne('/api/sessions'));
        await vi.advanceTimersByTimeAsync(REFRESH_LOCK_WAIT_MS + 1);
        expect(again.errors).toHaveLength(1);
        expect(locks.names).toHaveLength(2);
      } finally {
        vi.useRealTimers();
      }
    });

    it('dva obnavljača, kao dva taba, šalju jedan zahtev', async () => {
      const first = TestBed.inject(SessionRefresher);
      const second = TestBed.runInInjectionContext(() => new SessionRefresher());
      const tokens: string[] = [];

      first.refresh(stale).subscribe((token) => tokens.push(token));
      second.refresh(stale).subscribe((token) => tokens.push(token));
      await settle();
      grant(expectRefresh());
      await settle();

      expect(tokens).toEqual([renewed, renewed]);
    });
  });

  // --- drugi tabovi ----------------------------------------------------------

  function otherTabWrote(key: string | null = 'strength-planner.token'): void {
    window.dispatchEvent(new StorageEvent('storage', { key }));
  }

  it('obnavljanje u drugom tabu ne dira ovaj', () => {
    localStorage.setItem('strength-planner.token', renewed);

    otherTabWrote();

    expect(storage.token()).toBe(renewed);
    expect(reloads).toBe(0);
  });

  it('odjava u drugom tabu odjavljuje i ovaj, bez poziva serveru', async () => {
    localStorage.clear();

    otherTabWrote(null);
    await settle();

    expect(storage.token()).toBeNull();
    expect(router.url).toBe('/login');
  });

  it('prijava drugog naloga u drugom tabu ponovo učitava aplikaciju', () => {
    localStorage.setItem('strength-planner.token', tokenFor('neko-drugi', 1));

    otherTabWrote();

    expect(reloads).toBe(1);
  });

  it('upis drugih ključeva se zanemaruje', () => {
    localStorage.setItem('strength-planner.token', tokenFor('neko-drugi', 1));

    otherTabWrote('strength-planner.theme');

    expect(reloads).toBe(0);
  });
});
