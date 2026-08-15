import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { forkJoin } from 'rxjs';
import { extractErrorMessage } from '../../core/api/http-error';
import { CustomTemplateService } from '../../core/api/custom-template.service';
import { ExerciseService } from '../../core/api/exercise.service';
import {
  CustomTemplateDto,
  ExerciseDto,
  SaveCustomTemplateRequest,
  TEMPLATE_LIMITS,
} from '../../core/models/training.models';
import { Loading } from '../../shared/components/loading/loading';

/** Vežba u danu koji se sastavlja. Drži se identifikator, naziv se čita iz kataloga. */
interface DraftExercise {
  exerciseId: string;
  sets: number;
  repRangeMin: number;
  repRangeMax: number;
}

interface DraftDay {
  name: string;
  exercises: DraftExercise[];
}

@Component({
  selector: 'app-custom-templates',
  imports: [MatIconModule, Loading],
  templateUrl: './custom-templates.html',
  styleUrl: './custom-templates.scss',
})
export class CustomTemplates {
  private readonly customTemplateService = inject(CustomTemplateService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly router = inject(Router);

  protected readonly limits = TEMPLATE_LIMITS;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly templates = signal<CustomTemplateDto[]>([]);
  protected readonly exercises = signal<ExerciseDto[]>([]);

  // --- editor -----------------------------------------------------------------

  /** Otvoren editor: null je zatvoren, prazan string nov šablon, id izmena postojećeg. */
  protected readonly editingId = signal<string | null | undefined>(undefined);
  protected readonly name = signal('');
  protected readonly days = signal<DraftDay[]>([]);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly confirmingDeleteId = signal<string | null>(null);

  protected readonly isEditorOpen = computed(() => this.editingId() !== undefined);

  protected readonly canAddDay = computed(() => this.days().length < this.limits.maxDays);

  protected readonly exerciseCount = computed(() =>
    this.days().reduce((total, day) => total + day.exercises.length, 0),
  );

  /**
   * Šablon bez ijedne vežbe ne bi napravio nijedan trening, a server bi ga odbio tek posle
   * poziva. Dugme je zato zaključano dok se to ne ispuni.
   */
  protected readonly canSave = computed(() => {
    const days = this.days();
    const names = days.map((day) => day.name.trim().toLocaleLowerCase('sr'));

    return (
      this.name().trim().length > 0 &&
      days.length > 0 &&
      days.every((day) => day.name.trim().length > 0 && day.exercises.length > 0) &&
      new Set(names).size === names.length
    );
  });

  /** Poruka uz zaključano dugme, da se ne pogađa šta nedostaje. */
  protected readonly saveBlockedReason = computed(() => {
    const days = this.days();

    if (this.name().trim().length === 0) {
      return 'Unesi naziv šablona.';
    }
    if (days.length === 0) {
      return 'Dodaj bar jedan dan.';
    }
    if (days.some((day) => day.name.trim().length === 0)) {
      return 'Svaki dan mora da ima naziv.';
    }
    if (days.some((day) => day.exercises.length === 0)) {
      return 'Svaki dan mora da ima bar jednu vežbu.';
    }

    const names = days.map((day) => day.name.trim().toLocaleLowerCase('sr'));
    if (new Set(names).size !== names.length) {
      return 'Nazivi dana moraju da se razlikuju.';
    }

    return null;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      templates: this.customTemplateService.list(),
      exercises: this.exerciseService.load(),
    }).subscribe({
      next: ({ templates, exercises }) => {
        this.templates.set(templates);
        this.exercises.set(exercises);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam šablone. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected exerciseName(exerciseId: string): string {
    return this.exercises().find((exercise) => exercise.id === exerciseId)?.name ?? '';
  }

  // --- otvaranje i zatvaranje editora -------------------------------------------

  protected startNew(): void {
    this.editingId.set(null);
    this.name.set('');
    this.days.set([this.newDay(1)]);
    this.saveError.set(null);
  }

  protected startEdit(template: CustomTemplateDto): void {
    this.editingId.set(template.id);
    this.name.set(template.name);
    this.days.set(
      template.days.map((day) => ({
        name: day.name,
        exercises: day.exercises.map((exercise) => ({
          exerciseId: exercise.exerciseId,
          sets: exercise.sets,
          repRangeMin: exercise.repRangeMin,
          repRangeMax: exercise.repRangeMax,
        })),
      })),
    );
    this.saveError.set(null);
  }

  protected closeEditor(): void {
    this.editingId.set(undefined);
    this.saveError.set(null);
  }

  // --- dani --------------------------------------------------------------------

  private newDay(index: number): DraftDay {
    return { name: `Dan ${index}`, exercises: [] };
  }

  /**
   * Naziv novog dana mora da bude slobodan: server odbija dva dana istog naziva, jer naziv
   * postaje oznaka treninga po kojoj deload prepoznaje dan.
   */
  protected addDay(): void {
    if (!this.canAddDay()) {
      return;
    }

    this.days.update((days) => {
      const taken = new Set(days.map((day) => day.name.trim().toLocaleLowerCase('sr')));
      let candidate = days.length + 1;

      while (taken.has(`dan ${candidate}`)) {
        candidate += 1;
      }

      return [...days, this.newDay(candidate)];
    });
  }

  protected removeDay(dayIndex: number): void {
    this.days.update((days) => days.filter((_, index) => index !== dayIndex));
  }

  protected setDayName(dayIndex: number, value: string): void {
    this.days.update((days) =>
      days.map((day, index) => (index === dayIndex ? { ...day, name: value } : day)),
    );
  }

  // --- vežbe u danu -------------------------------------------------------------

  protected canAddExercise(dayIndex: number): boolean {
    return (this.days()[dayIndex]?.exercises.length ?? 0) < this.limits.maxExercisesPerDay;
  }

  /**
   * Vežbe koje dan još nema. Server odbija istu vežbu dvaput u danu - automatski deload
   * izvodi polazni broj serija po paru (dan, vežba) - pa se ovde i ne nudi.
   */
  protected availableFor(dayIndex: number): ExerciseDto[] {
    const taken = new Set(this.days()[dayIndex]?.exercises.map((item) => item.exerciseId) ?? []);

    return this.exercises().filter((exercise) => !taken.has(exercise.id));
  }

  /**
   * Nova vežba kreće od vrednosti koje odgovaraju hipertrofiji (3 serije, 8-12), jer je to
   * najčešći izbor; svaka je odmah izmenjiva.
   */
  protected addExercise(dayIndex: number, exerciseId: string): void {
    if (!exerciseId || !this.canAddExercise(dayIndex)) {
      return;
    }

    this.days.update((days) =>
      days.map((day, index) =>
        index === dayIndex
          ? {
              ...day,
              exercises: [
                ...day.exercises,
                { exerciseId, sets: 3, repRangeMin: 8, repRangeMax: 12 },
              ],
            }
          : day,
      ),
    );
  }

  protected removeExercise(dayIndex: number, exerciseIndex: number): void {
    this.days.update((days) =>
      days.map((day, index) =>
        index === dayIndex
          ? { ...day, exercises: day.exercises.filter((_, i) => i !== exerciseIndex) }
          : day,
      ),
    );
  }

  protected setExerciseNumber(
    dayIndex: number,
    exerciseIndex: number,
    field: 'sets' | 'repRangeMin' | 'repRangeMax',
    value: string,
  ): void {
    const parsed = Number(value);
    if (Number.isNaN(parsed)) {
      return;
    }

    const bounds =
      field === 'sets'
        ? { min: this.limits.minSets, max: this.limits.maxSets }
        : { min: this.limits.minReps, max: this.limits.maxReps };
    const clamped = Math.min(Math.max(Math.round(parsed), bounds.min), bounds.max);

    this.days.update((days) =>
      days.map((day, index) => {
        if (index !== dayIndex) {
          return day;
        }

        return {
          ...day,
          exercises: day.exercises.map((exercise, i) => {
            if (i !== exerciseIndex) {
              return exercise;
            }

            const next = { ...exercise, [field]: clamped };

            // Opseg mora da ostane opseg. Podizanje donje granice preko gornje pomera
            // gornju sa njom, umesto da server odbije ceo šablon zbog jednog polja.
            if (field === 'repRangeMin' && next.repRangeMin > next.repRangeMax) {
              next.repRangeMax = next.repRangeMin;
            }
            if (field === 'repRangeMax' && next.repRangeMax < next.repRangeMin) {
              next.repRangeMin = next.repRangeMax;
            }

            return next;
          }),
        };
      }),
    );
  }

  // --- čuvanje i brisanje --------------------------------------------------------

  protected save(): void {
    if (this.saving() || !this.canSave()) {
      return;
    }

    const request: SaveCustomTemplateRequest = {
      name: this.name().trim(),
      days: this.days().map((day) => ({
        name: day.name.trim(),
        exercises: day.exercises.map((exercise) => ({
          exerciseId: exercise.exerciseId,
          sets: exercise.sets,
          repRangeMin: exercise.repRangeMin,
          repRangeMax: exercise.repRangeMax,
        })),
      })),
    };

    this.saving.set(true);
    this.saveError.set(null);

    const editingId = this.editingId();
    const call = editingId
      ? this.customTemplateService.update(editingId, request)
      : this.customTemplateService.create(request);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.closeEditor();
        this.load();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(
          extractErrorMessage(err, 'Šablon nije sačuvan. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }

  protected requestDelete(templateId: string): void {
    this.confirmingDeleteId.set(templateId);
    this.saveError.set(null);
  }

  protected cancelDelete(): void {
    this.confirmingDeleteId.set(null);
  }

  protected confirmDelete(templateId: string): void {
    if (this.deletingId()) {
      return;
    }

    this.deletingId.set(templateId);

    this.customTemplateService.delete(templateId).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.confirmingDeleteId.set(null);
        this.load();
      },
      error: (err: unknown) => {
        this.deletingId.set(null);
        this.confirmingDeleteId.set(null);
        this.saveError.set(extractErrorMessage(err, 'Šablon nije obrisan.'));
      },
    });
  }

  protected goToMesocycle(): void {
    void this.router.navigateByUrl('/mesocycle');
  }
}
