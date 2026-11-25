import { TestBed } from '@angular/core/testing';
import { RestTimerService, formatClock } from './rest-timer.service';

const STORAGE_KEY = 'strength-planner.rest-target';

/**
 * Tajmer pauze računa vreme iz sata zida, a ne brojanjem otkucaja, pa se proverava upravo to:
 * pozadinska kartica koja je „spavala" mora da pokaže pravu pauzu kad se vrati.
 */
describe('RestTimerService', () => {
  let vibrate: ReturnType<typeof vi.fn>;

  function create(): RestTimerService {
    return TestBed.inject(RestTimerService);
  }

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-25T10:00:00Z'));
    localStorage.removeItem(STORAGE_KEY);
    vibrate = vi.fn(() => true);
    Object.defineProperty(navigator, 'vibrate', { value: vibrate, configurable: true });
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.removeItem(STORAGE_KEY);
    Reflect.deleteProperty(navigator, 'vibrate');
  });

  it('ne radi dok se ne pokrene, i ne broji', () => {
    const timer = create();

    expect(timer.running()).toBe(false);
    expect(timer.elapsedSeconds()).toBe(0);
    expect(timer.reached()).toBe(false);
  });

  it('broji sekunde od početka pauze', () => {
    const timer = create();

    timer.start();
    vi.advanceTimersByTime(65_000);

    expect(timer.running()).toBe(true);
    expect(timer.elapsedSeconds()).toBe(65);
  });

  it('pokazuje pravu pauzu kad kartica „prespava" otkucaje', () => {
    const timer = create();
    timer.start();

    // Pregledač je zaustavio interval, a sat zida je otišao dva minuta napred.
    vi.setSystemTime(new Date('2026-11-25T10:02:00Z'));
    document.dispatchEvent(new Event('visibilitychange'));

    expect(timer.elapsedSeconds()).toBe(120);
  });

  it('nova pauza kreće od nule', () => {
    const timer = create();
    timer.start();
    vi.advanceTimersByTime(40_000);

    timer.start();

    expect(timer.elapsedSeconds()).toBe(0);
  });

  it('stop zaustavlja brojanje i čisti interval', () => {
    const timer = create();
    timer.start();
    vi.advanceTimersByTime(5_000);

    timer.stop();

    expect(timer.running()).toBe(false);
    expect(timer.elapsedSeconds()).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('uništen servis ne ostavlja otkucaj za sobom', () => {
    const timer = create();
    timer.start();
    expect(vi.getTimerCount()).toBe(1);

    TestBed.resetTestingModule();

    expect(vi.getTimerCount()).toBe(0);
    expect(timer.running()).toBe(true);
  });

  describe('cilj pauze', () => {
    it('pamti se u pregledaču i vraća u novoj instanci', () => {
      create().setTarget(120);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('120');

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({});

      expect(create().targetSeconds()).toBe(120);
    });

    it('vrednost koja nije među ponuđenim se odbacuje, i uskladištena i uneta', () => {
      localStorage.setItem(STORAGE_KEY, '77');
      const timer = create();

      expect(timer.targetSeconds()).toBe(0);

      timer.setTarget(45);
      expect(timer.targetSeconds()).toBe(0);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('77');
    });

    it('prostor za pamćenje koji baca grešku ne ruši tajmer', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('blokirano');
      });
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('blokirano');
      });
      const timer = create();

      expect(timer.targetSeconds()).toBe(0);
      expect(() => timer.setTarget(180)).not.toThrow();
      expect(timer.targetSeconds()).toBe(180);
    });
  });

  describe('kad se cilj dostigne', () => {
    it('javlja da je pauza istekla i vibrira jednom', () => {
      const timer = create();
      timer.setTarget(90);
      timer.start();

      vi.advanceTimersByTime(89_000);
      expect(timer.reached()).toBe(false);
      expect(vibrate).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1_000);
      expect(timer.reached()).toBe(true);
      expect(vibrate).toHaveBeenCalledTimes(1);

      // Dalje otkucaje ne prate nove vibracije, a brojanje se nastavlja.
      vi.advanceTimersByTime(30_000);
      expect(vibrate).toHaveBeenCalledTimes(1);
      expect(timer.elapsedSeconds()).toBe(120);
    });

    it('bez cilja nikad nije istekla i ne vibrira', () => {
      const timer = create();
      timer.start();

      vi.advanceTimersByTime(600_000);

      expect(timer.reached()).toBe(false);
      expect(vibrate).not.toHaveBeenCalled();
    });

    it('isti cilj izabran ponovo ne ponavlja vibraciju, a novi cilj može da je izazove', () => {
      const timer = create();
      timer.setTarget(90);
      timer.start();
      vi.advanceTimersByTime(95_000);
      expect(vibrate).toHaveBeenCalledTimes(1);

      timer.setTarget(90);
      expect(vibrate).toHaveBeenCalledTimes(1);

      // Cilj koji je dalje od pauze ne vibrira; vraćanje na onaj koji je već prošao vibrira opet.
      timer.setTarget(120);
      expect(timer.reached()).toBe(false);
      expect(vibrate).toHaveBeenCalledTimes(1);

      timer.setTarget(90);
      expect(timer.reached()).toBe(true);
      expect(vibrate).toHaveBeenCalledTimes(2);
    });

    it('nova pauza opet vibrira kad dostigne cilj', () => {
      const timer = create();
      timer.setTarget(90);
      timer.start();
      vi.advanceTimersByTime(90_000);
      expect(vibrate).toHaveBeenCalledTimes(1);

      timer.start();
      vi.advanceTimersByTime(90_000);

      expect(vibrate).toHaveBeenCalledTimes(2);
    });

    it('uređaj bez vibracije, ili onaj koji je odbije, ne ruši tajmer', () => {
      Reflect.deleteProperty(navigator, 'vibrate');
      const timer = create();
      timer.setTarget(90);
      timer.start();

      expect(() => vi.advanceTimersByTime(90_000)).not.toThrow();
      expect(timer.reached()).toBe(true);

      Object.defineProperty(navigator, 'vibrate', {
        value: () => {
          throw new Error('odbijeno');
        },
        configurable: true,
      });
      timer.start();

      expect(() => vi.advanceTimersByTime(90_000)).not.toThrow();
    });
  });
});

describe('formatClock', () => {
  it.each([
    [0, '0:00'],
    [5, '0:05'],
    [59, '0:59'],
    [60, '1:00'],
    [90.9, '1:30'],
    [3725, '62:05'],
    [-4, '0:00'],
  ])('%s s -> %s', (seconds, expected) => {
    expect(formatClock(seconds)).toBe(expected);
  });
});
