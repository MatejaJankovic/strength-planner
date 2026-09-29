/**
 * Da li korak tega staje u ono sto serija moze, i koliko ponavljanja treba da bi stao.
 *
 * Preslikava `StepAbsorption` sa servera (Domain/Algorithms/StepAbsorption.cs). Server
 * odlucuje o koraku; ekran prikazuje cilj i napomenu, pa mora da racuna isto - kad se
 * pravilo na serveru menja, menja se i ovde, a tabela u step-absorption.spec.ts je ista
 * kao u StepAbsorptionTests na serveru.
 *
 * Racuna se iz tezine koja se UNOSI, ne iz propisane: server sudi o najtezoj tezini koja je
 * zaista podignuta, pa cilj izveden iz predloga ne bi vazio cim vezbac uzme drugu bucicu.
 */

/** Epley delilac: 1RM = w * (1 + ponavljanja / 30). */
const EPLEY_DIVISOR = 30;

/** Tolerancija za poredjenje u pokretnom zarezu; server poredi decimale tacno. */
const EPSILON = 1e-9;

/**
 * Da li serija od `reps` ponavljanja uz `rir` u rezervi, na `totalKg`, ostavlja dno opsega
 * dostiznim posle jos jednog koraka - makar do otkaza.
 */
export function absorbsStep(
  totalKg: number,
  stepKg: number,
  repRangeMin: number,
  reps: number,
  rir: number,
): boolean {
  if (totalKg <= 0 || stepKg <= 0) {
    return true;
  }

  return (
    (EPLEY_DIVISOR + reps + rir) * totalKg + EPSILON >=
    (totalKg + stepKg) * (EPLEY_DIVISOR + repRangeMin)
  );
}

/** Korak staje u propis: vezbac na vrhu opsega sa ciljnom rezervom bi ga podneo. */
export function stepFitsAtTarget(
  totalKg: number,
  stepKg: number,
  repRangeMin: number,
  repRangeMax: number,
  targetRir: number,
): boolean {
  return absorbsStep(totalKg, stepKg, repRangeMin, repRangeMax, targetRir);
}

/** Opseg uzi od ciljnog RIR-a (3-4 @RIR3, 11-12 @RIR2, fiksnih 5): tamo korak nosi rezerva. */
export function isNarrowRange(repRangeMin: number, repRangeMax: number, targetRir: number): boolean {
  return targetRir > repRangeMax - repRangeMin;
}

/**
 * Ponavljanja ka kojima se radi pre koraka: vrh opsega, osim kad korak ne staje u sirok
 * opseg - tada najmanji broj koji ga uz ciljnu rezervu upija (8 kg, korak 2 kg, 8-12 @RIR1:
 * 17). Uzak ili fiksan propis se ne produzava.
 */
export function repsToEarnStep(
  totalKg: number,
  stepKg: number,
  repRangeMin: number,
  repRangeMax: number,
  targetRir: number,
): number {
  if (
    isNarrowRange(repRangeMin, repRangeMax, targetRir) ||
    stepFitsAtTarget(totalKg, stepKg, repRangeMin, repRangeMax, targetRir)
  ) {
    return repRangeMax;
  }

  let reps = repRangeMax + 1;
  while (!absorbsStep(totalKg, stepKg, repRangeMin, reps, targetRir)) {
    reps++;
  }

  return reps;
}

export interface RepTargetPlan {
  repRangeMin: number;
  repRangeMax: number;
  targetRir: number;
  weightStepKg: number;
  bodyweightLoadKg: number;
}

/**
 * Cilj ponavljanja na kartici. U deload nedelji je to vrh opsega: posle deload-a se
 * nastavlja od tezine zaradjene pre njega, pa produzen cilj tamo ne bi nista doneo - samo
 * bi terao ponavljanja navise u nedelji odmora.
 */
export function repTargetFor(plan: RepTargetPlan, weightKg: number, isDeload: boolean): number {
  if (isDeload) {
    return plan.repRangeMax;
  }

  return repsToEarnStep(
    weightKg + plan.bodyweightLoadKg,
    plan.weightStepKg,
    plan.repRangeMin,
    plan.repRangeMax,
    plan.targetRir,
  );
}
