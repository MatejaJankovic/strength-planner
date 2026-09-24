export interface VolumeItemDto {
  muscle: string;
  /** Stimulativne serije: doprinos svake serije skaliran blizinom otkaza. */
  sets: number;
  mev: number;
  /** Maksimalni adaptivni volumen — naucena granica, i cilj osnovne nedelje. */
  mav: number;
  mrv: number;
  /**
   * Cilj koji gadja izabrana nedelja: MAV pomeren koliko i propis, i spusten kod bloka
   * snage. Null u deload nedelji i za misic koji ta nedelja ne trenira.
   */
  weekTargetSets: number | null;
  /** True kada je izabrana nedelja rasterecenje. */
  isDeloadWeek: boolean;
  /** Populaciona seed granica — vrednost na koju reset vraca. */
  defaultMev: number;
  defaultMav: number;
  defaultMrv: number;
  /** True kada su granice naucene iz korisnikovog odgovora na volumen. */
  isPersonal: boolean;
  status: 'below' | 'optimal' | 'above';
}

export interface E1rmPointDto {
  valueKg: number;
  recordedAt: string;
}

export interface PersonalRecordDto {
  exerciseId: string;
  exercise: string;
  bestE1Rm?: number | null;
  bestWeight?: number | null;
  achievedAt?: string | null;
  /** Vezba nosi deo telesne mase, pa su oba rekorda UKUPNO opterecenje (telo + dodato). */
  isBodyweight: boolean;
}

export interface OneRepMaxDto {
  id: string;
  exerciseId: string;
  exercise: string;
  valueKg: number;
  source: 'Manual' | 'Estimated' | string;
  recordedAt: string;
}

export interface CreateOneRepMaxRequest {
  exerciseId: string;
  valueKg: number;
}

export interface WeeklyTonnageDto {
  weekNumber: number;
  isDeload: boolean;
  tonnageKg: number;
}
