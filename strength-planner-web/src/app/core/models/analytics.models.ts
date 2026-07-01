export interface VolumeItemDto {
  muscle: string;
  sets: number;
  mev: number;
  mrv: number;
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
