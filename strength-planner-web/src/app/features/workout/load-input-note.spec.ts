import { describe, expect, it } from 'vitest';
import { loadInputNote, zeroLoadWarning } from './load-input-note';

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
    expect(loadInputNote('Barbell', 0)).toBe('zero-on-barbell');
    expect(loadInputNote('Dumbbell', 0)).toBe('zero-on-loaded');
    expect(loadInputNote('Machine', 0)).toBe('zero-on-loaded');
    expect(loadInputNote('Cable', 0)).toBe('zero-on-loaded');
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

  /**
   * Samo šipka ima težinu i kad je prazna - rečenica o šipci uz sajlu ili bučicu bila bi
   * netačna, a tako je stajala na ekranu od runde 13. Proverava se i izbor i sam tekst: greška
   * je tada bila u rečenici, ne u izboru.
   */
  it('pominje praznu šipku samo kod šipke', () => {
    for (const equipment of ['Barbell', 'Cable', 'Dumbbell', 'Machine']) {
      const note = loadInputNote(equipment, 0);
      expect(note === 'zero-on-barbell' || note === 'zero-on-loaded').toBe(true);

      const text = zeroLoadWarning(note as 'zero-on-barbell' | 'zero-on-loaded');
      expect(text.includes('šipk')).toBe(equipment === 'Barbell');
      expect(text).toContain('0 kg');
    }
  });

  it('ne tvrdi 20 kg kao težinu vežbe - EZ šipka je oko pola toga', () => {
    expect(zeroLoadWarning('zero-on-barbell')).not.toMatch(/šipka je (već )?oko 20 kg/);
  });

  it('spravu čita bez obzira na velika i mala slova, kao i server', () => {
    expect(loadInputNote('barbell', 0)).toBe('zero-on-barbell');
    expect(loadInputNote(' DUMBBELL ', 12)).toBe('single-dumbbell');
    expect(loadInputNote('bodyweight', 0)).toBeNull();
  });

  /** Nula je hitnija od podsetnika, pa kod bučice na nuli ide upozorenje. */
  it('kod bučice na nuli javlja grešku, ne podsetnik', () => {
    expect(loadInputNote('Dumbbell', 0)).toBe('zero-on-loaded');
  });
});
