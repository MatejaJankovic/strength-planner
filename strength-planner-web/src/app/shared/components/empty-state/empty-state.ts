import { Component, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.scss',
})
export class EmptyState {
  readonly icon = input('fitness_center');
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly action = input<string | null>(null);
  readonly actionClick = output<void>();
}
