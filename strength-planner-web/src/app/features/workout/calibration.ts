import { ExercisePlanDto } from '../../core/models/training.models';

/**
 * Da li je serija koja se upravo unosi kalibraciona: prva serija vežbe koju je server
 * označio (`CalibrationSet` - poslednja trenažna nedelja, prva vežba treninga, srednji i
 * napredni nivo). Kad je prva serija upisana, ostatak vežbe ide po planu.
 *
 * Izmena već upisane serije nije kalibracija u ovom smislu: unos tada ispravlja seriju
 * koja postoji, pa se ni natpis ni podrazumevana kvačica ne menjaju.
 */
export function isCalibrationPending(
  plan: Pick<ExercisePlanDto, 'isCalibration' | 'setLogs'>,
  isEditing = false,
): boolean {
  return !!plan.isCalibration && plan.setLogs.length === 0 && !isEditing;
}
