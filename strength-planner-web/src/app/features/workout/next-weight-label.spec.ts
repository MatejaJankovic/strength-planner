import { nextWeightLabel, nextWeightTone } from './next-weight-label';

/**
 * Oznaka „Sledeće" je do sada nosila strelicu naviše iz zastavice „sve serije su na vrhu
 * opsega", pa je pred planirani deload pisalo „90 kg ↑". Ovi testovi pokrivaju nulu i
 * nedostajuću vrednost, koje se u izrazu unutar šablona lako pomešaju.
 */
describe('nextWeightLabel', () => {
  it('prikazuje razliku kada predlog raste', () => {
    expect(nextWeightLabel({ nextWeightKg: 102.5, weightChangeKg: 2.5 })).toBe('102.5 kg ↑ +2.5');
  });

  it('prikazuje pad pred deload', () => {
    expect(nextWeightLabel({ nextWeightKg: 90, weightChangeKg: -10 })).toBe('90 kg ↓ −10');
  });

  it('bez strelice kada se težina zadržava', () => {
    expect(nextWeightLabel({ nextWeightKg: 100, weightChangeKg: 0 })).toBe('100 kg');
  });

  it('bez strelice kada nema sa čim da se uporedi', () => {
    // Vežba koja nije imala ni upisane serije ni planiranu težinu.
    expect(nextWeightLabel({ nextWeightKg: 80, weightChangeKg: null })).toBe('80 kg');
  });

  it('prazan tekst kada naredne nedelje nema', () => {
    expect(nextWeightLabel({ nextWeightKg: null, weightChangeKg: null })).toBe('');
  });

  it('naglašava samo rast', () => {
    expect(nextWeightTone({ weightChangeKg: 2.5 })).toBe('accent');
    expect(nextWeightTone({ weightChangeKg: -10 })).toBe('neutral');
    expect(nextWeightTone({ weightChangeKg: 0 })).toBe('neutral');
    expect(nextWeightTone({ weightChangeKg: null })).toBe('neutral');
  });
});
