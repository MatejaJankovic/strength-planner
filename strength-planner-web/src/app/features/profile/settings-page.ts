import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { extractErrorMessage } from '../../core/api/http-error';
import { AuthService } from '../../core/auth/auth.service';
import {
  ACCOUNT_DELETION_WORD,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
} from '../../core/models/auth.models';
import { SubscreenHeader } from '../../shared/components/subscreen-header/subscreen-header';

/**
 * Podešavanja naloga: lozinka, odjava i brisanje naloga.
 *
 * Sve tri stvari dele jedno svojstvo — tiču se pristupa nalogu, a ne trenažnih podataka —
 * pa su izdvojene sa ekrana profila, koji je sada pregled i ulaz u ostale ekrane.
 */
@Component({
  selector: 'app-settings-page',
  imports: [ReactiveFormsModule, MatIconModule, SubscreenHeader],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.currentUser;

  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;
  protected readonly deletionWord = ACCOUNT_DELETION_WORD;

  // --- lozinka ---------------------------------------------------------------

  protected readonly savingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly passwordSaved = signal(false);

  /**
   * Potvrda nove lozinke nije formalnost: u sistemu nema oporavka lozinke, pa greška u
   * kucanju znači trajan gubitak naloga i svih podataka u njemu.
   */
  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(PASSWORD_MIN_LENGTH),
          Validators.maxLength(PASSWORD_MAX_LENGTH),
        ],
      ],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingPasswords] },
  );

  // --- brisanje naloga -------------------------------------------------------

  protected readonly deleteOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  protected readonly deleteForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    confirmation: ['', [Validators.required]],
  });

  /**
   * Vrednost forme kao signal.
   *
   * `computed` prati samo signale, a `AbstractControl.valid` nije signal — bez ovoga se
   * `canDelete` izračuna jednom, nad praznom formom, i dugme nikada ne oživi. Ista greška
   * je već bila u čarobnjaku za registraciju i tamo se videla samo na ekranu.
   */
  private readonly deleteValue = toSignal(this.deleteForm.valueChanges, {
    initialValue: this.deleteForm.getRawValue(),
  });

  /**
   * Dugme za brisanje radi samo kada su i lozinka i tačna reč potvrde upisane.
   *
   * Reč se proverava i ovde, ne samo na serveru: dugme koje se može pritisnuti sa
   * pogrešnom rečju traži od korisnika da otkrije pravilo iz poruke o grešci.
   *
   * `toUpperCase` bez jezika je namerno. `toLocaleUpperCase('sr')` je vezivao pravilo za
   * jezik, a server poredi ordinalno — dve strane bi mogle da se raziđu na istom unosu
   * iako im je reč ista, i to je tačno onaj kvar zbog kog je poređenje na serveru i
   * promenjeno (na turskom „i" i „I" nisu isto slovo, a reč se završava tim slovom).
   */
  protected readonly canDelete = computed(() => {
    const value = this.deleteValue();

    return (
      (value.currentPassword ?? '').length > 0 &&
      (value.confirmation ?? '').trim().toUpperCase() === ACCOUNT_DELETION_WORD
    );
  });

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

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();

    this.auth.changePassword({ currentPassword, newPassword }).subscribe({
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

  protected openDelete(): void {
    this.deleteOpen.set(true);
  }

  protected cancelDelete(): void {
    this.deleteOpen.set(false);
    this.deleteError.set(null);
    // Lozinka ne sme da ostane u formi posle zatvaranja.
    this.deleteForm.reset();
  }

  protected deleteAccount(): void {
    if (this.deleting() || !this.canDelete()) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    // Uspeh ne vraća ništa u ovaj ekran: `deleteAccount` u servisu odjavljuje i
    // preusmerava na prijavu, jer ista putanja mora da isprazni token, keševe i sliku kao
    // i obična odjava. Zato ovde nema `next` — pisati u signal komponente koje više nema
    // izgleda kao da nešto radi.
    this.auth.deleteAccount(this.deleteForm.getRawValue()).subscribe({
      error: (err: unknown) => {
        this.deleting.set(false);
        this.deleteError.set(
          extractErrorMessage(err, 'Nalog nije obrisan. Proveri lozinku i pokušaj ponovo.'),
        );
      },
    });
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected back(): void {
    void this.router.navigateByUrl('/profile');
  }
}

/** Nova lozinka i potvrda moraju da se poklope. */
function matchingPasswords(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmation = group.get('confirmPassword')?.value;

  return password && confirmation && password !== confirmation ? { passwordMismatch: true } : null;
}
