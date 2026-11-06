import { ExercisePlanDto } from '../../core/models/training.models';

/** Koja serija vežbe je kalibraciona; isto kao `CalibrationSet.SetNumber` na serveru. */
export const CALIBRATION_SET_NUMBER = 1;

/**
 * Da li se u unos upravo upisuje kalibraciona serija: prva serija vežbe koju je server
 * označio (`CalibrationSet` - poslednja trenažna nedelja, prva vežba treninga, srednji i
 * napredni nivo).
 *
 * Bez izmene to je sledeća serija kad vežba još nema nijednu. Uz izmenu je to serija koja se
 * menja, ako je prva: ispravka kalibracione serije je i dalje serija do otkaza, pa joj ni
 * napomena o sledećem treningu nema šta da kaže (pregled #110).
 *
 * @param editingSetNumber Redni broj serije koja se menja, ili null kad se upisuje nova.
 */
export function isCalibrationEntry(
  plan: Pick<ExercisePlanDto, 'isCalibration' | 'setLogs'>,
  editingSetNumber: number | null = null,
): boolean {
  if (!plan.isCalibration) {
    return false;
  }

  return editingSetNumber === null
    ? plan.setLogs.length === 0
    : editingSetNumber === CALIBRATION_SET_NUMBER;
}
