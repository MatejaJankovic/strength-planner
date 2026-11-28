/** Deo politike privatnosti koji zavisi od servera na kom aplikacija radi. */
export interface PrivacyNoticeDto {
  /** Datum od kog važi tekst, `GGGG-MM-DD`. */
  version: string;
  /** Null kada server nema podešeno ko je rukovalac. */
  controllerName: string | null;
  contactEmail: string | null;
}
