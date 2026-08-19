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
  REGISTRATION_STEP_COUNT,
  RegisterDto,
  Sex,
} from '../../core/models/auth.models';
import { ChoiceList, ChoiceOption } from '../../shared/components/choice-list/choice-list';
import { MeasureInput } from '../../shared/components/measure-input/measure-input';
import { WizardShell } from '../../shared/components/wizard-shell/wizard-shell';

/** Koraci, redom. Vrednosti se nigde ne upisuju, pa smeju da se premeštaju. */
enum Step {
  Name,
  Credentials,
  Sex,
  Bodyweight,
  Height,
  Age,
  Experience,
}

/**
 * Vrednost kartice „Ne želim da navedem".
 *
 * Ne može da bude prazan string, jer `ChoiceList` prazno tumači kao „ništa nije
 * izabrano" i tada ne bi bilo razlike između neodgovorenog i odbijenog pitanja.
 */
const DECLINED_SEX = 'declined';

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
 * Osmi i poslednji korak — unos maksimuma (1RM) — ne stoji ovde nego na `/onboarding`.
 * Traži katalog vežbi sa servera, dakle postojeći token, pa mora da bude posle
 * registracije; a da je ostao na ovoj ruti, osvežavanje stranice tokom njega vraćalo bi
 * već prijavljenog korisnika na prvo pitanje čarobnjaka. Traka napretka je ista na oba
 * ekrana jer oba čitaju `REGISTRATION_STEP_COUNT`.
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
  ],
  templateUrl: './register-wizard.html',
  styleUrl: './register-wizard.scss',
})
export class RegisterWizard {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly Step = Step;
  protected readonly stepCount = REGISTRATION_STEP_COUNT;

  protected readonly step = signal(Step.Name);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  /**
   * Napomena na ekranima sa merama.
   *
   * Klizač mora od nečega da krene, pa su masa, visina i uzrast unaprijed popunjeni i
   * dugme „Nastavi" radi bez dodira. Bez ove napomene profil tvrdi tri mere koje korisnik
   * nikada nije izgovorio, i nigde ne piše da su procena. Formular koji je čarobnjak
   * zamenio je obe tražio izričito (`Validators.required`).
   */
  protected readonly prefilledNote = 'Vrednost je unapred popunjena - pomeri klizač ako nije tačna.';

  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;
  protected readonly displayNameMaxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly heightMin = HEIGHT_MIN_CM;
  protected readonly heightMax = HEIGHT_MAX_CM;

  /**
   * Poslednja ponuda je izričito odbijanje odgovora.
   *
   * Bez nje se izbor ne može poništiti: kartice samo postavljaju vrednost, pa ko jednom
   * dodirne „Muški" nema više načina da se vrati na neizjašnjeno. Pol je na serveru
   * nullable upravo zato da sme da se ne navede, pa bi ekran bez ove ponude bio stroži
   * od podatka koji čuva.
   */
  protected readonly sexOptions: ReadonlyArray<ChoiceOption> = [
    { value: String(Sex.Male), label: 'Muški', icon: 'male' },
    { value: String(Sex.Female), label: 'Ženski', icon: 'female' },
    { value: DECLINED_SEX, label: 'Ne želim da navedem', icon: 'do_not_disturb_on' },
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
        return true;
    }
  });

  protected readonly continueLabel = computed(() =>
    this.step() === Step.Experience
      ? this.submitting()
        ? 'Pravim nalog…'
        : 'Napravi nalog'
      : 'Nastavi',
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

    this.error.set(null);
    this.step.update((step) => step + 1);
  }

  private register(): void {
    const raw = this.form.getRawValue();
    const dto: RegisterDto = {
      email: raw.email.trim(),
      password: raw.password,
      displayName: raw.displayName.trim(),
      sex: raw.sex === '' || raw.sex === DECLINED_SEX ? null : (Number(raw.sex) as Sex),
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
        // Osmi korak je na svojoj ruti; `wizard=1` mu kaže da nosi opremu čarobnjaka
        // (traku napretka i dugme „Nastavi na plan") umesto svog samostalnog zaglavlja.
        void this.router.navigate(['/onboarding'], { queryParams: { wizard: 1 } });
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
