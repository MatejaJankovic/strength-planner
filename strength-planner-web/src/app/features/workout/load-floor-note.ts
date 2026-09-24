import { CompletedExerciseSummaryDto } from '../../core/models/training.models';

/**
 * Zašto rezime kaže da sledeći put nema kilograma više.
 *
 * `loadFloorReached` na serveru ima dva različita uzroka i ne razlikuje ih (vidi
 * `ProgressionResult.LoadFloorReached`): pravilo je htelo manje od tela vežbača, ili nije
 * bilo nikakvog opterećenja. Napomena je stajala samo za prvi uzrok, a prikazivala se za
 * oba — plank upisan sa 0 kg dobijao je rečenicu o sopstvenoj masi, koju ta vežba ne nosi.
 *
 * Razlikuje ih deo telesne mase, koji rezime i nosi: `isBodyweight` je `bodyweightLoadKg > 0`.
 */
export type LoadFloorNote = 'at-bodyweight' | 'unloaded';

export function loadFloorNote(
  summary: Pick<CompletedExerciseSummaryDto, 'loadFloorReached' | 'isBodyweight'>,
): LoadFloorNote | null {
  if (!summary.loadFloorReached) {
    return null;
  }

  return summary.isBodyweight ? 'at-bodyweight' : 'unloaded';
}
