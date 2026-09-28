import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { Goal, WorkoutSessionDto } from '../../core/models/training.models';
import { StatChip, StatChipTone } from '../../shared/components/stat-chip/stat-chip';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

interface StatusMeta {
  label: string;
  tone: StatChipTone;
}

@Component({
  selector: 'app-workout-dashboard',
  imports: [RouterLink, DatePipe, DecimalPipe, MatIconModule, StatChip, EmptyState, Loading],
  templateUrl: './workout-dashboard.html',
  styleUrl: './workout-dashboard.scss',
})
export class WorkoutDashboard {
  private readonly mesocycleService = inject(MesocycleService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly mesocycle = this.mesocycleService.active;

  /**
   * Vežbe u ovom bloku koje nemaju od čega da izvedu opterećenje.
   *
   * Rad (slučaj korišćenja 4) kaže da sistem traži dopunu unosa kada za neku vežbu nema
   * procene 1RM. Do sada to nije tražio nigde — svaka takva vežba je tiho čekala da je
   * vežbač prvi put unese „po osećaju", što je legitiman put, ali je bio i jedini, i to
   * neizrečen.
   *
   * Gleda se generisan blok, a ne šablon: tek blok zna koje su vežbe zaista ušle i koja od
   * njih je ostala bez cilja. Vežbe koje diže sopstvena masa se ne broje — njima je nula
   * dodatih tačan prvi propis, a ne nepoznanica.
   */
  protected readonly exercisesWithoutLoad = computed<string[]>(() => {
    const plan = this.mesocycle();
    if (!plan) {
      return [];
    }

    const names = new Set<string>();
    for (const week of plan.weeks) {
      for (const session of week.sessions) {
        for (const item of session.exercisePlans) {
          if (item.targetWeightKg == null && !item.isBodyweight) {
            names.add(item.exerciseName);
          }
        }
      }
    }

    return [...names].sort((a, b) => a.localeCompare(b));
  });

  protected openOneRepMaxSetup(): void {
    void this.router.navigateByUrl('/onboarding');
  }

  // First not-yet-finished session across the whole plan (weeks then sessions in order).
  protected readonly nextSessionId = computed<string | null>(() => {
    const plan = this.mesocycle();
    if (!plan) {
      return null;
    }
    for (const week of plan.weeks) {
      for (const session of week.sessions) {
        if (session.status === 'Planned' || session.status === 'InProgress') {
          return session.id;
        }
      }
    }
    return null;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.mesocycleService.loadActive().subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);
        // 404 simply means "no active plan yet" — show the empty state, not an error.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          return;
        }
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam plan. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected goalLabel(goal: Goal): string {
    return goal === Goal.Strength ? 'Snaga' : 'Hipertrofija';
  }

  protected statusMeta(status: WorkoutSessionDto['status']): StatusMeta {
    switch (status) {
      case 'Completed':
        return { label: 'Završeno', tone: 'optimal' };
      case 'InProgress':
        return { label: 'U toku', tone: 'accent' };
      case 'Skipped':
        return { label: 'Preskočeno', tone: 'below' };
      default:
        return { label: 'Planirano', tone: 'neutral' };
    }
  }

  /**
   * Prazno stanje vodi na dugoročni plan, jedino mesto sa kog trening nastaje.
   * Ekran „Trening" sam ništa ne pravi niti briše: mezociklus je blok plana, pa bi
   * prekid usred plana ostavio blok bez svog treninga.
   */
  protected openPlan(): void {
    void this.router.navigateByUrl('/plan');
  }
}
