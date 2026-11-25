import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** Ponuđeni ciljevi pauze u sekundama; 0 znači „bez cilja", samo broji. */
export const REST_TARGETS_SECONDS: readonly number[] = [0, 90, 120, 180, 300];

const STORAGE_KEY = 'strength-planner.rest-target';
const TICK_MS = 1000;
/** Vibracija kad pauza istekne: kratko, dvaput, da se oseti i u džepu. */
const REACHED_VIBRATION: readonly number[] = [200, 100, 200];

/** „1:05"; minuti nisu ograničeni na dve cifre, jer pauza od sat vremena je trening koji je stao. */
export function formatClock(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}

/**
 * Tajmer pauze između serija.
 *
 * Protekle sekunde se računaju iz sata zida (`Date.now()` u odnosu na početak), a ne brojanjem
 * otkucaja: pregledač usporava `setInterval` u pozadinskoj kartici, a telefon se između serija
 * upravo tako i ponaša. Otkucaj zato samo osvežava prikaz, a tačnost ne zavisi od njega.
 *
 * Cilj pauze se pamti u pregledaču (jedna ličnost po telefonu, nije podatak naloga). Kad se cilj
 * dostigne, tajmer jednom vibrira (ako uređaj to može) i nastavlja da broji, jer pauza koja je
 * prekoračena i dalje je podatak koji lifter želi da vidi.
 */
@Injectable({ providedIn: 'root' })
export class RestTimerService {
  private readonly document = inject(DOCUMENT);

  private interval: ReturnType<typeof setInterval> | null = null;
  private notified = false;

  private readonly startedAtSignal = signal<number | null>(null);
  private readonly nowSignal = signal(Date.now());
  private readonly targetSignal = signal(this.readTarget());

  readonly running = computed(() => this.startedAtSignal() !== null);
  readonly targetSeconds = this.targetSignal.asReadonly();

  readonly elapsedSeconds = computed(() => {
    const startedAt = this.startedAtSignal();
    return startedAt === null ? 0 : Math.max(0, Math.floor((this.nowSignal() - startedAt) / 1000));
  });

  /** Cilj je postavljen i pauza ga je dostigla. */
  readonly reached = computed(() => {
    const target = this.targetSignal();
    return this.running() && target > 0 && this.elapsedSeconds() >= target;
  });

  constructor() {
    const onVisibility = () => {
      if (this.running() && this.document.visibilityState === 'visible') {
        this.tick();
      }
    };

    this.document.addEventListener('visibilitychange', onVisibility);
    inject(DestroyRef).onDestroy(() => {
      this.document.removeEventListener('visibilitychange', onVisibility);
      this.clearInterval();
    });
  }

  /** Pauza počinje ispočetka: poziva se posle svake sačuvane serije. */
  start(): void {
    this.notified = false;
    this.nowSignal.set(Date.now());
    this.startedAtSignal.set(this.nowSignal());

    if (this.interval === null) {
      this.interval = setInterval(() => this.tick(), TICK_MS);
    }
  }

  stop(): void {
    this.clearInterval();
    this.notified = false;
    this.startedAtSignal.set(null);
  }

  setTarget(seconds: number): void {
    // Isti cilj ponovo ne sme da ponovi vibraciju: dodir na dugme koje je već izabrano nije novi cilj.
    if (!REST_TARGETS_SECONDS.includes(seconds) || seconds === this.targetSignal()) {
      return;
    }

    this.targetSignal.set(seconds);
    // Novi cilj važi i za pauzu koja je u toku, pa se obaveštenje ponovo dozvoljava.
    this.notified = false;
    this.writeTarget(seconds);
    this.tick();
  }

  private tick(): void {
    this.nowSignal.set(Date.now());

    if (this.reached() && !this.notified) {
      this.notified = true;
      this.vibrate();
    }
  }

  private vibrate(): void {
    try {
      this.document.defaultView?.navigator?.vibrate?.([...REACHED_VIBRATION]);
    } catch {
      // Uređaj ili pregledač ne dozvoljava vibraciju: ekran svakako prikazuje da je pauza istekla.
    }
  }

  private clearInterval(): void {
    if (this.interval !== null) {
      clearInterval(this.interval);
      this.interval = null;
    }
  }

  private readTarget(): number {
    try {
      const stored = Number(this.document.defaultView?.localStorage?.getItem(STORAGE_KEY));
      return REST_TARGETS_SECONDS.includes(stored) ? stored : 0;
    } catch {
      return 0;
    }
  }

  private writeTarget(seconds: number): void {
    try {
      this.document.defaultView?.localStorage?.setItem(STORAGE_KEY, String(seconds));
    } catch {
      // Privatni prozor ili blokiran prostor: cilj važi do kraja ove sesije.
    }
  }
}
