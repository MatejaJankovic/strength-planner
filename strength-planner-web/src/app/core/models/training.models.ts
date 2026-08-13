export enum Goal {
  Strength = 0,
  Hypertrophy = 1,
}

/** Kako se propis menja kroz nedelje bloka. */
export enum PeriodizationModel {
  Flat = 0,
  Linear = 1,
  Inverse = 2,
}

export interface ProfileDto {
  userId: string;
  email: string;
  sex?: string | null;
  age: number;
  bodyweightKg: number;
  experienceLevel: string;
  trainingDaysPerWeek: number;
}

export interface MuscleContributionDto {
  muscleGroup: string;
  contribution: number;
}

export interface ExerciseDto {
  id: string;
  name: string;
  type: string;
  equipment: string;
  isCustom: boolean;
  /** Korak koji se stvarno primenjuje (korisnicki override ili podrazumevani). */
  weightStepKg: number;
  /** Korak izveden iz sprave — vrednost na koju "Vrati podrazumevano" resetuje. */
  defaultWeightStepKg: number;
  isWeightStepOverridden: boolean;
  muscles: MuscleContributionDto[];
}

export interface UpdateWeightStepRequest {
  weightStepKg: number | null;
}

export interface CreateExerciseRequest {
  name: string;
  type: string;
  equipment: string;
  muscles: MuscleContributionDto[];
}

export interface WorkoutTemplateDayDto {
  name: string;
  exercises: string[];
}

export interface WorkoutTemplateDto {
  key: string;
  name: string;
  /** Šablon koji odgovara broju trenažnih dana iz profila. */
  isSuggested: boolean;
  /** Upozorenje o poznatom ograničenju šablona, ako ga ima. */
  note: string | null;
  days: WorkoutTemplateDayDto[];
}

export interface GenerateMesocycleRequest {
  templateKey: string;
  goal: Goal;
  periodizationModel: PeriodizationModel;
  name: string;
  startDate: string;
}

export interface MesocycleSummaryDto {
  id: string;
  name: string;
  goal: Goal;
  startDate: string;
  durationWeeks: number;
  periodizationModel: PeriodizationModel;
  isActive: boolean;
}

export interface MesocycleDto extends MesocycleSummaryDto {
  weeks: TrainingWeekDto[];
}

export interface TrainingWeekDto {
  id: string;
  weekNumber: number;
  isDeload: boolean;
  /** Deload uveden procenom umora, a ne planom. */
  isAutoDeload: boolean;
  /** Ocena umora izracunata iz ove nedelje (0-1); null dok nije zavrsena. */
  fatigueScore?: number | null;
  sessions: WorkoutSessionDto[];
}

export interface WorkoutSessionDto {
  id: string;
  weekNumber: number;
  isDeload: boolean;
  isAutoDeload: boolean;
  dayLabel: string;
  date?: string | null;
  status: 'Planned' | 'InProgress' | 'Completed' | string;
  exercisePlans: ExercisePlanDto[];
}

export interface ExercisePlanDto {
  id: string;
  exerciseId: string;
  exerciseName: string;
  order: number;
  targetSets: number;
  repRangeMin: number;
  repRangeMax: number;
  targetRir: number;
  targetWeightKg?: number | null;
  weightStepKg: number;
  setLogs: SetLogDto[];
}

export interface SetLogDto {
  id: string;
  exercisePlanId: string;
  setNumber: number;
  weightKg: number;
  reps: number;
  rir: number;
  /** Serija izvucena do otkaza; RIR je tada uvek 0. */
  isFailure: boolean;
  performedAt: string;
}

export interface AddSetLogRequest {
  weightKg: number;
  reps: number;
  rir: number;
  isFailure: boolean;
}

export interface CompleteSessionResultDto {
  sessionId: string;
  status: string;
  exercises: CompletedExerciseSummaryDto[];
  /** Popunjeno samo kada je ovaj trening zatvorio nedelju i pokrenuo deload. */
  autoDeload?: AutoDeloadDto | null;
  /** Popunjeno kada je ovaj trening zatvorio blok i otvorio sledeci iz plana. */
  nextBlock?: MacrocycleAdvanceDto | null;
}

export interface MacrocycleAdvanceDto {
  planName: string;
  blockOrder: number;
  blockCount: number;
  /** Cilj novog bloka ("Strength" ili "Hypertrophy"). */
  goal: string;
  mesocycleId: string;
  mesocycleName: string;
}

export interface AutoDeloadDto {
  triggeredByWeek: number;
  deloadWeek: number;
  /** Ocena umora, 0 (odmoran) do 1 (svi signali na maksimumu). */
  fatigueScore: number;
  /** Nedelja u kojoj je planirani deload otpao, jer mezociklus nosi samo jedan. */
  plannedDeloadReleasedWeek?: number | null;
}

export interface CompletedExerciseSummaryDto {
  exercisePlanId: string;
  exerciseId: string;
  exerciseName: string;
  e1Rm?: number | null;
  isPr: boolean;
  nextWeightKg?: number | null;
  weightIncreased: boolean;
}

export interface MacrocycleBlockDto {
  id: string;
  order: number;
  goal: Goal;
  periodizationModel: PeriodizationModel;
  /** Koliko nedelja blok traje — zavisi od modela. */
  durationWeeks: number;
  templateKey: string;
  /** Naziv sablona za prikaz ("Push/Pull/Legs"), a ne kljuc. */
  templateName: string;
  /** Mezociklus generisan za ovaj blok; null dok blok nije dosao na red. */
  mesocycleId?: string | null;
  status: 'planned' | 'active' | 'completed' | string;
  completedSessions: number;
  totalSessions: number;
}

export interface MacrocycleDto {
  id: string;
  name: string;
  startDate: string;
  isActive: boolean;
  blocks: MacrocycleBlockDto[];
}

export interface CreateMacrocycleBlockDto {
  goal: Goal;
  templateKey: string;
  periodizationModel: PeriodizationModel;
}

export interface CreateMacrocycleRequest {
  name: string;
  startDate: string;
  blocks: CreateMacrocycleBlockDto[];
}
