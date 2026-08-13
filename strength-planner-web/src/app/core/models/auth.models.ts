/** Mora da prati PasswordPolicy.MinimumLength na serveru. */
export const PASSWORD_MIN_LENGTH = 10;

export interface RegisterDto {
  email: string;
  password: string;
  sex?: string | null;
  age: number;
  bodyweightKg: number;
  experienceLevel: ExperienceLevel;
  trainingDaysPerWeek: number;
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface AuthResponseDto {
  userId: string;
  email: string;
  token: string;
  expiresAt: string;
}

export interface ChangePasswordDto {
  currentPassword: string;
  newPassword: string;
}

export interface CurrentUserDto {
  id: string;
  email: string;
  sex?: string | null;
  age?: number | null;
  bodyweightKg?: number | null;
  experienceLevel?: string | null;
  trainingDaysPerWeek?: number | null;
}

export enum ExperienceLevel {
  Beginner = 0,
  Intermediate = 1,
  Advanced = 2,
}

export interface UpdateProfileDto {
  sex?: string | null;
  age: number;
  bodyweightKg: number;
  experienceLevel: ExperienceLevel;
  trainingDaysPerWeek: number;
}
