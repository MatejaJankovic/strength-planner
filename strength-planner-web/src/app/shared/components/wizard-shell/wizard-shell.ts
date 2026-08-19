import { Component, computed, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Okvir jednog koraka čarobnjaka: strelica nazad, traka napretka, pitanje kao naslov,
 * telo koraka i dugme za nastavak zalepljeno za dno.
 *
 * Sadržaj koraka ulazi kroz projekciju, pa svaki korak ostaje samo svoje pitanje —
 * zaglavlje, traka i dugme se ne prepisuju sedam puta.
 */
@Component({
  selector: 'app-wizard-shell',
  imports: [MatIconModule],
  templateUrl: './wizard-shell.html',
  styleUrl: './wizard-shell.scss',
})
export class WizardShell {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly stepIndex = input.required<number>();
  readonly stepCount = input.required<number>();
  readonly continueLabel = input('Nastavi');
  readonly canContinue = input(true);
  readonly busy = input(false);
  readonly footnote = input<string | null>(null);

  /**
   * Poslednji korak nema povratak: nalog je do tada već napravljen, pa bi strelica nazad
   * vodila na pitanja koja više nemaju gde da se pošalju.
   */
  readonly showBack = input(true);

  readonly back = output<void>();
  readonly next = output<void>();

  /** Segmenti trake napretka — jedan po koraku, kao na uzoru. */
  protected readonly segments = computed(() =>
    Array.from({ length: this.stepCount() }, (_, index) => index),
  );

  protected readonly progressLabel = computed(
    () => `Korak ${this.stepIndex() + 1} od ${this.stepCount()}`,
  );
}
