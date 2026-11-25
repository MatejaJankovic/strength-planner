import { HttpErrorResponse } from '@angular/common/http';

/**
 * Poruka za grešku pri dodavanju ili uklanjanju vežbe.
 *
 * Razlog odbijanja se ne može pročitati iz odgovora: server u produkciji ne šalje tekst izuzetka
 * (`ProblemDetails.Detail` je isti kao `Title`, opšti engleski naslov - vidi `docs/security.md`), a
 * `extractErrorMessage` bi taj naslov prikazao vežbaču. Zato se ovde kaže, na srpskom, šta znači sam
 * status. Razlog koji se zna unapred (blok koji je već imao rasterećenje) stiže kao podatak u odgovoru
 * `addable-exercises`, a ostalo ekran ne nudi.
 */
export function blockEditError(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse && [400, 404, 409].includes(err.status)) {
    // Server ne kaže zašto. Najčešći razlozi su dva i oba se vide tek u drugim nedeljama dana: neki kasniji
    // trening bi ostao bez vežbe (ili je već pun), ili se trening u međuvremenu promenio.
    return 'Izmena nije moguća: neki kasniji trening tog dana to ne dozvoljava (na primer bi ostao bez vežbe), ili se trening u međuvremenu promenio.';
  }

  return fallback;
}
