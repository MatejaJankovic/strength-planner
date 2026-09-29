import { ExercisePlanDto } from '../../core/models/training.models';
import { absorbsStep, isNarrowRange as narrowPrescription, stepAbove, stepFitsAtTarget } from './step-absorption';

/**
 * Šta serija koja se upravo unosi znači za sledeći trening.
 *
 * Pravila prate `ProgressionEngine` na serveru, jer je napomena obećanje: ranije je
 * pisalo „opterećenje se zadržava" za otkaz na vrhu opsega, a server je iznad ~125 kg
 * težinu spuštao. Kad se pravilo na serveru menja, menja se i ovde.
 *
 * Napomena vidi **jednu** seriju, a server odlučuje po proseku cele vežbe, pa tekst uz
 * svaki slučaj nosi uslov („ako je cela vežba takva") umesto da obeća tačan broj.
 */
export type SetFeedback =
  | 'failure-below-range'
  | 'failure-at-top'
  | 'failure-at-top-narrow'
  | 'failure-in-range'
  | 'at-top-narrow'
  | 'at-top-step-not-earned'
  | 'below-range-no-reserve'
  | 'below-range-with-reserve';

export interface SetFeedbackDraft {
  /** Težina koja se unosi (dodata, kod vežbi sa telesnom masom). */
  weightKg: number;
  reps: number;
  rir: number;
  isFailure: boolean;
}

export type SetFeedbackPlan = Pick<
  ExercisePlanDto,
  'repRangeMin' | 'repRangeMax' | 'targetRir' | 'weightStepKg' | 'bodyweightLoadKg'
>;

/**
 * @param isDeload U deload nedelji napomene ćute: posle nje se nastavlja od težine zarađene
 * pre nje, pa ništa što se u njoj upiše ne menja sledeće opterećenje - a svaka napomena
 * ispod je obećanje o sledećem treningu.
 */
export function setFeedback(
  plan: SetFeedbackPlan,
  draft: SetFeedbackDraft,
  isDeload = false,
): SetFeedback | null {
  if (isDeload) {
    return null;
  }

  const stepNote = stepTooLargeFeedback(plan, draft);
  if (stepNote !== undefined) {
    return stepNote;
  }

  if (draft.isFailure) {
    if (draft.reps < plan.repRangeMin) {
      return 'failure-below-range';
    }

    if (draft.reps >= plan.repRangeMax) {
      return isNarrowRange(plan) ? 'failure-at-top-narrow' : 'failure-at-top';
    }

    return 'failure-in-range';
  }

  if (draft.reps >= plan.repRangeMax) {
    // U uskom opsegu vrh sam po sebi ne znači korak: kad rezerve nema, server zadržava
    // težinu. U običnom opsegu vrh uvek nosi korak, pa tu nema šta da se kaže.
    return isNarrowRange(plan) ? 'at-top-narrow' : null;
  }

  if (draft.reps >= plan.repRangeMin) {
    return null;
  }

  // RIR 0 ispod dna opsega je otkaz i bez kvačice - server ga tako i upisuje.
  if (draft.rir === 0) {
    return 'below-range-no-reserve';
  }

  // Ispod dna se meri kapacitet (ponavljanja + RIR) prema dnu opsega i ciljnom RIR-u.
  // 5 sa RIR 2 u 8-12 @RIR1 je kapacitet 7 naspram traženih 9: teže od plana.
  return draft.reps + draft.rir < plan.repRangeMin + plan.targetRir
    ? 'below-range-with-reserve'
    : null;
}

/**
 * Serija na vrhu širokog opsega kod lakog tega, gde korak ne staje u propis (bučica od 8 kg,
 * korak 2 kg = +25%). Server tu daje korak samo kad kapacitet serije - ponavljanja plus
 * rezerva koja je zaista ostala - upija korak; inače težina čeka.
 *
 * Vraća `undefined` kad ovo pravilo ne važi (ispod vrha, uzak opseg, korak staje),
 * pa odlučuju obične napomene. `null` kad pravilo važi, a nema šta da se kaže: kapacitet
 * upija korak bez otkaza, ili ima rezerve iznad cilja pa korekcija naviše i dalje može da
 * podigne težinu - to se ne obećava ni u jednom smeru.
 */
function stepTooLargeFeedback(plan: SetFeedbackPlan, draft: SetFeedbackDraft): SetFeedback | null | undefined {
  if (draft.reps < plan.repRangeMax || isNarrowRange(plan)) {
    return undefined;
  }

  const totalKg = draft.weightKg + plan.bodyweightLoadKg;
  if (stepFitsAtTarget(totalKg, plan.weightStepKg, plan.repRangeMin, plan.repRangeMax, plan.targetRir)) {
    return undefined;
  }

  // Server korača do sledeće težine na mreži, pa se upija baš to povećanje.
  const increaseKg = stepAbove(draft.weightKg, plan.weightStepKg) - draft.weightKg;
  const rir = draft.isFailure ? 0 : draft.rir;
  if (absorbsStep(totalKg, increaseKg, plan.repRangeMin, draft.reps, rir)) {
    return draft.isFailure ? 'failure-at-top' : null;
  }

  return rir <= plan.targetRir ? 'at-top-step-not-earned' : null;
}

/**
 * Opseg uži od ciljnog RIR-a (3-4 @RIR3, fiksnih 5 ponavljanja @RIR2): povratak na dno
 * opsega ne pokriva manjak RIR-a na vrhu, pa server tu težinu zadržava umesto da doda
 * korak.
 *
 * Širina nula je krajnji slučaj istog pravila: lični šablon sme da propiše tačan broj
 * ponavljanja (5×5), i tada ceo teret pada na sam RIR.
 */
function isNarrowRange(plan: SetFeedbackPlan): boolean {
  return narrowPrescription(plan.repRangeMin, plan.repRangeMax, plan.targetRir);
}
