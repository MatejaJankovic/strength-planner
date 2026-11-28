import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PrivacyService } from '../../core/api/privacy.service';
import { AuthTokenStorage } from '../../core/auth/auth-token-storage';
import { AGE_MIN } from '../../core/models/auth.models';
import { PrivacyNoticeDto } from '../../core/models/privacy.models';
import { SubscreenHeader } from '../../shared/components/subscreen-header/subscreen-header';

/**
 * Politika privatnosti i napomena da aplikacija nije zdravstveni savet.
 *
 * Otvorena je i bez naloga, jer mora da se pročita pre registracije. Tekst stoji ovde; sa servera
 * stižu verzija i ko je rukovalac, da ime i adresa ne bi stajali u repozitorijumu. Svaka izmena
 * onoga šta se čuva, zašto, koliko dugo ili ko to dobija traži i novu verziju
 * (`PrivacyPolicy.Version` na serveru).
 */
@Component({
  selector: 'app-privacy-page',
  imports: [SubscreenHeader],
  templateUrl: './privacy-page.html',
  styleUrl: './privacy-page.scss',
})
export class PrivacyPage {
  private readonly privacy = inject(PrivacyService);
  private readonly tokens = inject(AuthTokenStorage);
  private readonly router = inject(Router);

  protected readonly minimumAge = AGE_MIN;

  protected readonly notice = signal<PrivacyNoticeDto | null>(null);
  protected readonly loadFailed = signal(false);

  /** `2026-11-28` kao `28. 11. 2026.`, bez pretvaranja u datum, koji bi vremenska zona mogla da pomeri. */
  protected readonly versionDate = computed(() => {
    const version = this.notice()?.version;
    if (!version) {
      return null;
    }

    const [year, month, day] = version.split('-').map(Number);
    return `${day}. ${month}. ${year}.`;
  });

  constructor() {
    this.privacy.notice().subscribe({
      next: (notice) => this.notice.set(notice),
      error: () => this.loadFailed.set(true),
    });
  }

  protected back(): void {
    void this.router.navigateByUrl(this.tokens.token() ? '/settings' : '/login');
  }
}
