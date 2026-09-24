import { ExercisePlanDto } from '../../core/models/training.models';

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
  | 'below-range-no-reserve'
  | 'below-range-with-reserve';

export interface SetFeedbackDraft {
  reps: number;
  rir: number;
  isFailure: boolean;
}

export type SetFeedbackPlan = Pick<ExercisePlanDto, 'repRangeMin' | 'repRangeMax' | 'targetRir'>;

export function setFeedback(plan: SetFeedbackPlan, draft: SetFeedbackDraft): SetFeedback | null {
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
 * Opseg uži od ciljnog RIR-a (3-4 @RIR3, fiksnih 5 ponavljanja @RIR2): povratak na dno
 * opsega ne pokriva manjak RIR-a na vrhu, pa server tu težinu zadržava umesto da doda
 * korak.
 *
 * Širina nula je krajnji slučaj istog pravila: lični šablon sme da propiše tačan broj
 * ponavljanja (5×5), i tada ceo teret pada na sam RIR.
 */
function isNarrowRange(plan: SetFeedbackPlan): boolean {
  return plan.targetRir > plan.repRangeMax - plan.repRangeMin;
}
