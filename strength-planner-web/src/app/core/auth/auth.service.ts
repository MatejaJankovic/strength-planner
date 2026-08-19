import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-base';
import { ExerciseService } from '../api/exercise.service';
import { MacrocycleService } from '../api/macrocycle.service';
import { MesocycleService } from '../api/mesocycle.service';
import { OneRepMaxService } from '../api/one-rep-max.service';
import {
  AuthResponseDto,
  ChangePasswordDto,
  CurrentUserDto,
  LoginDto,
  RegisterDto,
  UpdateProfileDto,
} from '../models/auth.models';
import { AuthTokenStorage } from './auth-token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly tokenStorage = inject(AuthTokenStorage);
  private readonly router = inject(Router);
  private readonly exerciseService = inject(ExerciseService);
  private readonly mesocycleService = inject(MesocycleService);
  private readonly macrocycleService = inject(MacrocycleService);
  private readonly oneRepMaxService = inject(OneRepMaxService);

  readonly token = this.tokenStorage.token;

  private readonly currentUserSignal = signal<CurrentUserDto | null>(null);
  readonly currentUser = this.currentUserSignal.asReadonly();

  /**
   * Adresa slike profila kao `blob:` URL, ili null ako je nema.
   *
   * Slika se ne može staviti u `<img src>` direktno: `GET /api/auth/avatar` traži
   * Authorization zaglavlje, a `<img>` ga ne šalje. Zato se dohvata kao blob i pravi se
   * lokalni URL.
   *
   * Taj URL je keš vezan za korisnika i mora da se poništi pri promeni identiteta — vidi
   * `resetUserCaches`. Uz to se `revokeObjectURL` mora pozvati na svakoj zameni, jer
   * pregledač drži blob u memoriji dok URL postoji.
   */
  private readonly avatarUrlSignal = signal<string | null>(null);
  readonly avatarUrl = this.avatarUrlSignal.asReadonly();

  readonly isAuthenticated = computed(() => this.token() !== null);

  login(dto: LoginDto): Observable<AuthResponseDto> {
    return this.http
      .post<AuthResponseDto>(`${this.apiUrl}/auth/login`, dto)
      .pipe(tap((response) => this.handleAuthenticated(response)));
  }

  register(dto: RegisterDto): Observable<AuthResponseDto> {
    return this.http
      .post<AuthResponseDto>(`${this.apiUrl}/auth/register`, dto)
      .pipe(tap((response) => this.handleAuthenticated(response)));
  }

  loadMe(): Observable<CurrentUserDto> {
    return this.http
      .get<CurrentUserDto>(`${this.apiUrl}/auth/me`)
      .pipe(tap((user) => this.currentUserSignal.set(user)));
  }

  updateProfile(dto: UpdateProfileDto): Observable<CurrentUserDto> {
    return this.http
      .put<CurrentUserDto>(`${this.apiUrl}/auth/profile`, dto)
      .pipe(tap((user) => this.currentUserSignal.set(user)));
  }

  /**
   * Menja lozinku. Server pri tome poništava sve ranije izdate tokene, pa u odgovoru
   * stiže nov — bez njega bi korisnik promenom lozinke izbacio sam sebe.
   */
  changePassword(dto: ChangePasswordDto): Observable<AuthResponseDto> {
    return this.http
      .post<AuthResponseDto>(`${this.apiUrl}/auth/change-password`, dto)
      .pipe(tap((response) => this.tokenStorage.setToken(response.token)));
  }

  /** Dohvata sliku profila. 404 nije greška — znači da nalog nema sliku. */
  loadAvatar(): Observable<string | null> {
    return this.http
      .get(`${this.apiUrl}/auth/avatar`, { responseType: 'blob' })
      .pipe(map((blob) => this.setAvatarBlob(blob)));
  }

  /**
   * Otprema sliku profila kao multipart.
   *
   * Namerno ne base64 u JSON-u: base64 uveća sadržaj za trećinu i tera ceo zahtev u
   * memoriju kao string pre nego što se veličina proveri.
   */
  uploadAvatar(file: File): Observable<CurrentUserDto> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.http
      .put<CurrentUserDto>(`${this.apiUrl}/auth/avatar`, body)
      .pipe(tap((user) => this.currentUserSignal.set(user)));
  }

  removeAvatar(): Observable<CurrentUserDto> {
    return this.http.delete<CurrentUserDto>(`${this.apiUrl}/auth/avatar`).pipe(
      tap((user) => {
        this.currentUserSignal.set(user);
        this.clearAvatar();
      }),
    );
  }

  logout(): void {
    this.tokenStorage.clear();
    this.currentUserSignal.set(null);
    this.resetUserCaches();
    void this.router.navigate(['/login']);
  }

  /** Pravi lokalni URL za sliku i pušta prethodni. */
  private setAvatarBlob(blob: Blob): string | null {
    this.clearAvatar();

    if (blob.size === 0) {
      return null;
    }

    const url = URL.createObjectURL(blob);
    this.avatarUrlSignal.set(url);

    return url;
  }

  private clearAvatar(): void {
    const previous = this.avatarUrlSignal();
    if (previous) {
      // Bez ovoga pregledač drži svaku ranije dohvaćenu sliku u memoriji do osvežavanja
      // stranice - a slika je do dva megabajta.
      URL.revokeObjectURL(previous);
    }
    this.avatarUrlSignal.set(null);
  }

  private handleAuthenticated(response: AuthResponseDto): void {
    this.tokenStorage.setToken(response.token);
    this.currentUserSignal.set({ id: response.userId, email: response.email });
    // Novi identitet — keširani podaci prethodnog korisnika ne smeju da procure.
    this.resetUserCaches();
  }

  /**
   * Prazni sve keševe vezane za korisnika.
   *
   * Servisi žive na nivou cele aplikacije, pa promena identiteta bez osvežavanja stranice
   * ostavlja podatke prethodnog korisnika u memoriji. **Svaki servis koji kešira korisničke
   * podatke mora da bude naveden ovde.** `auth.service.spec.ts` proverava ova četiri —
   * peti, dodat kasnije, neće biti pokriven dok se i tamo ne doda.
   */
  private resetUserCaches(): void {
    this.exerciseService.reset();
    this.mesocycleService.reset();
    this.macrocycleService.reset();
    this.oneRepMaxService.reset();
    // Slika prethodnog naloga je isto keširan korisnički podatak: `blob:` URL ostaje
    // upotrebljiv dok se ne poništi, pa bi bez ovoga lice prethodnog korisnika stajalo na
    // profilu narednog do osvežavanja stranice.
    this.clearAvatar();
  }
}
