import { Component, inject, input, output, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { SessionService } from '../../core/api/session.service';
import {
  AddableExerciseDto,
  AddableExercisesDto,
  WorkoutSessionDto,
} from '../../core/models/training.models';
import { blockEditError } from './block-edit-error';

let nextId = 0;

/**
 * Dodavanje vežbe u dan bloka koji već teče. Vežba se dodaje na kraj dana, u ovom i u svakom sledećem
 * treningu istog dana koji još čeka, do kraja bloka; prošlost se ne dira. Server odlučuje šta se sme
 * (trening koji čeka ili traje, vežba koja se radi sa opterećenjem, blok bez deload-a zbog umora), a
 * ovde se samo prikazuje ono što je ponudio, ili razlog zbog koga ne nudi ništa.
 */
@Component({
  selector: 'app-add-exercise',
  imports: [MatIconModule],
  templateUrl: './add-exercise.html',
  styleUrl: './add-exercise.scss',
})
export class AddExercise {
  private readonly sessions = inject(SessionService);

  /** Trening iz koga se dodaje: od njega do kraja bloka. */
  readonly sessionId = input.required<string>();
  readonly added = output<WorkoutSessionDto>();

  protected readonly selectId = `add-exercise-${nextId++}`;
  protected readonly open = signal(false);
  /** Ponuda je null dok se učitava. */
  protected readonly offer = signal<AddableExercisesDto | null>(null);
  protected readonly choice = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected openPicker(): void {
    this.open.set(true);
    this.offer.set(null);
    this.choice.set('');
    this.error.set(null);

    this.sessions.addableExercises(this.sessionId()).subscribe({
      next: (offer) => {
        // Odgovor za izbor koji je u međuvremenu zatvoren se odbacuje.
        if (!this.open()) {
          return;
        }
        this.offer.set(offer);
        this.choice.set(offer.exercises[0]?.exerciseId ?? '');
      },
      error: (err: unknown) => {
        if (!this.open()) {
          return;
        }
        this.offer.set({ exercises: [] });
        this.error.set(blockEditError(err, 'Ne mogu da učitam vežbe koje mogu da se dodaju.'));
      },
    });
  }

  protected cancel(): void {
    this.open.set(false);
    this.error.set(null);
  }

  protected label(option: AddableExerciseDto): string {
    return option.primaryMuscles.length > 0
      ? `${option.name} (${option.primaryMuscles.join(', ')})`
      : option.name;
  }

  protected confirm(): void {
    const exerciseId = this.choice();
    if (!exerciseId || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);

    this.sessions.addExercise(this.sessionId(), exerciseId).subscribe({
      next: (session) => {
        this.busy.set(false);
        this.open.set(false);
        this.added.emit(session);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.error.set(blockEditError(err, 'Vežba nije dodata. Pokušaj ponovo.'));
      },
    });
  }
}
