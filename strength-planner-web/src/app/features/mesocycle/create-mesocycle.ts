import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { MesocycleService } from '../../core/api/mesocycle.service';
import {
  Goal,
  GenerateMesocycleRequest,
  PeriodizationModel,
  WorkoutTemplateDto,
} from '../../core/models/training.models';
import { Loading } from '../../shared/components/loading/loading';

interface GoalOption {
  value: Goal;
  label: string;
  detail: string;
}

interface ModelOption {
  value: PeriodizationModel;
  label: string;
  /** Već u ispravnom obliku množine: 4 nedelje, 6 nedelja. */
  weeks: string;
  detail: string;
}

@Component({
  selector: 'app-create-mesocycle',
  imports: [ReactiveFormsModule, MatIconModule, Loading],
  templateUrl: './create-mesocycle.html',
  styleUrl: './create-mesocycle.scss',
})
export class CreateMesocycle {
  private readonly fb = inject(FormBuilder);
  private readonly mesocycleService = inject(MesocycleService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);

  protected readonly templates = signal<WorkoutTemplateDto[]>([]);
  protected readonly selectedKey = signal<string | null>(null);
  protected readonly goal = signal<Goal>(Goal.Hypertrophy);

  protected readonly goalOptions: GoalOption[] = [
    {
      value: Goal.Strength,
      label: 'Snaga',
      detail: 'Teže serije u opsegu 3–6 ponavljanja, sa 2 ponavljanja u rezervi (RIR).',
    },
    {
      value: Goal.Hypertrophy,
      label: 'Hipertrofija',
      detail: 'Više ponavljanja, 8–12 po seriji, sa 1 ponavljanjem u rezervi (RIR).',
    },
  ];

  protected readonly model = signal<PeriodizationModel>(PeriodizationModel.Flat);

  protected readonly modelOptions: ModelOption[] = [
    {
      value: PeriodizationModel.Flat,
      label: 'Ravan',
      weeks: '4 nedelje',
      detail:
        'Isti propis svake nedelje; napredak nosi dupla progresija - prvo ponavljanja, pa opterećenje.',
    },
    {
      value: PeriodizationModel.Linear,
      label: 'Linearan',
      weeks: '6 nedelja',
      detail:
        'Počinje volumenom (više ponavljanja, lakše serije), završava intenzitetom (manje ponavljanja, bliže otkazu).',
    },
    {
      value: PeriodizationModel.Inverse,
      label: 'Obrnut',
      weeks: '6 nedelja',
      detail:
        'Teško dok si svež, volumen pred kraj - obrnut redosled u odnosu na linearan.',
    },
  ];

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    startDate: [todayIso(), [Validators.required]],
  });

  protected readonly selectedGoalDetail = computed(
    () => this.goalOptions.find((option) => option.value === this.goal())?.detail ?? '',
  );

  protected readonly selectedModel = computed(
    () => this.modelOptions.find((option) => option.value === this.model()) ?? this.modelOptions[0],
  );

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.mesocycleService.templates().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.loading.set(false);

        // Prvi šablon je izabran unapred samo da dugme "Napravi plan" ne bi bilo
        // zaključano na otvaranju ekrana; korisnik bira šta hoće.
        const first = templates[0];
        if (first && !this.selectedKey()) {
          this.selectTemplate(first);
        }
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam šablone. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected selectTemplate(template: WorkoutTemplateDto): void {
    this.selectedKey.set(template.key);
    // Suggest a name from the template until the athlete types their own.
    const nameControl = this.form.controls.name;
    if (!nameControl.dirty || !nameControl.value.trim()) {
      nameControl.setValue(template.name);
    }
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    const key = this.selectedKey();
    if (!key) {
      this.submitError.set('Izaberi šablon treninga.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: GenerateMesocycleRequest = {
      templateKey: key,
      goal: this.goal(),
      periodizationModel: this.model(),
      name: raw.name.trim(),
      startDate: raw.startDate,
    };

    this.submitting.set(true);
    this.submitError.set(null);

    this.mesocycleService.create(request).subscribe({
      next: () => {
        void this.router.navigateByUrl('/workout');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.submitError.set(
          extractErrorMessage(err, 'Plan nije napravljen. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
