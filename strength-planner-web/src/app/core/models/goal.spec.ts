import { fixesTheModel, goalAfter, goalGenitive, goalLabel, modelFor } from './goal';
import { Goal, PeriodizationModel } from './training.models';

describe('cilj bloka', () => {
  it('ima naziv za svaku vrednost', () => {
    expect(goalLabel(Goal.Strength)).toBe('Snaga');
    expect(goalLabel(Goal.Hypertrophy)).toBe('Hipertrofija');
    expect(goalLabel(Goal.Maintenance)).toBe('Održavanje');
  });

  /** Server šalje cilj sledećeg bloka kao tekst; nepoznata vrednost je hipertrofija, kao i pre. */
  it('čita cilj sledećeg bloka iz teksta', () => {
    expect(goalGenitive('Strength')).toBe('snage');
    expect(goalGenitive('Hypertrophy')).toBe('hipertrofije');
    expect(goalGenitive('Maintenance')).toBe('održavanja');
    expect(goalGenitive('Nešto')).toBe('hipertrofije');
  });

  /** Isto pravilo kao MacrocyclePlanner.AlternatingGoals: hipertrofija i snaga se smenjuju, održavanje se ponavlja. */
  it('iza održavanja dolazi održavanje, a ostali se smenjuju', () => {
    expect(goalAfter(Goal.Hypertrophy)).toBe(Goal.Strength);
    expect(goalAfter(Goal.Strength)).toBe(Goal.Hypertrophy);
    expect(goalAfter(Goal.Maintenance)).toBe(Goal.Maintenance);
  });

  it('održavanje je uvek ravno, ostali ciljevi zadržavaju traženi model', () => {
    for (const model of [
      PeriodizationModel.Flat,
      PeriodizationModel.Linear,
      PeriodizationModel.LinearRising,
      PeriodizationModel.Inverse,
    ]) {
      expect(modelFor(Goal.Maintenance, model)).toBe(PeriodizationModel.Flat);
      expect(modelFor(Goal.Hypertrophy, model)).toBe(model);
      expect(modelFor(Goal.Strength, model)).toBe(model);
    }

    expect(fixesTheModel(Goal.Maintenance)).toBe(true);
    expect(fixesTheModel(Goal.Strength)).toBe(false);
    expect(fixesTheModel(Goal.Hypertrophy)).toBe(false);
  });
});
