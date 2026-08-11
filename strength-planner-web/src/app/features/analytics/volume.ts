import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AnalyticsService } from '../../core/api/analytics.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { VolumeItemDto } from '../../core/models/analytics.models';
import { MesocycleSummaryDto } from '../../core/models/training.models';

type VolumeState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: VolumeItemDto[] };

interface VolumeRow extends VolumeItemDto {
  barPct: number;
  mevPct: number;
  mrvPct: number;
}

@Component({
  selector: 'app-volume',
  imports: [DecimalPipe],
  templateUrl: './volume.html',
  styleUrl: './volume.scss',
})
export class Volume {
  private readonly analyticsService = inject(AnalyticsService);

  readonly mesocycles = input<MesocycleSummaryDto[]>([]);

  protected readonly weeks = [1, 2, 3, 4];
  protected readonly week = signal(1);

  protected readonly resetting = signal(false);
  protected readonly confirmingReset = signal(false);
  protected readonly resetError = signal<string | null>(null);
  protected readonly resetDone = signal(false);
  // Menja se posle reseta da bi se granice ponovo dovukle sa servera.
  private readonly reloadToken = signal(0);
  // Poslednji viđeni token: razlikuje osvežavanje posle reseta od promene nedelje.
  private lastReloadToken = 0;

  private readonly chosenMesoId = signal<string | null>(null);
  protected readonly selectedMesoId = computed(
    () => this.chosenMesoId() ?? this.mesocycles()[0]?.id ?? null,
  );

  private readonly state = toSignal(
    toObservable(
      computed(() => ({ id: this.selectedMesoId(), week: this.week(), token: this.reloadToken() })),
    ).pipe(
      switchMap(({ id, week, token }) => {
        if (!id) {
          return of<VolumeState>({ status: 'ready', items: [] });
        }
        const request = this.analyticsService.volume(id, week).pipe(
          map((items): VolumeState => ({ status: 'ready', items })),
          catchError((err: unknown) =>
            of<VolumeState>({
              status: 'error',
              message: extractErrorMessage(err, 'Ne mogu da učitam volumen. Pokušaj ponovo.'),
            }),
          ),
        );

        // Promena mezociklusa ili nedelje menja podatke, pa spinner ima smisla.
        // Osvežavanje posle reseta ne — inače lista, legenda i dugme nestanu na
        // trenutak, a korisnik ostane bez potvrde da se išta desilo.
        const isReload = token !== this.lastReloadToken;
        this.lastReloadToken = token;

        return isReload ? request : request.pipe(startWith<VolumeState>({ status: 'loading' }));
      }),
    ),
    { initialValue: { status: 'loading' } as VolumeState },
  );

  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly error = computed(() => {
    const s = this.state();
    return s.status === 'error' ? s.message : null;
  });

  protected readonly rows = computed<VolumeRow[]>(() => {
    const s = this.state();
    if (s.status !== 'ready') {
      return [];
    }
    return s.items.map((item) => {
      const sets = Number(item.sets);
      const scale = Math.max(sets, item.mrv) * 1.15 || 1;
      return {
        ...item,
        barPct: toPct(sets / scale),
        mevPct: toPct(item.mev / scale),
        mrvPct: toPct(item.mrv / scale),
      };
    });
  });

  /** Broj mišićnih grupa čije su granice pomerene u odnosu na seed. */
  protected readonly personalCount = computed(
    () => this.rows().filter((row) => row.isPersonal).length,
  );

  protected selectMeso(id: string): void {
    this.chosenMesoId.set(id);
  }

  protected selectWeek(week: number): void {
    this.week.set(week);
  }

  protected requestReset(): void {
    this.confirmingReset.set(true);
  }

  protected cancelReset(): void {
    this.confirmingReset.set(false);
  }

  protected confirmReset(): void {
    if (this.resetting()) {
      return;
    }

    this.resetting.set(true);
    this.resetError.set(null);

    this.analyticsService.resetVolumeLandmarks().subscribe({
      next: () => {
        this.resetting.set(false);
        this.confirmingReset.set(false);
        // Dugme nestaje zajedno sa naučenim granicama, pa je potvrda jedini trag
        // da se nešto desilo.
        this.resetDone.set(true);
        setTimeout(() => this.resetDone.set(false), 3200);
        this.reloadToken.update((token) => token + 1);
      },
      error: (err: unknown) => {
        this.resetting.set(false);
        this.resetError.set(
          extractErrorMessage(err, 'Vraćanje granica nije uspelo. Pokušaj ponovo.'),
        );
      },
    });
  }
}

function toPct(ratio: number): number {
  return Math.min(100, Math.max(0, ratio * 100));
}
