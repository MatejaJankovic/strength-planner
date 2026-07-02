import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { AnalyticsService } from '../../core/api/analytics.service';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { MesocycleSummaryDto } from '../../core/models/training.models';
import { PersonalRecordDto } from '../../core/models/analytics.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';
import { E1rmTrend } from './e1rm-trend';
import { Volume } from './volume';
import { PrList } from './pr-list';
import { Tonnage } from './tonnage';

@Component({
  selector: 'app-analytics-home',
  imports: [MatIconModule, EmptyState, Loading, E1rmTrend, Volume, PrList, Tonnage],
  templateUrl: './analytics-home.html',
  styleUrl: './analytics-home.scss',
})
export class AnalyticsHome {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly mesocycleService = inject(MesocycleService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly prs = signal<PersonalRecordDto[]>([]);
  protected readonly mesocycles = signal<MesocycleSummaryDto[]>([]);

  protected readonly isEmpty = computed(
    () => this.prs().length === 0 && this.mesocycles().length === 0,
  );

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      prs: this.analyticsService.personalRecords(),
      mesocycles: this.mesocycleService.list(),
    }).subscribe({
      next: ({ prs, mesocycles }) => {
        this.prs.set(prs);
        this.mesocycles.set(mesocycles);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam analitiku. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected goToWorkout(): void {
    void this.router.navigateByUrl('/workout');
  }
}
