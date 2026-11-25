import { Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { extractErrorMessage } from '../../core/api/http-error';
import { HistoryService } from '../../core/api/history.service';
import { HistoryEntryDto } from '../../core/models/training.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';
import { SubscreenHeader } from '../../shared/components/subscreen-header/subscreen-header';
import { exercisesLabel, groupByMonth, tonnageLabel, whenLabel } from './history-format';

const PAGE_SIZE = 20;

/**
 * Istorija treninga: svaki završen ili preskočen trening kroz sve planove i blokove.
 *
 * Analitika čita jedan blok, a ekran plana tekući, pa je blok koji se završio nestajao iz
 * vida. Ovde se nikad ne briše ništa osim brisanjem plana. Dodir na trening otvara ga kao i
 * svaki drugi, uključujući ispravku ako je još moguća.
 */
@Component({
  selector: 'app-training-history',
  imports: [MatIconModule, EmptyState, Loading, SubscreenHeader],
  templateUrl: './training-history.html',
  styleUrl: './training-history.scss',
})
export class TrainingHistory {
  private readonly historyService = inject(HistoryService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly loadingMore = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly entries = signal<HistoryEntryDto[]>([]);
  protected readonly total = signal(0);
  private page = 0;

  protected readonly months = computed(() => groupByMonth(this.entries()));
  protected readonly hasMore = computed(() => this.entries().length < this.total());

  protected readonly when = whenLabel;
  protected readonly tonnage = tonnageLabel;
  protected readonly exercises = exercisesLabel;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.page = 0;
    this.entries.set([]);
    this.fetchNext(() => this.loading.set(false));
  }

  protected loadMore(): void {
    if (this.loadingMore()) {
      return;
    }

    this.loadingMore.set(true);
    this.fetchNext(() => this.loadingMore.set(false));
  }

  private fetchNext(done: () => void): void {
    const next = this.page + 1;

    this.historyService.page(next, PAGE_SIZE).subscribe({
      next: (result) => {
        this.page = next;
        this.total.set(result.total);
        this.entries.update((current) => [...current, ...result.items]);
        done();
      },
      error: (err: unknown) => {
        done();
        this.error.set(extractErrorMessage(err, 'Ne mogu da učitam istoriju. Proveri vezu i pokušaj ponovo.'));
      },
    });
  }

  protected open(entry: HistoryEntryDto): void {
    void this.router.navigate(['/session', entry.sessionId]);
  }

  protected back(): void {
    void this.router.navigateByUrl('/profile');
  }
}
