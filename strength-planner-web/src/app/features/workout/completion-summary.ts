import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CompleteSessionResultDto, CompletedExerciseSummaryDto } from '../../core/models/training.models';
import { StatChip, StatChipTone } from '../../shared/components/stat-chip/stat-chip';
import { adjustmentCause } from './adjustment-cause';
import { LoadFloorNote, loadFloorNote } from './load-floor-note';
import { nextWeightLabel, nextWeightTone } from './next-weight-label';

/**
 * „Trening završen": šta je završetak treninga promenio. Izdvojeno iz ekrana treninga da bi
 * taj ekran ostao ispod tvrde granice veličine stila (`anyComponentStyle`, 8 kB), a rezime
 * ima svoj stil i svoje pomoćne funkcije.
 */
@Component({
  selector: 'app-completion-summary',
  imports: [DecimalPipe, MatIconModule, StatChip],
  templateUrl: './completion-summary.html',
  styleUrl: './completion-summary.scss',
})
export class CompletionSummary {
  readonly outcome = input.required<CompleteSessionResultDto>();

  /** Naslov: „Trening završen“, ili „Ispravka sačuvana“ kad se rezime čita posle ispravke. */
  readonly title = input('Trening završen');

  /** Oznaka „Sledeće": predlog i razlika prema podignutoj težini. */
  protected nextWeight(summary: CompletedExerciseSummaryDto): string {
    return nextWeightLabel(summary);
  }

  protected nextWeightTone(summary: CompletedExerciseSummaryDto): StatChipTone {
    return nextWeightTone(summary);
  }

  /**
   * Zašto rezime kaže da sledeći put nema kilograma više (vidi load-floor-note.ts).
   * Server u oba slučaja vraća istu zastavicu, a uzrok se čita iz dela telesne mase.
   */
  protected floorNote(summary: CompletedExerciseSummaryDto): LoadFloorNote | null {
    return loadFloorNote(summary);
  }

  /** Oznaka uz pomeren predlog serija: mišić, i "pun trening" kad ga je spustila granica. */
  protected readonly adjustmentCause = adjustmentCause;
}
