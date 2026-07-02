import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AnalyticsService } from '../../core/api/analytics.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { WeeklyTonnageDto } from '../../core/models/analytics.models';
import { MesocycleSummaryDto } from '../../core/models/training.models';

type TonnageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; items: WeeklyTonnageDto[] };

interface TonnageRow extends WeeklyTonnageDto {
  barPct: number;
}

@Component({
  selector: 'app-tonnage',
  imports: [DecimalPipe],
  templateUrl: './tonnage.html',
  styleUrl: './tonnage.scss',
})
export class Tonnage {
  private readonly analyticsService = inject(AnalyticsService);

  readonly mesocycles = input<MesocycleSummaryDto[]>([]);

  private readonly chosenMesoId = signal<string | null>(null);
  protected readonly selectedMesoId = computed(
    () => this.chosenMesoId() ?? this.mesocycles()[0]?.id ?? null,
  );

  private readonly state = toSignal(
    toObservable(this.selectedMesoId).pipe(
      switchMap((id) => {
        if (!id) {
          return of<TonnageState>({ status: 'ready', items: [] });
        }
        return this.analyticsService.tonnage(id).pipe(
          map((items): TonnageState => ({ status: 'ready', items })),
          startWith<TonnageState>({ status: 'loading' }),
          catchError((err: unknown) =>
            of<TonnageState>({
              status: 'error',
              message: extractErrorMessage(err, 'Ne mogu da učitam tonažu. Pokušaj ponovo.'),
            }),
          ),
        );
      }),
    ),
    { initialValue: { status: 'loading' } as TonnageState },
  );

  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly error = computed(() => {
    const s = this.state();
    return s.status === 'error' ? s.message : null;
  });

  protected readonly rows = computed<TonnageRow[]>(() => {
    const s = this.state();
    if (s.status !== 'ready') {
      return [];
    }
    const max = Math.max(...s.items.map((item) => item.tonnageKg), 1);
    return s.items.map((item) => ({
      ...item,
      barPct: Math.round((item.tonnageKg / max) * 100),
    }));
  });

  protected readonly total = computed(() =>
    this.rows().reduce((sum, row) => sum + row.tonnageKg, 0),
  );

  protected readonly hasAnyTonnage = computed(() => this.rows().some((row) => row.tonnageKg > 0));

  protected selectMeso(id: string): void {
    this.chosenMesoId.set(id);
  }
}
