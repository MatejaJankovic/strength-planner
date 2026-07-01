import { Component, input } from '@angular/core';

export type StatChipTone = 'neutral' | 'below' | 'optimal' | 'above' | 'accent';

@Component({
  selector: 'app-stat-chip',
  templateUrl: './stat-chip.html',
  styleUrl: './stat-chip.scss',
})
export class StatChip {
  readonly label = input.required<string>();
  readonly value = input<string | number | null>(null);
  readonly tone = input<StatChipTone>('neutral');
}
