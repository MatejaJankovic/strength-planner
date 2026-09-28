import { describe, expect, it } from 'vitest';
import { loadInputNote } from './load-input-note';

describe('loadInputNote', () => {
  it('podseća da se kod bučica unosi jedna', () => {
    expect(loadInputNote('Dumbbell', 20)).toBe('single-dumbbell');
  });

  it('ne podseća na to kod šipke, mašine ili sajle', () => {
    expect(loadInputNote('Barbell', 100)).toBeNull();
    expect(loadInputNote('Machine', 60)).toBeNull();
    expect(loadInputNote('Cable', 25)).toBeNull();
  });

  it('upozorava na nulu kod svega što se opterećuje spolja', () => {
    expect(loadInputNote('Barbell', 0)).toBe('zero-on-loaded');
    expect(loadInputNote('Dumbbell', 0)).toBe('zero-on-loaded');
    expect(loadInputNote('Machine', 0)).toBe('zero-on-loaded');
  });

  /**
   * Plank ima spravu „Bodyweight" a udeo telesne mase 0, pa se i loguje na nuli. Kada bi
   * se upozorenje vezalo za „isBodyweight" (koji se izvodi iz udela), plank bi ga dobijao
   * na svakoj seriji — zato se gleda sprava, a ne udeo.
   */
  it('ćuti kod vežbe koju nosi sopstvena masa, uključujući plank na nuli', () => {
    expect(loadInputNote('Bodyweight', 0)).toBeNull();
    expect(loadInputNote('Bodyweight', 10)).toBeNull();
  });

  it('ćuti kada sprava nije poznata', () => {
    expect(loadInputNote(undefined, 0)).toBeNull();
    expect(loadInputNote('', 0)).toBeNull();
  });

  /** Nula je hitnija od podsetnika, pa kod bučice na nuli ide upozorenje. */
  it('kod bučice na nuli javlja grešku, ne podsetnik', () => {
    expect(loadInputNote('Dumbbell', 0)).toBe('zero-on-loaded');
  });
});
