import { SetAdjustmentDto } from '../../core/models/training.models';

/**
 * Oznaka pored pomerenog predloga serija u rezimeu posle treninga: koji mišić je tražio
 * izmenu, i da li ju je tražila nedelja ili jedan pretrpan trening.
 *
 * Razlika nije ukras. Kada granica serija po treningu spusti vežbu, nedelja tog mišića je
 * često **ispod** cilja - oznaka koja kaže samo „Chest" pored strelice nadole tada zvuči kao
 * da je grudi bilo previše za nedelju, a bilo ih je previše za jedan trening.
 */
export function adjustmentCause(change: Pick<SetAdjustmentDto, 'muscle' | 'reason'>): string | null {
  if (!change.muscle) {
    return null;
  }

  return change.reason === 'SessionCeiling' ? `${change.muscle} · pun trening` : change.muscle;
}
