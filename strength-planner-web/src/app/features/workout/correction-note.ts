import { SessionCorrectionDto, SessionRevisionResultDto } from '../../core/models/training.models';

/**
 * Šta ekran kaže o ispravci završenog treninga.
 *
 * Tekst zavisi od tri stvari koje server računa i koje se lako pomešaju: da li je ispravka
 * moguća, da li se posle nje išta preračunava (trening završen pre nego što su se rezultati
 * beležili po treningu se ispravlja samo kao istorija), i da li ima izmena koje još nisu
 * sačuvane. Zato su rečenice ovde, uz spec, a ne u šablonu.
 */
export function correctionNote(
  correction: SessionCorrectionDto | null | undefined,
  correcting: boolean,
): string | null {
  if (!correction) {
    return null;
  }

  if (!correction.canCorrect) {
    switch (correction.blockedBy) {
      case 'NextWorkoutStarted':
        return 'Ispravka više nije moguća: sledeći trening ovog dana je već počeo, a njegova težina je izračunata iz ovih serija.';
      case 'NextBlockOpened':
        return 'Ispravka više nije moguća: sledeći blok je generisan iz ovog treninga.';
      default:
        return null;
    }
  }

  if (correction.mode === 'HistoryOnly') {
    return 'Ovaj trening je završen pre nego što su se rezultati beležili po treningu. Serije možeš da ispraviš, ali se menja samo zapis i analitika: težine sledećih treninga se ne preračunavaju.';
  }

  if (correcting) {
    return correction.revisionPending
      ? 'Izmene su upisane, ali još nisu preračunate. Sačuvaj ispravku da se procena maksimuma, težine i ocena nedelje ažuriraju.'
      : 'Ispravljaš završen trening. Izmeni, dodaj ili obriši seriju, pa sačuvaj ispravku.';
  }

  return 'Pogrešno upisana serija? Možeš da je ispraviš dok sledeći trening ovog dana ne počne. Procena maksimuma, težine sledećeg treninga i ocena nedelje se tada računaju iznova.';
}

/** Ispravka koja ima šta da preračuna, a ne samo da prepiše zapis. */
export function recalculates(correction: SessionCorrectionDto | null | undefined): boolean {
  return correction?.canCorrect === true && correction.mode === 'Recalculate';
}

/**
 * Šta je sačuvana ispravka promenila a rezime ne kaže: ocena umora nedelje i deload koji
 * ostaje. Rezime sam pokazuje procenu, težine i pomerene serije.
 */
export function revisionNotes(
  result: Pick<SessionRevisionResultDto, 'previousFatigueScore' | 'fatigueScore' | 'deloadKept'> | null,
  weekNumber: number,
): string[] {
  if (!result) {
    return [];
  }

  const notes: string[] = [];
  const { previousFatigueScore: before, fatigueScore: after } = result;

  if (before != null && after != null && Math.abs(before - after) >= 0.005) {
    notes.push(`Ocena umora nedelje ${weekNumber}: ${before.toFixed(2)} → ${after.toFixed(2)}.`);
  }

  if (result.deloadKept) {
    notes.push(
      'Deload koji je ranije uveden ostaje, iako ispravljene serije ne bi dale toliki umor. Sledeća nedelja je već prepisana za njega i ne vraća se.',
    );
  }

  return notes;
}
