import { Component, computed, input, output } from '@angular/core';
import { DecimalPipe } from '@angular/common';

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

  readonly canDecrease = computed(() => !this.disabled() && this.value() - this.step() >= this.min());

  decrease(): void {
    if (!this.canDecrease()) {
      return;
    }

    this.valueChange.emit(this.roundToStep(this.value() - this.step()));
  }

  increase(): void {
    if (this.disabled()) {
      return;
    }

    this.valueChange.emit(this.roundToStep(this.value() + this.step()));
  }

  private roundToStep(value: number): number {
    return Math.round(value / this.step()) * this.step();
  }
}
