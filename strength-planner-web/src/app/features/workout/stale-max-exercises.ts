import { OneRepMaxDto } from '../../core/models/analytics.models';
import { MesocycleDto } from '../../core/models/training.models';
import { staleMaxAgeWeeks } from '../../shared/stale-max-age';

export interface StaleMaxExercise {
  name: string;
  weeks: number;
}

/**
 * Vežbe bloka čija opterećenja još stoje na maksimumu starijem od prozora.
 *
 * Plan tada namerno uzima poslednji poznati maksimum, da vežbač koji se vraća posle pauze
 * ne krene od praznih opterećenja, ali taj broj opisuje vežbača od pre pauze.
 *
 * Broji se samo vežba koja čeka u nekom treningu, a **na tom danu u ovom bloku još nije
 * odrađena**. Samo tada je tvrdnja „plan polazi od starog maksimuma" tačna: završen trening
 * prepisuje opterećenje iste vežbe za isti dan sledeće nedelje (SessionService traži sledeći
 * plan po `DayLabel`), i to iz onoga što je dignuto, a ne iz maksimuma. Zato se gleda par
 * dan + vežba: bench u Upper B prve nedelje i dalje stoji na starom maksimumu kad je Upper A
 * već odrađen. Prva verzija je gledala samo zapise i nabrajala bi i vežbe koje plan više ne
 * vodi po maksimumu.
 */
export function staleMaxExercises(
  plan: Pick<MesocycleDto, 'weeks'>,
  oneRepMaxes: readonly Pick<OneRepMaxDto, 'exerciseId' | 'isOutsideLookback' | 'recordedAt'>[],
  now: Date,
): StaleMaxExercise[] {
  const ageByExercise = new Map<string, number>();
  for (const record of oneRepMaxes) {
    const weeks = staleMaxAgeWeeks(record, now);
    if (weeks !== null) {
      ageByExercise.set(record.exerciseId, weeks);
    }
  }

  const key = (dayLabel: string, exerciseId: string) => `${dayLabel}|${exerciseId}`;
  const done = new Set<string>();
  for (const week of plan.weeks) {
    for (const session of week.sessions) {
      if (session.status === 'Completed') {
        for (const item of session.exercisePlans) {
          done.add(key(session.dayLabel, item.exerciseId));
        }
      }
    }
  }

  const stale = new Map<string, StaleMaxExercise>();
  for (const week of plan.weeks) {
    for (const session of week.sessions) {
      if (session.status !== 'Planned' && session.status !== 'InProgress') {
        continue;
      }
      for (const item of session.exercisePlans) {
        const weeks = ageByExercise.get(item.exerciseId);
        if (weeks !== undefined && !done.has(key(session.dayLabel, item.exerciseId))) {
          stale.set(item.exerciseId, { name: item.exerciseName, weeks });
        }
      }
    }
  }

  return [...stale.values()].sort((a, b) => a.name.localeCompare(b.name));
}
