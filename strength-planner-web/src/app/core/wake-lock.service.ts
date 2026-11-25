import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';

/** Posle ovoliko vremena bez dodira ekran se otpušta, da zaboravljen trening ne troši bateriju do jutra. */
export const WAKE_LOCK_IDLE_MS = 15 * 60 * 1000;

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'touchstart'] as const;

/**
 * Drži ekran upaljenim dok traje trening.
 *
 * Telefon se između serija gasi sam, a trening se unosi sa telefona u ruci: ekran koji se ugasi
 * usred pauze traži otključavanje pred svaku seriju. Wake Lock API to sprečava, ali ga pregledač
 * sam otpušta kad se kartica sakrije (zaključan telefon, drugi program), i može da ga odbije
 * (štednja baterije, nebezbedan kontekst), pa servis:
 *
 * - zapamti da se ekran *želi* i ponovo ga traži kad kartica opet postane vidljiva;
 * - ne baca grešku kad API ne postoji ili je zahtev odbijen: trening radi i bez njega;
 * - ne traži drugi put dok prvi drži;
 * - otpušta ekran posle {@link WAKE_LOCK_IDLE_MS} bez ijednog dodira: trening koji niko nije završio
 *   (telefon spušten sa otvorenim ekranom) ne sme da drži ekran upaljenim satima. Prvi sledeći
 *   dodir ga ponovo traži.
 *
 * Radi samo u bezbednom kontekstu (HTTPS ili localhost), kao i instalacija na početni ekran.
 */
@Injectable({ providedIn: 'root' })
export class WakeLockService {
  private readonly document = inject(DOCUMENT);

  private sentinel: WakeLockSentinel | null = null;
  private wanted = false;
  private idle = false;
  private requesting = false;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly activeSignal = signal(false);
  /** Ekran se trenutno drži upaljenim. */
  readonly active = this.activeSignal.asReadonly();

  constructor() {
    const onVisibility = () => {
      if (this.wanted && !this.idle && this.document.visibilityState === 'visible') {
        void this.request();
      }
    };
    const onActivity = () => this.noteActivity();

    this.document.addEventListener('visibilitychange', onVisibility);
    for (const type of ACTIVITY_EVENTS) {
      this.document.addEventListener(type, onActivity, { passive: true });
    }

    // Slušaoci stoje na dokumentu, pa ih servis skida kad se uništi: inače bi svaka instanca (a u
    // testovima ih ima mnogo) ostala da reaguje na događaje koji joj ne pripadaju.
    inject(DestroyRef).onDestroy(() => {
      this.document.removeEventListener('visibilitychange', onVisibility);
      for (const type of ACTIVITY_EVENTS) {
        this.document.removeEventListener(type, onActivity);
      }
      this.clearIdleTimer();
    });
  }

  /** Počinje da drži ekran upaljenim; poziv dok je već držan ne radi ništa. */
  async hold(): Promise<void> {
    this.wanted = true;
    this.idle = false;
    this.armIdleTimer();
    await this.request();
  }

  /** Prestaje da ga drži. */
  async release(): Promise<void> {
    this.wanted = false;
    this.idle = false;
    this.clearIdleTimer();

    const sentinel = this.sentinel;
    this.sentinel = null;
    this.activeSignal.set(false);

    try {
      await sentinel?.release();
    } catch {
      // Već otpušten (npr. kartica je bila sakrivena): nema šta da se poništi.
    }
  }

  private noteActivity(): void {
    if (!this.wanted) {
      return;
    }

    this.armIdleTimer();

    if (this.idle) {
      this.idle = false;
      void this.request();
    }
  }

  private armIdleTimer(): void {
    this.clearIdleTimer();
    this.idleTimer = setTimeout(() => void this.becomeIdle(), WAKE_LOCK_IDLE_MS);
  }

  private clearIdleTimer(): void {
    if (this.idleTimer !== null) {
      clearTimeout(this.idleTimer);
      this.idleTimer = null;
    }
  }

  /** Niko nije dodirnuo ekran: ekran se otpušta, a namera ostaje, pa prvi dodir vraća zaključavanje. */
  private async becomeIdle(): Promise<void> {
    this.idleTimer = null;
    this.idle = true;

    const sentinel = this.sentinel;
    this.sentinel = null;
    this.activeSignal.set(false);

    try {
      await sentinel?.release();
    } catch {
      // Već otpušten.
    }
  }

  private async request(): Promise<void> {
    const wakeLock = this.document.defaultView?.navigator?.wakeLock;
    if (!wakeLock || this.sentinel || this.requesting || this.idle) {
      return;
    }

    this.requesting = true;
    try {
      const sentinel = await wakeLock.request('screen');

      // release() je mogao da stigne dok se čekalo: ekran se više ne želi, pa se odmah otpušta.
      if (!this.wanted || this.idle) {
        await sentinel.release();
        return;
      }

      this.sentinel = sentinel;
      this.activeSignal.set(true);
      sentinel.addEventListener('release', () => {
        // Pregledač ga je otpustio sam (kartica sakrivena). `wanted` ostaje, pa se ponovo traži
        // kad kartica opet bude vidljiva.
        if (this.sentinel !== sentinel) {
          return;
        }

        this.sentinel = null;
        this.activeSignal.set(false);

        // Događaji mogu da stignu obrnutim redom: „vidljiva" pre „otpušteno". Ako je kartica već
        // vidljiva, kad se otpuštanje konačno javi nema nikoga ko bi ponovo tražio.
        if (this.wanted && !this.idle && this.document.visibilityState === 'visible') {
          void this.request();
        }
      });
    } catch {
      // Odbijeno (štednja baterije, nema dozvole): ekran se gasi kao i do sada.
      this.activeSignal.set(false);
    } finally {
      this.requesting = false;
    }
  }
}
