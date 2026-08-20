import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { extractErrorMessage } from '../../core/api/http-error';
import { ExerciseService } from '../../core/api/exercise.service';
import { CreateExerciseRequest, ExerciseDto } from '../../core/models/training.models';
import { Loading } from '../../shared/components/loading/loading';

/**
 * Vežbe: ceo katalog, korak opterećenja po vežbi, i pravljenje sopstvenih vežbi.
 *
 * Do ove runde su to bile dve odvojene kartice na ekranu profila — „Moje vežbe" i „Korak
 * opterećenja" — koje su govorile o istoj stvari sa dva mesta: jedna je nabrajala samo
 * korisničke vežbe, druga sve. Ovde je jedan red po vežbi, pa se na njemu vidi i menja
 * sve što se o toj vežbi može reći.
 *
 * Praktična posledica spajanja: pre dodavanja nove vežbe vidi se da li već postoji, jer
 * je pretraga nad istim spiskom u koji se dodaje.
 */
@Component({
  selector: 'app-exercise-catalog',
  imports: [ReactiveFormsModule, MatIconModule, Loading],
  templateUrl: './exercise-catalog.html',
  styleUrl: './exercise-catalog.scss',
})
export class ExerciseCatalog {
  private readonly fb = inject(FormBuilder);
  private readonly exerciseService = inject(ExerciseService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly muscleGroups = signal<string[]>([]);

  // --- pretraga i spisak -----------------------------------------------------

  protected readonly filter = signal('');

  /** Prikazuje li se forma za dodavanje. Zatvorena je da spisak bude prvo što se vidi. */
  protected readonly addOpen = signal(false);

  protected readonly filteredExercises = computed(() => {
    const term = this.filter().trim().toLocaleLowerCase('sr');
    const exercises = this.exerciseService.exercises();

    if (term.length === 0) {
      return exercises;
    }

    return exercises.filter((exercise) => exercise.name.toLocaleLowerCase('sr').includes(term));
  });

  protected readonly customCount = computed(
    () => this.exerciseService.exercises().filter((exercise) => exercise.isCustom).length,
  );

  protected readonly totalCount = computed(() => this.exerciseService.exercises().length);

  // --- korak opterećenja -----------------------------------------------------

  protected readonly weightStepOptions = [0.5, 1, 1.25, 2, 2.5, 5, 10];

  protected readonly savingStepId = signal<string | null>(null);
  protected readonly weightStepError = signal<string | null>(null);
  protected readonly weightStepSaved = signal<string | null>(null);

  // --- sopstvene vežbe -------------------------------------------------------

  protected readonly savingExercise = signal(false);
  protected readonly exerciseError = signal<string | null>(null);
  protected readonly exerciseSaved = signal<string | null>(null);

  protected readonly typeOptions = [
    { value: 'Compound', label: 'Složena' },
    { value: 'Isolation', label: 'Izolaciona' },
  ];

  protected readonly equipmentOptions = ['Barbell', 'Dumbbell', 'Machine', 'Cable', 'Bodyweight'];

  protected readonly exerciseForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    type: ['Compound', [Validators.required]],
    equipment: ['Barbell', [Validators.required]],
    primaryMuscle: ['', [Validators.required]],
  });

  private readonly secondarySelection = signal<Set<string>>(new Set());

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      muscles: this.exerciseService.muscleGroups(),
      exercises: this.exerciseService.load(),
    }).subscribe({
      next: ({ muscles }) => {
        this.muscleGroups.set(muscles);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam vežbe. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected back(): void {
    void this.router.navigateByUrl('/profile');
  }

  protected setFilter(term: string): void {
    this.filter.set(term);
  }

  protected toggleAdd(): void {
    this.addOpen.update((open) => !open);
  }

  /**
   * Ponuđeni koraci za vežbu. Ako korisnik ima korak koji nije na listi (postavljen
   * direktno preko API-ja), dodaje se da select ne bi tiho pao na prvu opciju.
   */
  protected stepOptionsFor(exercise: ExerciseDto): number[] {
    if (this.weightStepOptions.includes(exercise.weightStepKg)) {
      return this.weightStepOptions;
    }

    return [...this.weightStepOptions, exercise.weightStepKg].sort((a, b) => a - b);
  }

  protected setWeightStep(exercise: ExerciseDto, select: HTMLSelectElement): void {
    const parsed = Number(select.value);
    if (Number.isNaN(parsed) || parsed === exercise.weightStepKg) {
      return;
    }

    this.saveWeightStep(exercise, parsed, select);
  }

  protected resetWeightStep(exercise: ExerciseDto): void {
    this.saveWeightStep(exercise, null);
  }

  private saveWeightStep(
    exercise: ExerciseDto,
    weightStepKg: number | null,
    select?: HTMLSelectElement,
  ): void {
    if (this.savingStepId()) {
      return;
    }

    this.savingStepId.set(exercise.id);
    this.weightStepError.set(null);
    this.weightStepSaved.set(null);

    this.exerciseService.updateWeightStep(exercise.id, weightStepKg).subscribe({
      next: (updated) => {
        this.savingStepId.set(null);
        // Server može da vrati zaokruženu vrednost; select mora da prikaže nju.
        if (select) {
          select.value = String(updated.weightStepKg);
        }
        this.weightStepSaved.set(`Korak za "${updated.name}" je sada ${updated.weightStepKg} kg.`);
        setTimeout(() => this.weightStepSaved.set(null), 3200);
      },
      error: (err: unknown) => {
        this.savingStepId.set(null);
        // Angular ne prepisuje [selected] kada se model nije promenio, pa se
        // odbijena izmena mora ručno vratiti da select ne laže o stanju servera.
        if (select) {
          select.value = String(exercise.weightStepKg);
        }
        this.weightStepError.set(
          extractErrorMessage(err, 'Korak opterećenja nije sačuvan. Pokušaj ponovo.'),
        );
      },
    });
  }

  protected isSecondary(muscle: string): boolean {
    return this.secondarySelection().has(muscle);
  }

  protected toggleSecondary(muscle: string): void {
    this.secondarySelection.update((selection) => {
      const next = new Set(selection);
      if (next.has(muscle)) {
        next.delete(muscle);
      } else {
        next.add(muscle);
      }
      return next;
    });
  }

  protected saveExercise(): void {
    if (this.savingExercise()) {
      return;
    }

    if (this.exerciseForm.invalid) {
      this.exerciseForm.markAllAsTouched();
      return;
    }

    const raw = this.exerciseForm.getRawValue();
    const secondary = [...this.secondarySelection()].filter(
      (muscle) => muscle !== raw.primaryMuscle,
    );
    const request: CreateExerciseRequest = {
      name: raw.name.trim(),
      type: raw.type,
      equipment: raw.equipment,
      muscles: [
        { muscleGroup: raw.primaryMuscle, contribution: 1.0 },
        ...secondary.map((muscle) => ({ muscleGroup: muscle, contribution: 0.5 })),
      ],
    };

    this.savingExercise.set(true);
    this.exerciseError.set(null);
    this.exerciseSaved.set(null);

    this.exerciseService.createCustom(request).subscribe({
      next: (exercise) => {
        this.savingExercise.set(false);
        this.exerciseSaved.set(`Vežba "${exercise.name}" je dodata u katalog.`);
        this.exerciseForm.reset({
          name: '',
          type: 'Compound',
          equipment: 'Barbell',
          primaryMuscle: '',
        });
        this.secondarySelection.set(new Set());
        // Nova vežba je na dnu spiska; forma se zatvara da se spisak vidi.
        this.addOpen.set(false);
        setTimeout(() => this.exerciseSaved.set(null), 4000);
      },
      error: (err: unknown) => {
        this.savingExercise.set(false);
        this.exerciseError.set(
          extractErrorMessage(err, 'Vežba nije sačuvana. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }
}
