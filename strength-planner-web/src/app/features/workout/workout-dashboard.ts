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
  protected readonly deleting = signal(false);
  protected readonly confirmingDelete = signal(false);

  protected readonly mesocycle = this.mesocycleService.active;

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
      default:
        return { label: 'Planirano', tone: 'neutral' };
    }
  }

  protected openCreate(): void {
    void this.router.navigateByUrl('/mesocycle');
  }

  protected requestDelete(): void {
    this.confirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  protected confirmDelete(): void {
    const plan = this.mesocycle();
    if (!plan || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.mesocycleService.delete(plan.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        this.error.set(extractErrorMessage(err, 'Brisanje nije uspelo. Pokušaj ponovo.'));
      },
    });
  }
}
