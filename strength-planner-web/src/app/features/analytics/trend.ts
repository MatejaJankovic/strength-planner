import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AnalyticsService } from '../../core/api/analytics.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { TrendDto } from '../../core/models/analytics.models';
import {
  TREND_RANGES,
  TrendMetric,
  TrendRow,
  metricFromKey,
  metricKey,
  trendRows,
} from './trend-rows';

type TrendState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; trend: TrendDto };

/**
 * Tonaža i serije po nedeljama kroz sve blokove. Pregled po bloku (nedeljni volumen i nedeljna
 * tonaža iznad) ostaje, jer cilj nedelje i granice volumena imaju smisla samo unutar bloka. Ovde
 * se vidi ono što se unutar bloka ne vidi: kako jedan blok stoji prema prethodnom, i gde je bio
 * deload ili pauza, pa se pad na granici bloka ne čita kao gubitak snage.
 */
@Component({
  selector: 'app-trend',
  imports: [DecimalPipe],
  templateUrl: './trend.html',
  styleUrl: './trend.scss',
})
export class Trend {
  private readonly analyticsService = inject(AnalyticsService);

  protected readonly ranges = TREND_RANGES;
  protected readonly range = signal(TREND_RANGES[1].weeks);
  private readonly metric = signal<TrendMetric>({ kind: 'tonnage' });
  protected readonly metricKey = computed(() => metricKey(this.metric()));

  private readonly state = toSignal(
    toObservable(this.range).pipe(
      switchMap((weeks) =>
        this.analyticsService.trend(weeks).pipe(
          map((trend): TrendState => ({ status: 'ready', trend })),
          startWith<TrendState>({ status: 'loading' }),
          catchError((err: unknown) =>
            of<TrendState>({
              status: 'error',
              message: extractErrorMessage(err, 'Ne mogu da učitam pregled kroz blokove. Pokušaj ponovo.'),
            }),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as TrendState },
  );

  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly error = computed(() => {
    const state = this.state();
    return state.status === 'error' ? state.message : null;
  });

  protected readonly muscles = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.trend.muscles : [];
  });

  protected readonly rows = computed<TrendRow[]>(() => {
    const state = this.state();
    return state.status === 'ready' ? trendRows(state.trend, this.metric()) : [];
  });

  protected readonly hasAnyTraining = computed(() => this.rows().some((row) => !row.isEmpty));

  protected readonly isTonnage = computed(() => this.metric().kind === 'tonnage');

  protected readonly total = computed(() => this.rows().reduce((sum, row) => sum + row.value, 0));

  protected selectRange(weeks: number): void {
    this.range.set(weeks);
  }

  protected selectMetric(key: string): void {
    this.metric.set(metricFromKey(key));
  }
}
