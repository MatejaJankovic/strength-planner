import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, map } from 'rxjs';
import { MatIconModule } from '@angular/material/icon';
import { Loading } from '../../shared/components/loading/loading';
import { extractErrorMessage } from '../../core/api/http-error';
import { ExerciseService } from '../../core/api/exercise.service';
import { OneRepMaxService } from '../../core/api/one-rep-max.service';
import { REGISTRATION_STEP_COUNT } from '../../core/models/auth.models';
import { ExerciseDto } from '../../core/models/training.models';
import { WizardShell } from '../../shared/components/wizard-shell/wizard-shell';

const MAIN_LIFT_NAMES = ['Back Squat', 'Bench Press', 'Deadlift', 'Overhead Press', 'Barbell Row'];
const STEP = 2.5;
const MIN = 0;
const MAX = 500;

interface LiftRow {
  exerciseId: string;
  name: string;
  savedValueKg: number | null;
}

@Component({
  selector: 'app-one-rep-max-setup',
  imports: [FormsModule, MatIconModule, DecimalPipe, NgTemplateOutlet, Loading, WizardShell],
  templateUrl: './one-rep-max-setup.html',
  styleUrl: './one-rep-max-setup.scss',
})
export class OneRepMaxSetup {
  /**
   * Napomena da unos nije obavezan. Jedan string, jer ga čitaju oba prikaza ekrana —
   * ranije je isto rečenica stajala i ovde i u čarobnjaku, i dve kopije su se već
   * razišle po crtici.
   */
  protected readonly optionalNote =
    'Ne moraš uneti sve - vežbe bez 1RM prvi put loguješ po osećaju.';

  protected readonly stepCount = REGISTRATION_STEP_COUNT;
  protected readonly stepIndex = REGISTRATION_STEP_COUNT - 1;

  private readonly exerciseService = inject(ExerciseService);
  private readonly oneRepMaxService = inject(OneRepMaxService);
  private readonly router = inject(Router);

  /**
   * Da li je ekran poslednji korak registracije ili samostalna izmena maksimuma.
   *
   * Isti ekran služi za oba: čarobnjak ga otvara sa `?wizard=1` i tada nosi traku
   * napretka i dugme „Nastavi na plan", a sa profila se otvara bez parametra i tada nosi
   * svoje zaglavlje. Bez toga bi vraćeni korisnik čitao „Korak 8 od 8" nad ekranom koji
   * nije deo nikakve registracije.
   */
  protected readonly inWizard = toSignal(
    inject(ActivatedRoute).queryParamMap.pipe(map((params) => params.get('wizard') === '1')),
    { initialValue: false },
  );

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly savingId = signal<string | null>(null);
  protected readonly saveError = signal<string | null>(null);

  private readonly extraIds = signal<string[]>([]);
  private readonly drafts = signal<Record<string, number>>({});
  protected readonly exerciseToAdd = signal<string>('');

  private readonly exercises = this.exerciseService.exercises;
  private readonly oneRepMaxes = this.oneRepMaxService.oneRepMaxes;

  protected readonly rows = computed<LiftRow[]>(() => {
    const catalog = this.exercises();
    const byId = new Map(catalog.map((exercise) => [exercise.id, exercise]));
    const ormByExercise = new Map(this.oneRepMaxes().map((orm) => [orm.exerciseId, orm]));

    const ordered: { id: string; name: string }[] = [];
    const seen = new Set<string>();
    const push = (id: string, name: string) => {
      if (!seen.has(id)) {
        ordered.push({ id, name });
        seen.add(id);
      }
    };

    // 1) Main compound lifts, in a deliberate order.
    for (const name of MAIN_LIFT_NAMES) {
      const match = catalog.find((exercise) => normalize(exercise.name) === normalize(name));
      if (match) {
        push(match.id, match.name);
      }
    }
    // 2) Anything the athlete already has a 1RM for.
    for (const orm of this.oneRepMaxes()) {
      push(orm.exerciseId, byId.get(orm.exerciseId)?.name ?? orm.exercise);
    }
    // 3) Extra lifts the athlete added from the catalog.
    for (const id of this.extraIds()) {
      const exercise = byId.get(id);
      if (exercise) {
        push(exercise.id, exercise.name);
      }
    }

    return ordered.map((entry) => ({
      exerciseId: entry.id,
      name: entry.name,
      savedValueKg: ormByExercise.get(entry.id)?.valueKg ?? null,
    }));
  });

  protected readonly savedCount = computed(
    () => this.rows().filter((row) => row.savedValueKg !== null).length,
  );

  protected readonly addableExercises = computed<ExerciseDto[]>(() => {
    const shown = new Set(this.rows().map((row) => row.exerciseId));
    return this.exercises()
      .filter((exercise) => !shown.has(exercise.id))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      exercises: this.exerciseService.load(),
      oneRepMaxes: this.oneRepMaxService.load(),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam vežbe. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected valueOf(row: LiftRow): number {
    return this.drafts()[row.exerciseId] ?? row.savedValueKg ?? 0;
  }

  protected isDirty(row: LiftRow): boolean {
    return this.valueOf(row) !== (row.savedValueKg ?? 0);
  }

  protected canSave(row: LiftRow): boolean {
    return this.valueOf(row) > 0 && this.isDirty(row) && this.savingId() !== row.exerciseId;
  }

  protected step(row: LiftRow, direction: 1 | -1): void {
    const next = clamp(roundToStep(this.valueOf(row) + direction * STEP), MIN, MAX);
    this.setDraft(row.exerciseId, next);
  }

  protected onInput(row: LiftRow, raw: string): void {
    const parsed = Number(raw);
    if (Number.isNaN(parsed)) {
      return;
    }
    this.setDraft(row.exerciseId, clamp(roundToHalf(parsed), MIN, MAX));
  }

  protected save(row: LiftRow): void {
    if (!this.canSave(row)) {
      return;
    }

    this.savingId.set(row.exerciseId);
    this.saveError.set(null);

    this.oneRepMaxService.save({ exerciseId: row.exerciseId, valueKg: this.valueOf(row) }).subscribe({
      next: () => {
        this.savingId.set(null);
        // Drop the draft so the row reflects the freshly saved value.
        this.clearDraft(row.exerciseId);
      },
      error: (err: unknown) => {
        this.savingId.set(null);
        this.saveError.set(
          extractErrorMessage(err, `„${row.name}" nije sačuvan. Pokušaj ponovo.`),
        );
      },
    });
  }

  protected addExercise(): void {
    const id = this.exerciseToAdd();
    if (!id) {
      return;
    }
    this.extraIds.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
    this.exerciseToAdd.set('');
  }

  protected continueToPlan(): void {
    void this.router.navigateByUrl('/plan');
  }

  private setDraft(exerciseId: string, value: number): void {
    this.drafts.update((drafts) => ({ ...drafts, [exerciseId]: value }));
  }

  private clearDraft(exerciseId: string): void {
    this.drafts.update((drafts) => {
      const next = { ...drafts };
      delete next[exerciseId];
      return next;
    });
  }
}

function normalize(name: string): string {
  return name.trim().toLowerCase();
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function roundToStep(value: number): number {
  return Math.round(value / STEP) * STEP;
}

function roundToHalf(value: number): number {
  return Math.round(value * 2) / 2;
}
