import { Component, inject, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { SessionService } from '../../core/api/session.service';
import { WorkoutSessionDto } from '../../core/models/training.models';
import { blockEditError } from './block-edit-error';

/**
 * Uklanjanje vežbe iz dana bloka koji već teče: iz ovog i iz svakog sledećeg treninga istog dana koji još
 * čeka, do kraja bloka. Traži potvrdu, jer se vraća samo dodavanjem vežbe iznova, a serije koje su već
 * upisane ne mogu da ostanu bez vežbe: server ih ne dozvoljava.
 */
@Component({
  selector: 'app-remove-exercise',
  imports: [MatIconModule],
  templateUrl: './remove-exercise.html',
  styleUrl: './remove-exercise.scss',
})
export class RemoveExercise {
  private readonly sessions = inject(SessionService);

  readonly planId = input.required<string>();
  readonly exerciseName = input.required<string>();
  readonly removed = output<WorkoutSessionDto>();

  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected ask(): void {
    this.confirming.set(true);
    this.error.set(null);
  }

  protected cancel(): void {
    this.confirming.set(false);
    this.error.set(null);
  }

  protected confirm(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);

    this.sessions.removeExercise(this.planId()).subscribe({
      next: (session) => {
        this.busy.set(false);
        this.confirming.set(false);
        this.removed.emit(session);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(blockEditError(err, 'Vežba nije uklonjena. Pokušaj ponovo.'));
      },
    });
  }
}
