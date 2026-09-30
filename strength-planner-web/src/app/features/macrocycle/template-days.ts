import { Goal, WorkoutTemplateDto } from '../../core/models/training.models';

/**
 * Dani šablona onako kako će ih blok dobiti. Blok snage ima najmanje dve složene vežbe po
 * treningu, pa se ugrađen šablon za njega skraćuje drugačije nego za hipertrofiju - prikaz
 * koji bi to prećutao obećavao bi naprednom vežbaču jednu složenu vežbu, a davao dve.
 *
 * Prazan spisak se vraća kao null da se ne bi prikazao prazan okvir (rezervni šablon nema
 * dane).
 */
export function templateDaysFor(
  template: Pick<WorkoutTemplateDto, 'days' | 'strengthDays'> | undefined,
  goal: Goal,
): WorkoutTemplateDto['days'] | null {
  const days = goal === Goal.Strength && template?.strengthDays?.length ? template.strengthDays : template?.days;

  return days && days.length > 0 ? days : null;
}
