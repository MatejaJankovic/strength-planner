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

/** Mora da prati ProfilePolicy.DisplayNameMaximumLength na serveru. */
export const DISPLAY_NAME_MAX_LENGTH = 64;

/** Mora da prati ProfilePolicy.MinimumHeightCm na serveru. */
export const HEIGHT_MIN_CM = 100;

/** Mora da prati ProfilePolicy.MaximumHeightCm na serveru. */
export const HEIGHT_MAX_CM = 250;

/**
 * Ukupan broj koraka registracije: sedam pitanja u čarobnjaku plus unos maksimuma.
 *
 * Stoji ovde zato što ga čitaju dva ekrana na dve rute — `/register` i `/onboarding` —
 * a traka napretka mora da pokazuje isti ukupan broj na oba. Da svaki ekran drži svoj,
 * poslednji korak bi pisao „8 od 8" ili „1 od 1" u zavisnosti od toga koji je zaboravljen.
 */
export const REGISTRATION_STEP_COUNT = 8;

/** Mora da prati ImageFormat.MaximumSizeBytes na serveru. */
export const AVATAR_MAX_BYTES = 2 * 1024 * 1024;

/**
 * Tipovi koje server prihvata. Stoji i u `accept` atributu birača fajla, ali to je samo
 * predlog pregledaču — pravu odluku donosi server iz bajtova, ne iz ovog spiska.
 */
export const AVATAR_ACCEPTED_TYPES = 'image/jpeg,image/png,image/webp';

/**
 * Reč koju korisnik mora da otkuca da bi nalog bio obrisan.
 *
 * Mora da prati AccountDeletionPolicy.ConfirmationWord na serveru. Kada se raziđu, ekran
 * traži jedno a server drugo i brisanje prestaje da radi bez poruke o tome zašto.
 */
export const ACCOUNT_DELETION_WORD = 'OBRIŠI';

export interface RegisterDto {
  email: string;
  password: string;
  /** Ime koje stoji na profilu. Registracija ga traži; izmena profila ne. */
  displayName: string;
  sex?: Sex | null;
  age: number;
  bodyweightKg: number;
  heightCm?: number | null;
  experienceLevel: ExperienceLevel;
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
  /** Null za naloge napravljene pre uvođenja imena — ekrani padaju nazad na email. */
  displayName?: string | null;
  sex?: Sex | string | null;
  age?: number | null;
  bodyweightKg?: number | null;
  heightCm?: number | null;
  experienceLevel?: string | null;
  /** Da li nalog ima sliku profila; same bajtove treba tražiti sa GET /auth/avatar. */
  hasAvatar?: boolean;
}

/**
 * Naslov profila: ime ako ga nalog ima, inače email.
 *
 * Stoji ovde, a ne u komponenti, jer ga čitaju i pregled profila i ekran za izmenu. Kada
 * je bio prepisan u oba, pravilo „prazno ili samo razmak pada na email" postojalo je
 * dvaput, a test ga je pokrivao na jednom mestu.
 */
export function profileTitle(user: CurrentUserDto | null): string {
  return user?.displayName?.trim() || user?.email || '';
}

/** Slovo u krugu kada slike nema — prvo slovo naslova, veliko. */
export function profileInitial(user: CurrentUserDto | null): string {
  return profileTitle(user).charAt(0).toLocaleUpperCase('sr');
}

export enum ExperienceLevel {
  Beginner = 0,
  Intermediate = 1,
  Advanced = 2,
}

/**
 * Mora da prati Domain.Enums.Sex na serveru.
 *
 * Bio je slobodan string na obe strane, pa su se registracija ("male"/"female") i profil
 * ("M"/"F") razišli i izabrani pol se na profilu nije prikazivao.
 */
export enum Sex {
  Male = 0,
  Female = 1,
}

/**
 * Jedini spisak ponuđenih polova. Registracija i profil su svaki imali svoj, pa su se
 * razišli; dok ga oba ekrana čitaju odavde, to više ne može da se ponovi.
 */
export const SEX_OPTIONS: ReadonlyArray<{ value: Sex; label: string }> = [
  { value: Sex.Male, label: 'Muški' },
  { value: Sex.Female, label: 'Ženski' },
];

export interface DeleteAccountDto {
  currentPassword: string;
  confirmation: string;
}

export interface UpdateProfileDto {
  displayName?: string | null;
  sex?: Sex | null;
  age: number;
  bodyweightKg: number;
  heightCm?: number | null;
  experienceLevel: ExperienceLevel;
}
