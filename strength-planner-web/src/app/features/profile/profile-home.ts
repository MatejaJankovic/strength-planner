import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { forkJoin } from 'rxjs';
import { extractErrorMessage } from '../../core/api/http-error';
import { ExerciseService } from '../../core/api/exercise.service';
import { AuthService } from '../../core/auth/auth.service';
import { ExperienceLevel, PASSWORD_MIN_LENGTH, UpdateProfileDto } from '../../core/models/auth.models';
import { CreateExerciseRequest, ExerciseDto } from '../../core/models/training.models';
import { Loading } from '../../shared/components/loading/loading';

@Component({
  selector: 'app-profile-home',
  imports: [ReactiveFormsModule, MatIconModule, Loading],
  templateUrl: './profile-home.html',
  styleUrl: './profile-home.scss',
})
export class ProfileHome {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly exerciseService = inject(ExerciseService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly user = this.auth.currentUser;

  // --- profil ----------------------------------------------------------------

  protected readonly savingProfile = signal(false);
  protected readonly profileError = signal<string | null>(null);
  protected readonly profileSaved = signal(false);

  protected readonly experienceOptions = [
    { value: ExperienceLevel.Beginner, label: 'Početnik' },
    { value: ExperienceLevel.Intermediate, label: 'Srednji nivo' },
    { value: ExperienceLevel.Advanced, label: 'Napredni' },
  ];

  protected readonly dayOptions = [2, 3, 4, 5, 6];

  protected readonly profileForm = this.fb.nonNullable.group({
    sex: [''],
    age: ['', [Validators.required, Validators.min(14), Validators.max(90)]],
    bodyweightKg: ['', [Validators.required, Validators.min(30), Validators.max(300)]],
    experienceLevel: ['', [Validators.required]],
    trainingDaysPerWeek: ['3', [Validators.required]],
  });

  // --- custom vežbe ------------------------------------------------------------

  // --- promena lozinke ---
  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;
  protected readonly savingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly passwordSaved = signal(false);

  protected readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)]],
  });

  protected readonly muscleGroups = signal<string[]>([]);
  protected readonly savingExercise = signal(false);
  protected readonly exerciseError = signal<string | null>(null);
  protected readonly exerciseSaved = signal<string | null>(null);

  protected readonly customExercises = computed(() =>
    this.exerciseService.exercises().filter((exercise) => exercise.isCustom),
  );

  // --- korak opterećenja -------------------------------------------------------

  protected readonly weightStepOptions = [0.5, 1, 1.25, 2, 2.5, 5, 10];

  protected readonly savingStepId = signal<string | null>(null);
  protected readonly weightStepError = signal<string | null>(null);
  protected readonly weightStepSaved = signal<string | null>(null);
  protected readonly exerciseFilter = signal('');

  protected readonly filteredExercises = computed(() => {
    const term = this.exerciseFilter().trim().toLocaleLowerCase('sr');
    const exercises = this.exerciseService.exercises();

    if (term.length === 0) {
      return exercises;
    }

    return exercises.filter((exercise) => exercise.name.toLocaleLowerCase('sr').includes(term));
  });

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
      me: this.auth.loadMe(),
      muscles: this.exerciseService.muscleGroups(),
      exercises: this.exerciseService.load(),
    }).subscribe({
      next: ({ me, muscles }) => {
        this.muscleGroups.set(muscles);
        this.profileForm.patchValue({
          sex: me.sex ?? '',
          age: me.age != null ? String(me.age) : '',
          bodyweightKg: me.bodyweightKg != null ? String(me.bodyweightKg) : '',
          experienceLevel: this.experienceLevelToValue(me.experienceLevel),
          trainingDaysPerWeek:
            me.trainingDaysPerWeek != null ? String(me.trainingDaysPerWeek) : '3',
        });
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam profil. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected changePassword(): void {
    if (this.savingPassword()) {
      return;
    }

    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.savingPassword.set(true);
    this.passwordError.set(null);
    this.passwordSaved.set(false);

    this.auth.changePassword(this.passwordForm.getRawValue()).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.passwordSaved.set(true);
        // Lozinka ne sme da ostane u formi posle uspešne izmene.
        this.passwordForm.reset();
        setTimeout(() => this.passwordSaved.set(false), 3200);
      },
      error: (err: unknown) => {
        this.savingPassword.set(false);
        this.passwordError.set(
          extractErrorMessage(err, 'Lozinka nije promenjena. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }

  protected saveProfile(): void {
    if (this.savingProfile()) {
      return;
    }

    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const raw = this.profileForm.getRawValue();
    const dto: UpdateProfileDto = {
      sex: raw.sex ? raw.sex : null,
      age: Number(raw.age),
      bodyweightKg: Number(raw.bodyweightKg),
      experienceLevel: Number(raw.experienceLevel) as ExperienceLevel,
      trainingDaysPerWeek: Number(raw.trainingDaysPerWeek),
    };

    this.savingProfile.set(true);
    this.profileError.set(null);
    this.profileSaved.set(false);

    this.auth.updateProfile(dto).subscribe({
      next: () => {
        this.savingProfile.set(false);
        this.profileSaved.set(true);
        setTimeout(() => this.profileSaved.set(false), 3200);
      },
      error: (err: unknown) => {
        this.savingProfile.set(false);
        this.profileError.set(
          extractErrorMessage(err, 'Izmena profila nije sačuvana. Pokušaj ponovo.'),
        );
      },
    });
  }

  protected setExerciseFilter(term: string): void {
    this.exerciseFilter.set(term);
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

  protected logout(): void {
    this.auth.logout();
  }

  private experienceLevelToValue(level?: string | number | null): string {
    // Backend serijalizuje enum kao broj; string imena su fallback.
    switch (level) {
      case ExperienceLevel.Beginner:
      case 'Beginner':
        return String(ExperienceLevel.Beginner);
      case ExperienceLevel.Intermediate:
      case 'Intermediate':
        return String(ExperienceLevel.Intermediate);
      case ExperienceLevel.Advanced:
      case 'Advanced':
        return String(ExperienceLevel.Advanced);
      default:
        return '';
    }
  }
}
