import { Component, computed, input, output } from '@angular/core';
import { DecimalPipe } from '@angular/common';

// Tolerancija za poređenje sa mrežom koraka (npr. 82.5 / 2.5 ne sme da promaši zbog float-a).
const GRID_EPSILON = 1e-9;

@Component({
  selector: 'app-weight-stepper',
  imports: [DecimalPipe],
  templateUrl: './weight-stepper.html',
  styleUrl: './weight-stepper.scss',
})
export class WeightStepper {
  readonly value = input(0);
  readonly step = input(2.5);
  readonly min = input(0);
  readonly label = input('Ciljno opterećenje');
  readonly disabled = input(false);
  readonly valueChange = output<number>();

  readonly canDecrease = computed(() => !this.disabled() && this.snapped(-1) >= this.min());

  /** Korak u tekstu dugmadi, bez veštačkog zaokruživanja (1.25 ostaje "1.25"). */
  readonly stepLabel = computed(() => String(this.step()));

  decrease(): void {
    if (!this.canDecrease()) {
      return;
    }

    this.valueChange.emit(this.snapped(-1));
  }

  increase(): void {
    if (this.disabled()) {
      return;
    }

    this.valueChange.emit(this.snapped(1));
  }

  /**
   * Sledeća vrednost na mreži koraka u traženom smeru. Zaokruživanje na najbližu
   * vrednost ovde ne valja: kada tekuća težina nije na mreži (npr. 82.5 kg uz korak
   * od 5 kg, posle promene koraka), "+" bi skočio 7.5 kg a "−" pao samo 2.5 kg.
   */
  private snapped(direction: 1 | -1): number {
    const step = this.step();
    const grid = this.value() / step;
    const next =
      direction === 1
        ? Math.floor(grid + GRID_EPSILON) + 1
        : Math.ceil(grid - GRID_EPSILON) - 1;

    // toFixed skida float drift kod koraka koji nisu stepen dvojke (npr. 2.33).
    return Number((next * step).toFixed(2));
  }
}
