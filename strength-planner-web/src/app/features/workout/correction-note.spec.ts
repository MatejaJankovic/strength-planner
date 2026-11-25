import { SessionCorrectionDto } from '../../core/models/training.models';
import { correctionNote, recalculates, revisionNotes } from './correction-note';

const open: SessionCorrectionDto = {
  canCorrect: true,
  mode: 'Recalculate',
  blockedBy: null,
  revisionPending: false,
};

describe('correctionNote', () => {
  it('trening bez prozora ispravke nema poruku', () => {
    expect(correctionNote(null, false)).toBeNull();
    expect(correctionNote(undefined, false)).toBeNull();
  });

  it('zatvoren prozor kaže zašto, i razlikuje dva uzroka', () => {
    const started = correctionNote({ ...open, canCorrect: false, blockedBy: 'NextWorkoutStarted' }, false);
    const block = correctionNote({ ...open, canCorrect: false, blockedBy: 'NextBlockOpened' }, false);

    expect(started).toContain('sledeći trening ovog dana je već počeo ili je preskočen');
    expect(block).toContain('sledeći blok je generisan');
    expect(started).not.toBe(block);
  });

  it('trening završen pre beleženja po treningu kaže da se ništa ne preračunava', () => {
    const note = correctionNote({ ...open, mode: 'HistoryOnly' }, false);

    expect(note).toContain('ne preračunavaju');
  });

  it('istorija se ne obećava preračun ni u režimu ispravke', () => {
    // U režimu ispravke bi tekst za preračun lagao: server za takav trening ništa ne računa.
    const note = correctionNote({ ...open, mode: 'HistoryOnly' }, true);

    expect(note).not.toContain('Sačuvaj ispravku');
  });

  it('tri stanja otvorene ispravke imaju različite poruke', () => {
    const idle = correctionNote(open, false);
    const correcting = correctionNote(open, true);
    const pending = correctionNote({ ...open, revisionPending: true }, true);

    expect(new Set([idle, correcting, pending]).size).toBe(3);
    expect(pending).toContain('još nisu preračunate');
  });
});

describe('recalculates', () => {
  it('samo otvorena ispravka u režimu preračuna ima šta da sačuva', () => {
    expect(recalculates(open)).toBe(true);
    expect(recalculates({ ...open, mode: 'HistoryOnly' })).toBe(false);
    expect(recalculates({ ...open, canCorrect: false, blockedBy: 'NextBlockOpened' })).toBe(false);
    expect(recalculates(null)).toBe(false);
  });
});

describe('revisionNotes', () => {
  it('bez rezultata nema beleški', () => {
    expect(revisionNotes(null, 2)).toEqual([]);
  });

  it('promenjenu ocenu umora piše sa nedeljom, nepromenjenu ne pominje', () => {
    expect(
      revisionNotes({ previousFatigueScore: 0.1, fatigueScore: 0.45, deloadKept: false }, 2),
    ).toEqual(['Ocena umora nedelje 2: 0.10 → 0.45.']);
    expect(
      revisionNotes({ previousFatigueScore: 0.3, fatigueScore: 0.3, deloadKept: false }, 2),
    ).toEqual([]);
  });

  it('nedelja koja još nije zatvorena nema ocenu, pa nema ni te beleške', () => {
    expect(
      revisionNotes({ previousFatigueScore: null, fatigueScore: null, deloadKept: false }, 1),
    ).toEqual([]);
  });

  it('deload koji ostaje se kaže i kad se ocena nije promenila', () => {
    const notes = revisionNotes({ previousFatigueScore: 0.6, fatigueScore: 0.6, deloadKept: true }, 3);

    expect(notes).toHaveLength(1);
    expect(notes[0]).toContain('Deload koji je ranije uveden ostaje');
  });
});
