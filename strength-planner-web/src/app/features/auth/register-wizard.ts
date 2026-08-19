import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { AuthService } from '../../core/auth/auth.service';
import {
  DISPLAY_NAME_MAX_LENGTH,
  EMAIL_MAX_LENGTH,
  ExperienceLevel,
  HEIGHT_MAX_CM,
  HEIGHT_MIN_CM,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  RegisterDto,
  Sex,
} from '../../core/models/auth.models';
import { ChoiceList, ChoiceOption } from '../../shared/components/choice-list/choice-list';
import { MeasureInput } from '../../shared/components/measure-input/measure-input';
import { WizardShell } from '../../shared/components/wizard-shell/wizard-shell';
import { OneRepMaxSetup } from '../onboarding/one-rep-max-setup';

/**
 * Koraci, redom. Vrednosti se nigde ne upisuju, pa smeju da se premeštaju — jedino
 * `OneRepMax` mora da ostane poslednji, jer je jedini koji traži postojeći nalog.
 */
enum Step {
  Name,
  Credentials,
  Sex,
  Bodyweight,
  Height,
  Age,
  Experience,
  OneRepMax,
}

const STEP_COUNT = Step.OneRepMax + 1;

/** Polazne vrednosti mera. Klizač mora od nečega da krene, a prazan nema gde da stoji. */
const DEFAULT_BODYWEIGHT_KG = 75;
const DEFAULT_HEIGHT_CM = 175;
const DEFAULT_AGE = 25;

/**
 * Registracija kao niz ekrana, po jedno pitanje na svakom.
 *
 * Nalog nastaje tek posle poslednjeg pitanja, jednim zahtevom: dok se odgovori drže u
 * pregledaču, odustajanje na petom ekranu ne ostavlja nalog sa pola profila koji korisnik
 * ne može ni da dovrši ni da obriše. Cena je što se zauzet email vidi tek na kraju — zato
 * je taj korak drugi po redu, da se do njega dođe u dva dodira.
 *
 * Poslednji korak je unos maksimuma (1RM) i on je posle registracije: traži katalog vežbi
 * sa servera, a njemu se pristupa tek sa tokenom.
 */
@Component({
  selector: 'app-register-wizard',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatIconModule,
    WizardShell,
    ChoiceList,
    MeasureInput,
    OneRepMaxSetup,
  ],
  templateUrl: './register-wizard.html',
  styleUrl: './register-wizard.scss',
})
export class RegisterWizard {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly Step = Step;
  protected readonly stepCount = STEP_COUNT;

  protected readonly step = signal(Step.Name);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;
  protected readonly displayNameMaxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly heightMin = HEIGHT_MIN_CM;
  protected readonly heightMax = HEIGHT_MAX_CM;

  protected readonly sexOptions: ReadonlyArray<ChoiceOption> = [
    { value: String(Sex.Male), label: 'Muški', icon: 'male' },
    { value: String(Sex.Female), label: 'Ženski', icon: 'female' },
  ];

  protected readonly experienceOptions: ReadonlyArray<ChoiceOption> = [
    { value: String(ExperienceLevel.Beginner), label: 'Početnik', hint: '0-1 godina' },
    { value: String(ExperienceLevel.Intermediate), label: 'Srednji nivo', hint: '1-3 godine' },
    { value: String(ExperienceLevel.Advanced), label: 'Napredni', hint: '3+ godine' },
  ];

