/** Mora da prati PasswordPolicy.MinimumLength na serveru. */
export const PASSWORD_MIN_LENGTH = 10;

/**
 * Mora da prati PasswordPolicy.MaximumLength na serveru.
 *
 * Bez ove granice ovde korisnik koji nalepi dugu lozinku iz menadžera lozinki prođe svaku
 * proveru u pregledaču pa dobije goli 400 sa servera, bez označenog polja.
 */
export const PASSWORD_MAX_LENGTH = 128;

/** Mora da prati EmailPolicy.MaximumLength na serveru. */
export const EMAIL_MAX_LENGTH = 256;

export interface RegisterDto {
  email: string;
  password: string;
  sex?: string | null;
  age: number;
  bodyweightKg: number;
  experienceLevel: ExperienceLevel;
  trainingDaysPerWeek: number;
  /**
   * Zamka za automate — vidi RegisterDto.Website na serveru. Polje je sakriveno, pa je
   * kod čoveka uvek prazno; popunjeno znači da formular nije popunio čovek.
   */
  website?: string | null;
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
