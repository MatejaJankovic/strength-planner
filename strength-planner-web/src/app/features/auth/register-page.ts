import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { AuthService } from '../../core/auth/auth.service';
import { ExperienceLevel, RegisterDto } from '../../core/models/auth.models';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, MatIconModule],
  templateUrl: './register-page.html',
  styleUrl: './register-page.scss',
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly experienceOptions = [
    { value: ExperienceLevel.Beginner, label: 'Početnik' },
    { value: ExperienceLevel.Intermediate, label: 'Srednji nivo' },
    { value: ExperienceLevel.Advanced, label: 'Napredni' },
  ];

  protected readonly dayOptions = [2, 3, 4, 5, 6];

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    sex: [''],
    age: ['', [Validators.required, Validators.min(14), Validators.max(90)]],
    bodyweightKg: ['', [Validators.required, Validators.min(30), Validators.max(300)]],
    experienceLevel: ['', [Validators.required]],
    trainingDaysPerWeek: ['3', [Validators.required]],
  });

  protected togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const dto: RegisterDto = {
      email: raw.email.trim(),
      password: raw.password,
      sex: raw.sex ? raw.sex : null,
      age: Number(raw.age),
      bodyweightKg: Number(raw.bodyweightKg),
      experienceLevel: Number(raw.experienceLevel) as ExperienceLevel,
      trainingDaysPerWeek: Number(raw.trainingDaysPerWeek),
    };

    this.submitting.set(true);
    this.error.set(null);

    this.auth.register(dto).subscribe({
      next: () => {
        void this.router.navigateByUrl('/onboarding');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(
          extractErrorMessage(err, 'Registracija nije uspela. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }
}
