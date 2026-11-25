import { Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { REST_TARGETS_SECONDS, RestTimerService, formatClock } from './rest-timer.service';

/** Traka sa pauzom između serija: koliko traje, i do kada je cilj. */
@Component({
  selector: 'app-rest-timer',
  imports: [MatIconModule],
  templateUrl: './rest-timer.html',
  styleUrl: './rest-timer.scss',
})
export class RestTimer {
  private readonly timer = inject(RestTimerService);

  protected readonly targets = REST_TARGETS_SECONDS;
  protected readonly running = this.timer.running;
  protected readonly reached = this.timer.reached;
  protected readonly target = this.timer.targetSeconds;

  /**
   * Izbor cilja je skriven dok se ne zatraži: traka stoji zalepljena uz vrh ekrana, a cilj se
   * postavlja jednom i pamti, pa dugme sa ciljevima ne treba da zauzima prostor pred svaku seriju.
   */
  protected readonly choosing = signal(false);

  protected readonly clock = computed(() => formatClock(this.timer.elapsedSeconds()));
  protected readonly goal = computed(() =>
    this.target() > 0 ? formatClock(this.target()) : null,
  );

  protected label(seconds: number): string {
    return seconds === 0 ? 'Bez cilja' : formatClock(seconds);
  }

  protected toggleChoosing(): void {
    this.choosing.update((open) => !open);
  }

  /** Izabran cilj se odmah vidi uz sat, pa se izbor zatvara. */
  protected choose(seconds: number): void {
    this.timer.setTarget(seconds);
    this.choosing.set(false);
  }

  protected restart(): void {
    this.timer.start();
  }

  protected dismiss(): void {
    this.timer.stop();
  }
}
