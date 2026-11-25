import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { NoteService } from '../../core/api/note.service';

/** Mora da prati `NotePolicy.MaximumLength` na serveru: inače korisnik upiše više nego što server prima. */
export const NOTE_MAX_LENGTH = 500;

let nextId = 0;

/**
 * Beleška koju korisnik piše sam: jedna uz trening (kako je prošao) ili jedna uz vežbu (podešavanje
 * sprave, napomena koja štiti rame). Beleška uz vežbu pripada vežbi, ne planu, pa se vidi pri svakom
 * njenom treningu. Ni jedna nije podatak o treningu: može se pisati i na završenom i preskočenom,
 * i ništa ne preračunava.
 *
 * Isti komponent ostaje na kartici kad se vežba zameni (kartica je vezana za plan, a plan zadržava id),
 * pa sve što je vezano za vežbu mora da se vrati na početak kad se `targetId` promeni: prikazana
 * beleška, otvorena izmena i rezultat čuvanja koji je krenuo za staru vežbu.
 */
@Component({
  selector: 'app-note-field',
  imports: [MatIconModule],
  templateUrl: './note-field.html',
  styleUrl: './note-field.scss',
})
export class NoteField {
  private readonly notes = inject(NoteService);
  private readonly injector = inject(Injector);
  private readonly field = viewChild<ElementRef<HTMLTextAreaElement>>('field');

  readonly label = input.required<string>();
  /** Tekst dugmeta kad beleške još nema („Dodaj belešku uz vežbu"): padež se ne može izvesti iz naslova. */
  readonly addLabel = input.required<string>();
  readonly kind = input.required<'session' | 'exercise'>();
  /** Id treninga, ili id vežbe, zavisno od `kind`. */
  readonly targetId = input.required<string>();
  /** Beleška koju je server poslao. */
  readonly value = input<string | null | undefined>(null);

  protected readonly maxLength = NOTE_MAX_LENGTH;
  protected readonly fieldId = `note-field-${nextId++}`;

  /**
   * Sačuvana beleška. Prati ono što server pošalje, a posle uspešnog čuvanja drži novu vrednost,
   * pa se ne vraća na staru dok se ekran ne učita ponovo. Čita i `targetId`: posle zamene vežbe
   * server može da pošalje isto `null` kao pre nje, a to samo po sebi ne bi poništilo belešku stare
   * vežbe, koja bi ostala na kartici nove.
   */
  protected readonly saved = linkedSignal<string | null>(() => {
    this.targetId();
    return this.value() ?? null;
  });

  /** Otvorena izmena ne sme da preživi zamenu vežbe: sačuvala bi otkucano uz novu vežbu. */
  protected readonly editing = linkedSignal(() => {
    this.targetId();
    return false;
  });

  protected readonly draft = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected edit(): void {
    this.draft.set(this.saved() ?? '');
    this.error.set(null);
    this.editing.set(true);

    // Polje se pojavljuje tek posle sledećeg iscrtavanja. Fokus odmah otvara tastaturu na telefonu, pa
    // dodir na „Dodaj belešku" ne traži još jedan dodir na polje.
    afterNextRender(() => this.field()?.nativeElement.focus(), { injector: this.injector });
  }

  protected cancel(): void {
    this.editing.set(false);
    this.error.set(null);
  }

  protected setDraft(text: string): void {
    this.draft.set(text);
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    const target = this.targetId();
    const note = this.draft();
    const request =
      this.kind() === 'session'
        ? this.notes.setSessionNote(target, note)
        : this.notes.setExerciseNote(target, note);

    request.subscribe({
      next: (result) => {
        this.saving.set(false);

        // Vežba je u međuvremenu zamenjena: ovo je odgovor za staru, pa ne sme da se prikaže uz novu.
        if (this.targetId() !== target) {
          return;
        }

        this.saved.set(result.note);
        this.editing.set(false);
      },
      error: (err: unknown) => {
        this.saving.set(false);

        if (this.targetId() === target) {
          this.error.set(extractErrorMessage(err, 'Beleška nije sačuvana. Pokušaj ponovo.'));
        }
      },
    });
  }
}
