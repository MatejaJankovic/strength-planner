export enum Goal {
  Strength = 0,
  Hypertrophy = 1,
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
  days: WorkoutTemplateDayDto[];
}

export interface GenerateMesocycleRequest {
  templateKey: string;
  goal: Goal;
  name: string;
  startDate: string;
}

export interface MesocycleSummaryDto {
  id: string;
  name: string;
  goal: Goal;
  startDate: string;
  durationWeeks: number;
  isActive: boolean;
}

export interface MesocycleDto extends MesocycleSummaryDto {
  weeks: TrainingWeekDto[];
}

export interface TrainingWeekDto {
  id: string;
  weekNumber: number;
  isDeload: boolean;
  sessions: WorkoutSessionDto[];
}

export interface WorkoutSessionDto {
  id: string;
  weekNumber: number;
  isDeload: boolean;
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
  performedAt: string;
}

export interface AddSetLogRequest {
  weightKg: number;
  reps: number;
  rir: number;
}

export interface CompleteSessionResultDto {
  sessionId: string;
  status: string;
  exercises: CompletedExerciseSummaryDto[];
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
