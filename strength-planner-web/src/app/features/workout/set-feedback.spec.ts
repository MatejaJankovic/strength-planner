import { setFeedback } from './set-feedback';

/**
 * Napomena ispod unosa serije je obećanje o sledećem treningu, pa mora da prati pravilo
 * sa servera. Stara napomena je za otkaz na vrhu opsega tvrdila „zadržava se", a server
 * je iznad ~125 kg spuštao težinu - to se na snimku ekrana ne vidi.
 */
describe('setFeedback', () => {
  const hypertrophy = { repRangeMin: 8, repRangeMax: 12, targetRir: 1 };
  // Uzak opseg više ne dolazi iz periodizacije - faza volumena hipertrofije se od
  // ispravke Epley granice propisuje kao 8-12 - nego iz ličnog šablona i iz nedelje
  // intenziteta snage (3-4 @RIR3).
  const narrowRange = { repRangeMin: 11, repRangeMax: 12, targetRir: 2 };
  const fixedReps = { repRangeMin: 5, repRangeMax: 5, targetRir: 2 };

  it('najavljuje korak za otkaz na vrhu običnog opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 12, rir: 0, isFailure: true })).toBe('failure-at-top');
  });

  it('najavljuje zadržavanje za otkaz na vrhu uskog opsega sa većim ciljnim RIR-om', () => {
    expect(setFeedback(narrowRange, { reps: 12, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
  });

  it('fiksan broj ponavljanja tretira kao krajnji slučaj uskog opsega', () => {
    // 5x5 @RIR2: širina nula, pa korak nosi samo rezerva. Otkaz na petom ponavljanju je
    // teže od plana i težina se zadržava.
    expect(setFeedback(fixedReps, { reps: 5, rir: 2, isFailure: false })).toBe('at-top-narrow');
    expect(setFeedback(fixedReps, { reps: 5, rir: 0, isFailure: true })).toBe(
      'failure-at-top-narrow',
    );
    // Ispod propisanog broja se i dalje meri kapacitet: 4 + 2 = 6 naspram 5 + 2.
    expect(setFeedback(fixedReps, { reps: 4, rir: 2, isFailure: false })).toBe(
      'below-range-with-reserve',
    );
  });

  it('ne obećava korak kad je korak prevelik da ga opseg upije', () => {
    // Bučica od 8 kg, korak 2 kg: server težinu drži do 17 ponavljanja. Vrh opsega tada
    // nije vrh koji donosi korak, bez obzira na otkaz.
    const lightDumbbell = { ...hypertrophy, repsToEarnStep: 17 };

    expect(setFeedback(lightDumbbell, { reps: 12, rir: 1, isFailure: false })).toBe(
      'at-top-step-not-earned',
    );
    expect(setFeedback(lightDumbbell, { reps: 12, rir: 0, isFailure: true })).toBe(
      'at-top-step-not-earned',
    );
    expect(setFeedback(lightDumbbell, { reps: 17, rir: 0, isFailure: true })).toBe('failure-at-top');
    // Ispod vrha opsega se ništa ne menja.
    expect(setFeedback(lightDumbbell, { reps: 10, rir: 0, isFailure: true })).toBe('failure-in-range');
  });

  it('bez cilja sa servera važi vrh opsega', () => {
    expect(setFeedback({ ...hypertrophy, repsToEarnStep: 12 }, { reps: 12, rir: 0, isFailure: true })).toBe(
      'failure-at-top',
    );
  });

  it('prepoznaje otkaz ispod i unutar opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 6, rir: 0, isFailure: true })).toBe('failure-below-range');
    expect(setFeedback(hypertrophy, { reps: 10, rir: 0, isFailure: true })).toBe('failure-in-range');
  });

  it('seriju ispod dna sa rezervom čita kao težu od plana kad kapacitet ne dostiže cilj', () => {
    // 5 + 2 = 7 < 8 + 1
    expect(setFeedback(hypertrophy, { reps: 5, rir: 2, isFailure: false })).toBe(
      'below-range-with-reserve',
    );
  });

  it('ne upozorava kad je kapacitet ispod dna dovoljan', () => {
    // 6 + 3 = 9 = 8 + 1: tačno propisano opterećenje.
    expect(setFeedback(hypertrophy, { reps: 6, rir: 3, isFailure: false })).toBeNull();
  });

  it('RIR 0 ispod dna bez kvačice tretira kao otkaz', () => {
    expect(setFeedback(hypertrophy, { reps: 6, rir: 0, isFailure: false })).toBe(
      'below-range-no-reserve',
    );
  });

  it('ćuti za obične serije unutar opsega', () => {
    expect(setFeedback(hypertrophy, { reps: 10, rir: 1, isFailure: false })).toBeNull();
    expect(setFeedback(hypertrophy, { reps: 12, rir: 0, isFailure: false })).toBeNull();
  });

  it('upozorava na vrh uskog opsega i bez kvačice otkaza', () => {
    // 11-12 @RIR2: sa RIR 0 server zadržava težinu (odstupanje -2, širina 1), sa RIR 2
    // dodaje korak. Vrh sam po sebi tu ne znači korak, pa napomena to kaže.
    expect(setFeedback(narrowRange, { reps: 12, rir: 0, isFailure: false })).toBe(
      'at-top-narrow',
    );
    expect(setFeedback(narrowRange, { reps: 12, rir: 2, isFailure: false })).toBe(
      'at-top-narrow',
    );
    // U običnom opsegu vrh uvek nosi korak, pa nema šta da se kaže.
    expect(setFeedback(hypertrophy, { reps: 12, rir: 2, isFailure: false })).toBeNull();
  });

  /**
   * Granica koju napomena ne može da pokrije: odluku donosi prosek cele vežbe, a napomena
   * vidi samo seriju koja se unosi. Zato tekst uz svaki slučaj nosi uslov "ako je cela
   * vežba takva" - u mešovitoj vežbi (jedna slaba serija, dve lake) server predlaže VEĆE
   * opterećenje: 5@RIR2 + 12@RIR4 + 12@RIR4 na 100 kg daje 105 kg, jer je prosečan
   * efektivni RIR 2.33 naspram cilja 1. Prijavila revizija koda.
   */
  it('daje istu napomenu za slabu seriju i kad je ostatak vežbe lak', () => {
    const weakSet = { reps: 5, rir: 2, isFailure: false };
    const easySet = { reps: 12, rir: 4, isFailure: false };

    expect(setFeedback(hypertrophy, weakSet)).toBe('below-range-with-reserve');
    expect(setFeedback(hypertrophy, easySet)).toBeNull();
  });
});
