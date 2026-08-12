import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { Goal, GenerateMesocycleRequest, WorkoutTemplateDto } from '../../core/models/training.models';
import { Loading } from '../../shared/components/loading/loading';

interface GoalOption {
  value: Goal;
  label: string;
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

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(128)]],
    startDate: [todayIso(), [Validators.required]],
  });

  protected readonly selectedGoalDetail = computed(
    () => this.goalOptions.find((option) => option.value === this.goal())?.detail ?? '',
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

        // Server označava šablon koji odgovara broju trenažnih dana iz profila.
        // Izabran je unapred, ali korisnik i dalje bira šta hoće.
        const suggested = templates.find((template) => template.isSuggested);
        if (suggested && !this.selectedKey()) {
          this.selectTemplate(suggested);
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
