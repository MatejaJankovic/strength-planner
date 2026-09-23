import { nextWeightLabel, nextWeightTone } from './next-weight-label';

/**
 * Oznaka „Sledeće" je do sada nosila strelicu naviše iz zastavice „sve serije su na vrhu
 * opsega", pa je pred planirani deload pisalo „90 kg ↑". Ovi testovi pokrivaju nulu i
 * nedostajuću vrednost, koje se u izrazu unutar šablona lako pomešaju.
 */
describe('nextWeightLabel', () => {
  it('prikazuje razliku kada predlog raste', () => {
    expect(nextWeightLabel({ nextWeightKg: 102.5, weightChangeKg: 2.5, isBodyweight: false })).toBe('102.5 kg ↑ +2.5');
  });

  it('prikazuje pad pred deload', () => {
    expect(nextWeightLabel({ nextWeightKg: 90, weightChangeKg: -10, isBodyweight: false })).toBe('90 kg ↓ −10');
  });

  it('bez strelice kada se težina zadržava', () => {
    expect(nextWeightLabel({ nextWeightKg: 100, weightChangeKg: 0, isBodyweight: false })).toBe('100 kg');
  });

  it('bez strelice kada nema sa čim da se uporedi', () => {
    // Vežba koja nije imala ni upisane serije ni planiranu težinu.
    expect(nextWeightLabel({ nextWeightKg: 80, weightChangeKg: null, isBodyweight: false })).toBe('80 kg');
  });

  it('prazan tekst kada naredne nedelje nema', () => {
    expect(nextWeightLabel({ nextWeightKg: null, weightChangeKg: null, isBodyweight: false })).toBe('');
  });

  it('vezbu sa telesnom masom pise kao dodato opterecenje', () => {
    // Zgib: predlog je ono sto ide na pojas, a ne ukupno opterecenje.
    expect(
      nextWeightLabel({ nextWeightKg: 17.5, weightChangeKg: 7.5, isBodyweight: true }),
    ).toBe('TM + 17.5 kg ↑ +7.5');
  });

  it('nula dodatnih kilograma je sopstvena masa, ne prazno polje', () => {
    // Pravilo je htelo manje od tela samog: nema sta da se skine, pa se propisuje TM.
    expect(nextWeightLabel({ nextWeightKg: 0, weightChangeKg: -5, isBodyweight: true })).toBe(
      'TM ↓ −5',
    );
  });

  it('naglašava samo rast', () => {
    expect(nextWeightTone({ weightChangeKg: 2.5 })).toBe('accent');
    expect(nextWeightTone({ weightChangeKg: -10 })).toBe('neutral');
    expect(nextWeightTone({ weightChangeKg: 0 })).toBe('neutral');
    expect(nextWeightTone({ weightChangeKg: null })).toBe('neutral');
  });
});
