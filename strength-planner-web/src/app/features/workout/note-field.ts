import { Component, inject, input, linkedSignal, signal } from '@angular/core';
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
 */
@Component({
  selector: 'app-note-field',
  imports: [MatIconModule],
  templateUrl: './note-field.html',
  styleUrl: './note-field.scss',
})
export class NoteField {
  private readonly notes = inject(NoteService);

  readonly label = input.required<string>();
  readonly kind = input.required<'session' | 'exercise'>();
  /** Id treninga, ili id vežbe, zavisno od `kind`. */
  readonly targetId = input.required<string>();
  /** Beleška koju je server poslao. */
  readonly value = input<string | null | undefined>(null);

  protected readonly maxLength = NOTE_MAX_LENGTH;
  protected readonly fieldId = `note-field-${nextId++}`;

  /**
   * Sačuvana beleška. Prati ono što server pošalje, a posle uspešnog čuvanja drži novu vrednost,
   * pa se ne vraća na staru dok se ekran ne učita ponovo.
   */
  protected readonly saved = linkedSignal<string | null>(() => this.value() ?? null);

  protected readonly editing = signal(false);
  protected readonly draft = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected edit(): void {
    this.draft.set(this.saved() ?? '');
    this.error.set(null);
    this.editing.set(true);
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

    const note = this.draft();
    const request =
      this.kind() === 'session'
        ? this.notes.setSessionNote(this.targetId(), note)
        : this.notes.setExerciseNote(this.targetId(), note);

    request.subscribe({
      next: (result) => {
        this.saved.set(result.note);
        this.saving.set(false);
        this.editing.set(false);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(extractErrorMessage(err, 'Beleška nije sačuvana. Pokušaj ponovo.'));
      },
    });
  }
}