  /**
   * Sva pitanja u jednoj formi, iako se prikazuju jedno po jedno.
   *
   * Odvojene forme po koraku bi značile da povratak na raniji korak gubi odgovor, a
   * povratak je na ovom toku očekivan koliko i napredovanje.
   */
  protected readonly form = this.fb.nonNullable.group({
    displayName: [
      '',
      [Validators.required, Validators.maxLength(DISPLAY_NAME_MAX_LENGTH)],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(EMAIL_MAX_LENGTH)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(PASSWORD_MIN_LENGTH),
        Validators.maxLength(PASSWORD_MAX_LENGTH),
      ],
    ],
    sex: [''],
    bodyweightKg: [DEFAULT_BODYWEIGHT_KG],
    heightCm: [DEFAULT_HEIGHT_CM],
    age: [DEFAULT_AGE],
    experienceLevel: ['', [Validators.required]],
    // Zamka za automate: sakrivena je u prikazu, pa je kod čoveka uvek prazna.
    website: [''],
  });

  /**
   * Vrednost forme kao signal.
   *
   * `computed` prati samo signale, a `AbstractControl.valid` nije signal — bez ovoga se
   * `canContinue` izračuna jednom, na praznoj formi, i ostane `false` zauvek. Ovo se ne
   * vidi u testu forme nego tek na ekranu: dugme „Nastavi" nikada ne oživi, ma šta se
   * ukucalo. Merljivo je i bilo je izmereno — prvi prolaz kroz čarobnjaka je stao na
   * prvom pitanju.
   *
   * Vrednost se ne čita; služi samo da uspostavi zavisnost. Svi validatori su sinhroni i
   * izvedeni iz vrednosti, pa je `valueChanges` dovoljan okidač i za promenu validnosti.
   */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly title = computed(() => {
    switch (this.step()) {
      case Step.Name:
        return 'Kako da te zovemo?';
      case Step.Credentials:
        return 'Napravi nalog';
      case Step.Sex:
        return 'Koji je tvoj pol?';
      case Step.Bodyweight:
        return 'Kolika ti je telesna masa?';
      case Step.Height:
        return 'Koliko si visok?';
      case Step.Age:
        return 'Koliko imaš godina?';
      case Step.Experience:
        return 'Koliko dugo treniraš?';
      case Step.OneRepMax:
        return 'Poznati maksimumi';
    }
  });

  protected readonly subtitle = computed(() => {
    switch (this.step()) {
      case Step.Name:
        return 'Ime stoji na tvom profilu. Niko drugi ga ne vidi — aplikacija nema deljenje.';
      case Step.Credentials:
        return 'Email i lozinka su jedini način da se vratiš na svoje podatke.';
      case Step.Sex:
        return 'Ne ulazi u računicu plana. Stoji uz ostale podatke o tebi.';
      case Step.Bodyweight:
        return 'Koristi se u analitici. Kasnije je menjaš na profilu.';
      case Step.Height:
        return 'Ne ulazi u računicu plana, kao ni pol.';
      case Step.Age:
        return null;
      case Step.Experience:
        return 'Ovo bira koliko serija i koliko težak plan dobijaš na početku.';
      case Step.OneRepMax:
        return 'Procena maksimuma za jedno ponavljanje daje početna opterećenja u prvom bloku.';
    }
  });

  /**
   * Da li tekući korak sme da propusti dalje.
   *
   * Pol i visina nemaju uslov: opcioni su na serveru, pa bi uslov ovde značio da ekran
   * traži više nego što je potrebno.
   */
  protected readonly canContinue = computed(() => {
    // Čitanje uspostavlja zavisnost od forme; vrednost sama po sebi ovde ne treba.
    this.formValue();
    const controls = this.form.controls;

    switch (this.step()) {
      case Step.Name:
        return controls.displayName.valid;
      case Step.Credentials:
        return controls.email.valid && controls.password.valid;
      case Step.Experience:
        return controls.experienceLevel.valid;
      case Step.Sex:
      case Step.Bodyweight:
      case Step.Height:
      case Step.Age:
      case Step.OneRepMax:
        return true;
    }
  });

  protected readonly continueLabel = computed(() => {
    switch (this.step()) {
      case Step.Experience:
        return this.submitting() ? 'Pravim nalog…' : 'Napravi nalog';
      case Step.OneRepMax:
        return 'Nastavi na plan';
      default:
        return 'Nastavi';
    }
  });

  protected readonly footnote = computed(() =>
    this.step() === Step.OneRepMax
      ? 'Ne moraš uneti sve — vežbe bez 1RM prvi put loguješ po osećaju.'
      : 'Tvoji podaci ostaju na tvom nalogu.',
  );

  protected togglePassword(): void {
    this.showPassword.update((value) => !value);
  }

  protected setSex(value: string): void {
    this.form.controls.sex.setValue(value);
  }

  protected setExperience(value: string): void {
    this.form.controls.experienceLevel.setValue(value);
  }

  protected setBodyweight(value: number): void {
    this.form.controls.bodyweightKg.setValue(value);
  }

  protected setHeight(value: number): void {
    this.form.controls.heightCm.setValue(value);
  }

  protected setAge(value: number): void {
    this.form.controls.age.setValue(value);
  }

  protected back(): void {
    this.error.set(null);

    if (this.step() === Step.Name) {
      void this.router.navigateByUrl('/login');
      return;
    }

    this.step.update((step) => step - 1);
  }

  protected next(): void {
    if (this.submitting()) {
      return;
    }

    if (!this.canContinue()) {
      this.markCurrentStepTouched();
      return;
    }

    if (this.step() === Step.Experience) {
      this.register();
      return;
    }

    if (this.step() === Step.OneRepMax) {
      void this.router.navigateByUrl('/plan');
      return;
    }

    this.error.set(null);
    this.step.update((step) => step + 1);
  }

  private register(): void {
    const raw = this.form.getRawValue();
    const dto: RegisterDto = {
      email: raw.email.trim(),
      password: raw.password,
      displayName: raw.displayName.trim(),
      sex: raw.sex === '' ? null : (Number(raw.sex) as Sex),
      age: raw.age,
      bodyweightKg: raw.bodyweightKg,
      heightCm: raw.heightCm,
      experienceLevel: Number(raw.experienceLevel) as ExperienceLevel,
      website: raw.website ? raw.website : null,
    };

    this.submitting.set(true);
    this.error.set(null);

    this.auth.register(dto).subscribe({
      next: () => {
        this.submitting.set(false);
        this.step.set(Step.OneRepMax);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(
          extractErrorMessage(err, 'Registracija nije uspela. Proveri podatke i pokušaj ponovo.'),
        );
      },
    });
  }

  /**
   * Greške se pokazuju tek kad korisnik pokuša dalje.
   *
   * Označavanje cele forme bi obojilo i pitanja do kojih se još nije stiglo, pa bi
   * korisnik na prvom ekranu video crveno na poljima koja nije ni video.
   */
  private markCurrentStepTouched(): void {
    const controls = this.form.controls;

    switch (this.step()) {
      case Step.Name:
        controls.displayName.markAsTouched();
        break;
      case Step.Credentials:
        controls.email.markAsTouched();
        controls.password.markAsTouched();
        break;
      case Step.Experience:
        controls.experienceLevel.markAsTouched();
        break;
      default:
        break;
    }
  }
}
