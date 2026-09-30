import { describe, expect, it } from 'vitest';
import { adjustmentCause } from './adjustment-cause';

describe('adjustmentCause', () => {
  it('imenuje mišić kada izmenu traži nedelja', () => {
    expect(adjustmentCause({ muscle: 'Back', reason: 'WeeklyTarget' })).toBe('Back');
  });

  it('kaže da je trening pun kada izmenu traži granica po treningu', () => {
    expect(adjustmentCause({ muscle: 'Chest', reason: 'SessionCeiling' })).toBe('Chest · pun trening');
  });

  it('ne izmišlja razlog kada ga server nije dao', () => {
    expect(adjustmentCause({ muscle: null, reason: null })).toBeNull();
    expect(adjustmentCause({ muscle: undefined, reason: 'SessionCeiling' })).toBeNull();
  });

  it('odgovor starijeg servera bez razloga čita kao nedeljni', () => {
    expect(adjustmentCause({ muscle: 'Quads' })).toBe('Quads');
  });
});
