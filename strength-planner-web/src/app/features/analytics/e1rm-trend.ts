import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AnalyticsService } from '../../core/api/analytics.service';
import { ExerciseService } from '../../core/api/exercise.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { E1rmPointDto } from '../../core/models/analytics.models';
import { LineChart } from '../../shared/components/line-chart/line-chart';

type TrendState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; points: E1rmPointDto[] };

@Component({
  selector: 'app-e1rm-trend',
  imports: [LineChart],
  templateUrl: './e1rm-trend.html',
  styleUrl: './e1rm-trend.scss',
})
export class E1rmTrend {
  private readonly exerciseService = inject(ExerciseService);
  private readonly analyticsService = inject(AnalyticsService);

  protected readonly exercises = this.exerciseService.exercises;
  protected readonly loadingExercises = signal(true);
  protected readonly exercisesError = signal<string | null>(null);
  protected readonly selectedId = signal<string>('');

  private readonly state = toSignal(
    toObservable(this.selectedId).pipe(
      switchMap((id) => {
        if (!id) {
          return of<TrendState>({ status: 'ready', points: [] });
        }
        return this.analyticsService.e1rmTrend(id).pipe(
          map((points): TrendState => ({ status: 'ready', points })),
          startWith<TrendState>({ status: 'loading' }),
          catchError((err: unknown) =>
            of<TrendState>({
              status: 'error',
              message: extractErrorMessage(err, 'Ne mogu da učitam trend. Pokušaj ponovo.'),
            }),
          ),
        );
      }),
    ),
    { initialValue: { status: 'loading' } as TrendState },
  );

  private readonly points = computed<E1rmPointDto[]>(() => {
    const s = this.state();
    return s.status === 'ready' ? s.points : [];
  });

  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly error = computed(() => {
    const s = this.state();
    return s.status === 'error' ? s.message : null;
  });

  protected readonly labels = computed(() => this.points().map((p) => formatShort(p.recordedAt)));
  protected readonly values = computed(() => this.points().map((p) => Number(p.valueKg)));
  protected readonly hasData = computed(() => this.values().length > 0);

  protected readonly last = computed(() => this.values().at(-1) ?? null);
  protected readonly delta = computed(() => {
    const values = this.values();
    if (values.length < 2) {
      return null;
    }
    return round1(values[values.length - 1] - values[0]);
  });

  constructor() {
    this.loadExercises();
  }

  protected loadExercises(): void {
    this.loadingExercises.set(true);
    this.exercisesError.set(null);

    this.exerciseService.load().subscribe({
      next: (exercises) => {
        this.loadingExercises.set(false);
        if (exercises.length > 0 && !this.selectedId()) {
          this.selectedId.set(exercises[0].id);
        }
      },
      error: (err: unknown) => {
        this.loadingExercises.set(false);
        this.exercisesError.set(
          extractErrorMessage(err, 'Ne mogu da učitam vežbe. Pokušaj ponovo.'),
        );
      },
    });
  }

  protected select(id: string): void {
    this.selectedId.set(id);
  }
}

function formatShort(iso: string): string {
  const date = new Date(iso);
  return `${date.getDate()}.${date.getMonth() + 1}.`;
}

function round1(value: number): number {
  return Math.round(value * 10) / 10;
}
