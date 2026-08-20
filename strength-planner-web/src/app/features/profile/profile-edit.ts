import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { extractErrorMessage } from '../../core/api/http-error';
import { AuthService } from '../../core/auth/auth.service';
import {
  AVATAR_ACCEPTED_TYPES,
  AVATAR_MAX_BYTES,
  DISPLAY_NAME_MAX_LENGTH,
  ExperienceLevel,
  HEIGHT_MAX_CM,
  HEIGHT_MIN_CM,
  profileInitial,
  Sex,
  SEX_OPTIONS,
  UpdateProfileDto,
} from '../../core/models/auth.models';
import { Loading } from '../../shared/components/loading/loading';
import { SubscreenHeader } from '../../shared/components/subscreen-header/subscreen-header';

/**
 * Izmena profila — svi podaci o korisniku na jednom mestu.
 *
 * Do ove runde su isti podaci stajali kao kartica „Osnovni podaci" na ekranu profila.
 * Profil je sada pregled sa dugmetom olovke koje vodi ovamo, pa se podaci menjaju na
 * ekranu koji ne služi ničemu drugom.
 *
 * Slika se čuva odmah po izboru, odvojenim zahtevom, dok ostala polja čekaju „Sačuvaj".
 * Ne zato što je lakše: slika ide kao multipart a ostalo kao JSON, i vezati ih u jedan
 * zahtev značilo bi da neuspeh na jednom polju vrati i sliku.
 */
@Component({
  selector: 'app-profile-edit',
  imports: [ReactiveFormsModule, MatIconModule, Loading, SubscreenHeader],
  templateUrl: './profile-edit.html',
  styleUrl: './profile-edit.scss',
})
export class ProfileEdit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  protected readonly avatarBusy = signal(false);
  protected readonly avatarError = signal<string | null>(null);

  protected readonly user = this.auth.currentUser;
  protected readonly avatarUrl = this.auth.avatarUrl;

  protected readonly acceptedTypes = AVATAR_ACCEPTED_TYPES;
  protected readonly displayNameMaxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly heightMin = HEIGHT_MIN_CM;
  protected readonly heightMax = HEIGHT_MAX_CM;

  protected readonly sexOptions = SEX_OPTIONS;

  protected readonly experienceOptions = [
    { value: ExperienceLevel.Beginner, label: 'Početnik' },
    { value: ExperienceLevel.Intermediate, label: 'Srednji nivo' },
    { value: ExperienceLevel.Advanced, label: 'Napredni' },
  ];

  /** Slovo u krugu kada slike nema. Isti izvor kao naslov na ekranu profila. */
  protected readonly initial = computed(() => profileInitial(this.user()));

  protected readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.maxLength(DISPLAY_NAME_MAX_LENGTH)]],
    sex: [''],
    age: ['', [Validators.required, Validators.min(14), Validators.max(90)]],
    bodyweightKg: ['', [Validators.required, Validators.min(30), Validators.max(300)]],
    heightCm: ['', [Validators.min(HEIGHT_MIN_CM), Validators.max(HEIGHT_MAX_CM)]],
    experienceLevel: ['', [Validators.required]],
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      me: this.auth.loadMe(),
      // Nalog bez slike vraća 404; to je odgovor, ne greška, pa ne sme da obori ekran.
      avatar: this.auth.loadAvatar().pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ me }) => {
        this.form.patchValue({
          displayName: me.displayName ?? '',
          sex: sexToValue(me.sex),
          age: me.age != null ? String(me.age) : '',
          bodyweightKg: me.bodyweightKg != null ? String(me.bodyweightKg) : '',
          heightCm: me.heightCm != null ? String(me.heightCm) : '',
          experienceLevel: experienceLevelToValue(me.experienceLevel),
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

  protected pickAvatar(input: HTMLInputElement): void {
    const file = input.files?.[0];

    // Birač fajla se prazni odmah: bez toga izbor iste datoteke drugi put ne pokreće
    // `change`, pa posle neuspelog otpremanja ponovni izbor ne bi radio ništa.
    input.value = '';

    if (!file || this.avatarBusy()) {
      return;
    }

    // Granica se proverava i ovde, da se dva megabajta ne pošalju uzalud. Pravu odluku
    // ipak donosi server - ovo je udobnost, ne zaštita.
    if (file.size > AVATAR_MAX_BYTES) {
      this.avatarError.set(
        `Slika je veća od ${AVATAR_MAX_BYTES / (1024 * 1024)} MB. Izaberi manju.`,
      );
      return;
    }

    this.avatarBusy.set(true);
    this.avatarError.set(null);

    this.auth.uploadAvatar(file).subscribe({
      next: () => {
        // Prikaz se osvežava sa servera, ne iz izabranog fajla: server je taj koji je
        // odlučio da sadržaj jeste slika i pod kojim tipom je vraća.
        this.auth.loadAvatar().subscribe({
          next: () => this.avatarBusy.set(false),
          error: () => this.avatarBusy.set(false),
        });
      },
      error: (err: unknown) => {
        this.avatarBusy.set(false);
        this.avatarError.set(
          extractErrorMessage(err, 'Slika nije sačuvana. Izaberi JPEG, PNG ili WebP.'),
        );
      },
    });
  }

  protected removeAvatar(): void {
    if (this.avatarBusy()) {
      return;
    }

    this.avatarBusy.set(true);
    this.avatarError.set(null);

    this.auth.removeAvatar().subscribe({
      next: () => this.avatarBusy.set(false),
      error: (err: unknown) => {
        this.avatarBusy.set(false);
        this.avatarError.set(extractErrorMessage(err, 'Slika nije uklonjena. Pokušaj ponovo.'));
      },
    });
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const dto: UpdateProfileDto = {
      // Prazno polje je "nemam", pa ide kao null - isto kao pol.
      displayName: raw.displayName.trim() === '' ? null : raw.displayName.trim(),
      sex: raw.sex === '' ? null : (Number(raw.sex) as Sex),
      age: Number(raw.age),
      bodyweightKg: Number(raw.bodyweightKg),
      heightCm: raw.heightCm === '' ? null : Number(raw.heightCm),
      experienceLevel: Number(raw.experienceLevel) as ExperienceLevel,
    };

    this.saving.set(true);
    this.saveError.set(null);

    this.auth.updateProfile(dto).subscribe({
      next: () => {
        this.saving.set(false);
        void this.router.navigateByUrl('/profile');
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(extractErrorMessage(err, 'Izmena nije sačuvana. Pokušaj ponovo.'));
      },
    });
  }

  protected cancel(): void {
    void this.router.navigateByUrl('/profile');
  }
}

/**
 * Vrednost za `<option>` iz onoga što server pošalje.
 *
 * Prazan string je "ne želim da navedem" i jedina je vrednost koja sme da ostavi meni
 * neoznačen. Backend serijalizuje enum kao broj; string imena su fallback.
 */
function sexToValue(sex?: Sex | string | null): string {
  switch (sex) {
    case Sex.Male:
    case 'Male':
      return String(Sex.Male);
    case Sex.Female:
    case 'Female':
      return String(Sex.Female);
    default:
      return '';
  }
}

function experienceLevelToValue(level?: string | number | null): string {
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
