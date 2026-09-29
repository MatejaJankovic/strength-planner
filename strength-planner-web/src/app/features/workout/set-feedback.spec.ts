import { setFeedback } from './set-feedback';

/**
 * Napomena ispod unosa serije je obećanje o sledećem treningu, pa mora da prati pravilo
 * sa servera. Stara napomena je za otkaz na vrhu opsega tvrdila „zadržava se", a server
 * je iznad ~125 kg spuštao težinu - to se na snimku ekrana ne vidi.
 */
describe('setFeedback', () => {
  // Šipka od 100 kg: korak staje u propis, pa važe napomene iz runda 9 i 10.
  const bar = { weightStepKg: 2.5, bodyweightLoadKg: 0 };
  const hypertrophy = { repRangeMin: 8, repRangeMax: 12, targetRir: 1, ...bar };
  // Uzak opseg više ne dolazi iz periodizacije - faza volumena hipertrofije se od
  // ispravke Epley granice propisuje kao 8-12 - nego iz ličnog šablona i iz nedelje
  // intenziteta snage (3-4 @RIR3).
  const narrowRange = { repRangeMin: 11, repRangeMax: 12, targetRir: 2, ...bar };
  const fixedReps = { repRangeMin: 5, repRangeMax: 5, targetRir: 2, ...bar };

  it('najavljuje korak za otkaz na vrhu običnog opsega', () => {
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 12, rir: 0, isFailure: true })).toBe('failure-at-top');
  });

  it('najavljuje zadržavanje za otkaz na vrhu uskog opsega sa većim ciljnim RIR-om', () => {
    expect(setFeedback(narrowRange, { weightKg: 100, reps: 12, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
  });

  it('fiksan broj ponavljanja tretira kao krajnji slučaj uskog opsega', () => {
    // 5x5 @RIR2: širina nula, pa korak nosi samo rezerva. Otkaz na petom ponavljanju je
    // teže od plana i težina se zadržava.
    expect(setFeedback(fixedReps, { weightKg: 100, reps: 5, rir: 2, isFailure: false })).toBe('at-top-narrow');
    expect(setFeedback(fixedReps, { weightKg: 100, reps: 5, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
    // Ispod propisanog broja se i dalje meri kapacitet: 4 + 2 = 6 naspram 5 + 2.
    expect(setFeedback(fixedReps, { weightKg: 100, reps: 4, rir: 2, isFailure: false })).toBe(
      'below-range-with-reserve',
    );
  });

  describe('laka bučica, gde korak ne staje u propis', () => {
    // 8 kg, korak 2 kg (+25%), 8-12 @RIR1. Server daje korak tek kad kapacitet serije -
    // ponavljanja plus rezerva - upija korak: 17.5 efektivnih ponavljanja.
    const lightDumbbell = { repRangeMin: 8, repRangeMax: 12, targetRir: 1, weightStepKg: 2, bodyweightLoadKg: 0 };
    const at = (reps: number, rir: number, isFailure = false, weightKg = 8) => ({ weightKg, reps, rir, isFailure });

    it('ne obećava korak na vrhu opsega', () => {
      expect(setFeedback(lightDumbbell, at(12, 1))).toBe('at-top-step-not-earned');
      expect(setFeedback(lightDumbbell, at(12, 0, true))).toBe('at-top-step-not-earned');
    });

    it('otkaz na produženom cilju ne upija korak, pa ga i ne najavljuje', () => {
      // 17 do otkaza je kapacitet 17 naspram potrebnih 17.5 - server drži težinu.
      expect(setFeedback(lightDumbbell, at(17, 0, true))).toBe('at-top-step-not-earned');
      // 17 uz RIR 1 (ili 15 uz RIR 3) upija: korak dolazi, nema šta da se upozori.
      expect(setFeedback(lightDumbbell, at(17, 1))).toBeNull();
      expect(setFeedback(lightDumbbell, at(15, 3))).toBeNull();
      expect(setFeedback(lightDumbbell, at(18, 0, true))).toBe('failure-at-top');
    });

    it('ne obećava ni zadržavanje kad rezerva iznad cilja može da podigne težinu', () => {
      // 12 uz RIR 4 na 8 kg ne upija korak, ali korekcija naviše i dalje sme da prođe.
      expect(setFeedback(lightDumbbell, at(12, 4))).toBeNull();
    });

    it('računa iz težine koja se unosi, ne iz predloga', () => {
      // Na 20 kg korak od 2 kg (10%) staje u propis: obične napomene.
      expect(setFeedback(lightDumbbell, at(12, 0, true, 20))).toBe('failure-at-top');
      expect(setFeedback(lightDumbbell, at(12, 1, false, 20))).toBeNull();
    });

    it('ispod vrha opsega i u deload nedelji važe obične napomene', () => {
      expect(setFeedback(lightDumbbell, at(10, 0, true))).toBe('failure-in-range');
      expect(setFeedback(lightDumbbell, at(12, 1), true)).toBeNull();
    });
  });

  it('uzak i fiksan propis se ne produžavaju ni na laganoj šipci - korak nosi rezerva', () => {
    // 5x5 @RIR2 na 20 kg, korak 2 kg: server težinu drži posle 7 do otkaza (kapacitet ne
    // upija korak od 10%), pa napomena o zadržavanju mora da ostane tačna.
    const lightFixed = { ...fixedReps, weightStepKg: 2 };
    expect(setFeedback(lightFixed, { weightKg: 20, reps: 7, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
    expect(setFeedback(lightFixed, { weightKg: 20, reps: 5, rir: 2, isFailure: false })).toBe(
      'at-top-narrow',
    );
  });

  it('prepoznaje otkaz ispod i unutar opsega', () => {
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 6, rir: 0, isFailure: true })).toBe('failure-below-range');
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 10, rir: 0, isFailure: true })).toBe('failure-in-range');
  });

  it('seriju ispod dna sa rezervom čita kao težu od plana kad kapacitet ne dostiže cilj', () => {
    // 5 + 2 = 7 < 8 + 1
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 5, rir: 2, isFailure: false })).toBe(
      'below-range-with-reserve',
    );
  });

  it('ne upozorava kad je kapacitet ispod dna dovoljan', () => {
    // 6 + 3 = 9 = 8 + 1: tačno propisano opterećenje.
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 6, rir: 3, isFailure: false })).toBeNull();
  });

  it('RIR 0 ispod dna bez kvačice tretira kao otkaz', () => {
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 6, rir: 0, isFailure: false })).toBe(
      'below-range-no-reserve',
    );
  });

  it('ćuti za obične serije unutar opsega', () => {
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 10, rir: 1, isFailure: false })).toBeNull();
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 12, rir: 0, isFailure: false })).toBeNull();
  });

  it('upozorava na vrh uskog opsega i bez kvačice otkaza', () => {
    // 11-12 @RIR2: sa RIR 0 server zadržava težinu (odstupanje -2, širina 1), sa RIR 2
    // dodaje korak. Vrh sam po sebi tu ne znači korak, pa napomena to kaže.
    expect(setFeedback(narrowRange, { weightKg: 100, reps: 12, rir: 0, isFailure: false })).toBe(
      'at-top-narrow',
    );
    expect(setFeedback(narrowRange, { weightKg: 100, reps: 12, rir: 2, isFailure: false })).toBe(
      'at-top-narrow',
    );
    // U običnom opsegu vrh uvek nosi korak, pa nema šta da se kaže.
    expect(setFeedback(hypertrophy, { weightKg: 100, reps: 12, rir: 2, isFailure: false })).toBeNull();
  });

  /**
   * Granica koju napomena ne može da pokrije: odluku donosi prosek cele vežbe, a napomena
   * vidi samo seriju koja se unosi. Zato tekst uz svaki slučaj nosi uslov "ako je cela
   * vežba takva" - u mešovitoj vežbi (jedna slaba serija, dve lake) server predlaže VEĆE
   * opterećenje: 5@RIR2 + 12@RIR4 + 12@RIR4 na 100 kg daje 105 kg, jer je prosečan
   * efektivni RIR 2.33 naspram cilja 1. Prijavila revizija koda.
   */
  it('daje istu napomenu za slabu seriju i kad je ostatak vežbe lak', () => {
    const weakSet = { weightKg: 100, reps: 5, rir: 2, isFailure: false };
    const easySet = { weightKg: 100, reps: 12, rir: 4, isFailure: false };

    expect(setFeedback(hypertrophy, weakSet)).toBe('below-range-with-reserve');
    expect(setFeedback(hypertrophy, easySet)).toBeNull();
  });
});
