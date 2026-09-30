import { describe, expect, it } from 'vitest';
import { Goal } from '../../core/models/training.models';
import { templateDaysFor } from './template-days';

const hypertrophy = [{ name: 'Upper A', exercises: ['Bench Press', 'Cable Fly'] }];
const strength = [{ name: 'Upper A', exercises: ['Bench Press', 'Barbell Row', 'Cable Fly'] }];

describe('templateDaysFor', () => {
  it('bloku snage pokazuje sastav za snagu', () => {
    expect(templateDaysFor({ days: hypertrophy, strengthDays: strength }, Goal.Strength)).toBe(strength);
  });

  it('bloku hipertrofije pokazuje sastav za hipertrofiju', () => {
    expect(templateDaysFor({ days: hypertrophy, strengthDays: strength }, Goal.Hypertrophy)).toBe(hypertrophy);
  });

  it('stariji odgovor bez sastava za snagu pada na isti spisak', () => {
    expect(templateDaysFor({ days: hypertrophy }, Goal.Strength)).toBe(hypertrophy);
  });

  it('prazan šablon ne crta prazan okvir', () => {
    expect(templateDaysFor({ days: [] }, Goal.Hypertrophy)).toBeNull();
    expect(templateDaysFor(undefined, Goal.Strength)).toBeNull();
  });
});
