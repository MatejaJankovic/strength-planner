import { Goal, PeriodizationModel } from './training.models';

/** Naziv cilja bloka na ekranu; isti na svakom ekranu koji ga prikazuje. */
export function goalLabel(goal: Goal): string {
  switch (goal) {
    case Goal.Strength:
      return 'Snaga';
    case Goal.Maintenance:
      return 'Održavanje';
    default:
      return 'Hipertrofija';
  }
}

/**
 * Cilj u rečenici „nastavlja sa ciljem …". Server ga šalje kao tekst ("Strength"), ne kao broj,
 * pa se ovde čita tekst; nepoznata vrednost se tretira kao hipertrofija, kao i pre.
 */
export function goalGenitive(goal: string): string {
  switch (goal) {
    case 'Strength':
      return 'snage';
    case 'Maintenance':
      return 'održavanja';
    default:
      return 'hipertrofije';
  }
}

/**
 * Cilj novog bloka koji se dodaje iza `last`: smenjuje hipertrofiju i snagu, a plan održavanja
 * ostaje održavanje. Isto pravilo kao MacrocyclePlanner.AlternatingGoals na serveru.
 */
export function goalAfter(last: Goal): Goal {
  switch (last) {
    case Goal.Maintenance:
      return Goal.Maintenance;
    case Goal.Hypertrophy:
      return Goal.Strength;
    default:
      return Goal.Hypertrophy;
  }
}

/** Blok održavanja je uvek ravan; vidi Periodization.ModelFor na serveru. */
export function fixesTheModel(goal: Goal): boolean {
  return goal === Goal.Maintenance;
}

/** Model koji blok sa datim ciljem stvarno dobija, kad je traženo `requested`. */
export function modelFor(goal: Goal, requested: PeriodizationModel): PeriodizationModel {
  return fixesTheModel(goal) ? PeriodizationModel.Flat : requested;
}
