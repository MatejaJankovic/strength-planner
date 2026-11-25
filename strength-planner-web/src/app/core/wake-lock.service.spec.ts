import { TestBed } from '@angular/core/testing';
import { WAKE_LOCK_IDLE_MS, WakeLockService } from './wake-lock.service';

/**
 * Ekran se drži upaljenim dok traje trening. Pregledač sam otpušta zaključavanje kad se kartica
 * sakrije i može da ga odbije, pa se proverava ono što lako pođe naopako: ponovno traženje kad
 * kartica postane vidljiva, odbijen zahtev, i otpuštanje dok se zahtev još čeka.
 */
describe('WakeLockService', () => {
  type FakeSentinel = EventTarget & { release: ReturnType<typeof vi.fn> };

  let sentinels: FakeSentinel[];
  let request: ReturnType<typeof vi.fn>;
  let service: WakeLockService;
  let visibility: DocumentVisibilityState;

  function fakeSentinel(): FakeSentinel {
    const target = new EventTarget() as FakeSentinel;
    target.release = vi.fn(async () => {
      target.dispatchEvent(new Event('release'));
    });
    return target;
  }

  function install(wakeLock: unknown): void {
    Object.defineProperty(navigator, 'wakeLock', { value: wakeLock, configurable: true });
  }

  function setVisibility(state: DocumentVisibilityState): void {
    visibility = state;
    document.dispatchEvent(new Event('visibilitychange'));
  }

  beforeEach(() => {
    sentinels = [];
    visibility = 'visible';
    request = vi.fn(async () => {
      const sentinel = fakeSentinel();
      sentinels.push(sentinel);
      return sentinel;
    });
    install({ request });
    vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visibility);

    TestBed.configureTestingModule({});
    service = TestBed.inject(WakeLockService);
  });

  afterEach(() => {
    vi.useRealTimers();
    // Servis skida svoje slušaoce kad se uništi: bez ovoga bi instance iz ranijih testova reagovale
    // na događaje u sledećem.
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    Reflect.deleteProperty(navigator, 'wakeLock');
  });

  it('traži ekran i javlja da je upaljen', async () => {
    await service.hold();

    expect(request).toHaveBeenCalledExactlyOnceWith('screen');
    expect(service.active()).toBe(true);
  });

  it('dva istovremena poziva traže zaključavanje jednom (drugi ne prepisuje prvo i ne curi)', async () => {
    await Promise.all([service.hold(), service.hold()]);

    expect(request).toHaveBeenCalledTimes(1);
    await service.release();
    // Da je drugi zahtev prošao, jedno zaključavanje bi ostalo neotpušteno posle release().
    expect(sentinels.every((sentinel) => sentinel.release.mock.calls.length > 0)).toBe(true);
  });

  it('drugi poziv dok je ekran držan ne traži ponovo', async () => {
    await service.hold();
    await service.hold();

    expect(request).toHaveBeenCalledTimes(1);
  });

  it('release otpušta zaključavanje i gasi oznaku', async () => {
    await service.hold();
    await service.release();

    expect(sentinels[0].release).toHaveBeenCalledTimes(1);
    expect(service.active()).toBe(false);
  });

  it('kad pregledač sam otpusti (kartica sakrivena), ponovo se traži čim kartica postane vidljiva', async () => {
    await service.hold();

    // Pregledač otpušta zaključavanje kad se kartica sakrije.
    setVisibility('hidden');
    sentinels[0].dispatchEvent(new Event('release'));
    expect(service.active()).toBe(false);
    expect(request).toHaveBeenCalledTimes(1);

    setVisibility('visible');
    await vi.waitFor(() => expect(request).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(service.active()).toBe(true));
  });

  it('otpuštanje koje stigne posle "vidljiva" (obrnut redosled događaja) ponovo traži', async () => {
    await service.hold();

    // Kartica je već vidljiva, a pregledač tek sada javlja da je zaključavanje otpušteno.
    sentinels[0].dispatchEvent(new Event('release'));

    await vi.waitFor(() => expect(request).toHaveBeenCalledTimes(2));
  });

  it('posle release se ne traži ponovo kad kartica postane vidljiva', async () => {
    await service.hold();
    await service.release();
    request.mockClear();

    setVisibility('hidden');
    setVisibility('visible');

    expect(request).not.toHaveBeenCalled();
  });

  it('sakrivena kartica ne traži zaključavanje (pregledač bi ga odbio)', async () => {
    await service.hold();
    sentinels[0].dispatchEvent(new Event('release'));
    request.mockClear();

    setVisibility('hidden');

    expect(request).not.toHaveBeenCalled();
  });

  it('odbijen zahtev ne baca grešku i ne javlja da je ekran upaljen', async () => {
    request.mockRejectedValueOnce(new DOMException('Štednja baterije', 'NotAllowedError'));

    await expect(service.hold()).resolves.toBeUndefined();

    expect(service.active()).toBe(false);
  });

  it('posle odbijenog zahteva sledeće vidljivo stanje pokušava ponovo', async () => {
    request.mockRejectedValueOnce(new DOMException('Štednja baterije', 'NotAllowedError'));
    await service.hold();

    setVisibility('visible');

    await vi.waitFor(() => expect(service.active()).toBe(true));
  });

  it('bez API-ja (nebezbedan kontekst ili stariji pregledač) trening radi, samo bez zaključavanja', async () => {
    Reflect.deleteProperty(navigator, 'wakeLock');

    await expect(service.hold()).resolves.toBeUndefined();
    await expect(service.release()).resolves.toBeUndefined();

    expect(service.active()).toBe(false);
  });

  it('release dok se zahtev još čeka otpušta ono što stigne, umesto da ostavi ekran upaljen', async () => {
    let resolve!: (sentinel: FakeSentinel) => void;
    request.mockImplementationOnce(
      () =>
        new Promise<FakeSentinel>((done) => {
          resolve = done;
        }),
    );

    const holding = service.hold();
    await service.release();

    const late = fakeSentinel();
    resolve(late);
    await holding;

    expect(late.release).toHaveBeenCalledTimes(1);
    expect(service.active()).toBe(false);
  });

  describe('rok neaktivnosti', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    function touch(): void {
      document.dispatchEvent(new Event('pointerdown'));
    }

    it('bez dodira ekran se otpušta posle roka, a namera ostaje', async () => {
      await service.hold();

      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS + 1);

      expect(sentinels[0].release).toHaveBeenCalledTimes(1);
      expect(service.active()).toBe(false);
    });

    it('dodir pre isteka roka ga produžava', async () => {
      await service.hold();

      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS - 1000);
      touch();
      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS - 1000);

      expect(service.active()).toBe(true);
      expect(sentinels[0].release).not.toHaveBeenCalled();
    });

    it('prvi dodir posle isteka roka ponovo traži ekran', async () => {
      await service.hold();
      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS + 1);
      expect(service.active()).toBe(false);

      touch();
      await vi.advanceTimersByTimeAsync(0);

      expect(request).toHaveBeenCalledTimes(2);
      expect(service.active()).toBe(true);
    });

    it('kartica koja postane vidljiva ne vraća ekran dok je rok istekao (to radi tek dodir)', async () => {
      await service.hold();
      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS + 1);
      request.mockClear();

      setVisibility('visible');
      await vi.advanceTimersByTimeAsync(0);

      expect(request).not.toHaveBeenCalled();
    });

    it('dodir bez namere (trening nije u toku) ništa ne traži', async () => {
      touch();
      await vi.advanceTimersByTimeAsync(0);

      expect(request).not.toHaveBeenCalled();
    });

    it('posle release nema tajmera koji bi kasnije otpustio ili tražio', async () => {
      await service.hold();
      await service.release();
      request.mockClear();

      await vi.advanceTimersByTimeAsync(WAKE_LOCK_IDLE_MS * 2);
      touch();
      await vi.advanceTimersByTimeAsync(0);

      expect(request).not.toHaveBeenCalled();
    });
  });
});
