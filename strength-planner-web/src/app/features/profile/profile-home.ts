import { Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { extractErrorMessage } from '../../core/api/http-error';
import { AuthService } from '../../core/auth/auth.service';
import {
  ExperienceLevel,
  profileInitial,
  profileTitle,
  Sex,
} from '../../core/models/auth.models';
import { Loading } from '../../shared/components/loading/loading';

/**
 * Profil kao pregled i raskrsnica.
 *
 * Ekran je nekada bio spisak formi: osnovni podaci, lozinka, korak opterećenja, šabloni,
 * sopstvene vežbe i odjava, jedno pod drugim. Sada nosi ono što se o vežbaču čita, i
 * odvodi na ekrane koji svaki radi jednu stvar — olovka na izmenu podataka, zupčanik na
 * podešavanja naloga, i mreža dugmadi na statistiku, vežbe i šablone.
 */
@Component({
  selector: 'app-profile-home',
  imports: [MatIconModule, RouterLink, Loading],
  templateUrl: './profile-home.html',
  styleUrl: './profile-home.scss',
})
export class ProfileHome {
  private readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly user = this.auth.currentUser;
  protected readonly avatarUrl = this.auth.avatarUrl;

  protected readonly title = computed(() => profileTitle(this.user()));
  protected readonly initial = computed(() => profileInitial(this.user()));

  /**
   * Dugmad na dashboardu.
   *
   * Tri, kao što je i traženo: statistika, vežbe i šabloni. Stoje kao podaci, a ne kao
   * prepisani blokovi u šablonu, pa dodavanje četvrtog ne dira raspored.
   */
  protected readonly dashboard = [
    { label: 'Statistika', icon: 'monitoring', route: '/analytics' },
    { label: 'Vežbe', icon: 'fitness_center', route: '/exercises' },
    { label: 'Šabloni', icon: 'assignment', route: '/templates' },
  ];

  /** Podaci o vežbaču, onako kako se čitaju - prazna polja se ne prikazuju. */
  protected readonly summary = computed(() => {
    const current = this.user();
    if (!current) {
      return [];
    }

    const rows: { label: string; value: string }[] = [];

    if (current.age != null) {
      rows.push({ label: 'Uzrast', value: `${current.age}` });
    }
    if (current.bodyweightKg != null) {
      rows.push({ label: 'Telesna masa', value: `${current.bodyweightKg} kg` });
    }
    if (current.heightCm != null) {
      rows.push({ label: 'Visina', value: `${current.heightCm} cm` });
    }

    const level = experienceLabel(current.experienceLevel);
    if (level) {
      rows.push({ label: 'Nivo iskustva', value: level });
    }

    const sex = sexLabel(current.sex);
    if (sex) {
      rows.push({ label: 'Pol', value: sex });
    }

    return rows;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      me: this.auth.loadMe(),
      // Nalog bez slike vraća 404; to je odgovor, ne greška, pa ne sme da obori ekran.
      avatar: this.auth.loadAvatar().pipe(catchError(() => of(null))),
    }).subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam profil. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }
}

/**
 * Čitljiv naziv nivoa iskustva. Backend serijalizuje enum kao broj; string imena su
 * fallback, isto kao za pol.
 */
function experienceLabel(level?: string | number | null): string | null {
  switch (level) {
    case ExperienceLevel.Beginner:
    case 'Beginner':
      return 'Početnik';
    case ExperienceLevel.Intermediate:
    case 'Intermediate':
      return 'Srednji nivo';
    case ExperienceLevel.Advanced:
    case 'Advanced':
      return 'Napredni';
    default:
      return null;
  }
}

/** Čitljiv naziv pola, ili null kada nije naveden. */
function sexLabel(sex?: Sex | string | null): string | null {
  switch (sex) {
    case Sex.Male:
    case 'Male':
      return 'Muški';
    case Sex.Female:
    case 'Female':
      return 'Ženski';
    default:
      return null;
  }
}
