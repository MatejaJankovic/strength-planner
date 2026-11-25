import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

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
 * - ne traži drugi put dok prvi drži.
 *
 * Radi samo u bezbednom kontekstu (HTTPS ili localhost), kao i instalacija na početni ekran.
 */
@Injectable({ providedIn: 'root' })
export class WakeLockService {
  private readonly document = inject(DOCUMENT);

  private sentinel: WakeLockSentinel | null = null;
  private wanted = false;
  private requesting = false;

  private readonly activeSignal = signal(false);
  /** Ekran se trenutno drži upaljenim. */
  readonly active = this.activeSignal.asReadonly();

  constructor() {
    this.document.addEventListener('visibilitychange', () => {
      if (this.wanted && this.document.visibilityState === 'visible') {
        void this.request();
      }
    });
  }

  /** Počinje da drži ekran upaljenim; poziv dok je već držan ne radi ništa. */
  async hold(): Promise<void> {
    this.wanted = true;
    await this.request();
  }

  /** Prestaje da ga drži. */
  async release(): Promise<void> {
    this.wanted = false;

    const sentinel = this.sentinel;
    this.sentinel = null;
    this.activeSignal.set(false);

    try {
      await sentinel?.release();
    } catch {
      // Već otpušten (npr. kartica je bila sakrivena): nema šta da se poništi.
    }
  }

  private async request(): Promise<void> {
    const wakeLock = this.document.defaultView?.navigator?.wakeLock;
    if (!wakeLock || this.sentinel || this.requesting) {
      return;
    }

    this.requesting = true;
    try {
      const sentinel = await wakeLock.request('screen');

      // release() je mogao da stigne dok se čekalo: ekran se više ne želi, pa se odmah otpušta.
      if (!this.wanted) {
        await sentinel.release();
        return;
      }

      this.sentinel = sentinel;
      this.activeSignal.set(true);
      sentinel.addEventListener('release', () => {
        // Pregledač ga je otpustio sam (kartica sakrivena). `wanted` ostaje, pa se ponovo traži
        // kad kartica opet bude vidljiva.
        if (this.sentinel === sentinel) {
          this.sentinel = null;
          this.activeSignal.set(false);
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
