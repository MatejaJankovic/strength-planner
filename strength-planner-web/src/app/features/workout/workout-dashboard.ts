import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { OneRepMaxService } from '../../core/api/one-rep-max.service';
import { Goal, WorkoutSessionDto } from '../../core/models/training.models';
import { StatChip, StatChipTone } from '../../shared/components/stat-chip/stat-chip';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

interface StatusMeta {
  label: string;
  tone: StatChipTone;
}

/** Koliko imena vežbi bez maksimuma se ispisuje pre nego što spisak pređe u broj. */
const MISSING_NAMES_SHOWN = 3;

@Component({
  selector: 'app-workout-dashboard',
  imports: [RouterLink, DatePipe, DecimalPipe, MatIconModule, StatChip, EmptyState, Loading],
  templateUrl: './workout-dashboard.html',
  styleUrl: './workout-dashboard.scss',
})
export class WorkoutDashboard {
  private readonly mesocycleService = inject(MesocycleService);
  private readonly oneRepMaxService = inject(OneRepMaxService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly mesocycle = this.mesocycleService.active;

  /**
   * Vežbe iz ovog bloka za koje ne postoji nijedan zapis maksimuma.
   *
   * Rad (slučaj korišćenja 4) kaže da sistem traži dopunu unosa kada za neku vežbu nema
   * procene 1RM. Do sada to nije tražio nigde — svaka takva vežba je tiho čekala da je
   * vežbač prvi put unese „po osećaju". To je legitiman put, ali je bio i jedini, i to
   * neizrečen.
   *
   * Pita se **spisak maksimuma**, a ne upisan cilj u planu. Cilj je prazan i kad maksimum
   * postoji ali ga blok još nije pokupio (npr. unet je posle generisanja), pa bi po njemu
   * ovde stajale i vežbe koje odgovor imaju. Prva verzija ovog spiska je baš tako izlistala
   * osamnaest vežbi, među njima i dve sa upisanim maksimumom.
   *
   * Vežbe koje diže sopstvena masa se ne broje — njima je nula dodatih tačan prvi propis,
   * a ne nepoznanica.
   */
  protected readonly exercisesWithoutMax = computed<string[]>(() => {
    const plan = this.mesocycle();
    if (!plan) {
      return [];
    }

    const known = new Set(this.oneRepMaxService.oneRepMaxes().map((item) => item.exerciseId));
    const names = new Map<string, string>();

    for (const week of plan.weeks) {
      for (const session of week.sessions) {
        for (const item of session.exercisePlans) {
          if (!item.isBodyweight && !known.has(item.exerciseId)) {
            names.set(item.exerciseId, item.exerciseName);
          }
        }
      }
    }

    return [...names.values()].sort((a, b) => a.localeCompare(b));
  });

  /**
   * Spisak se skraćuje. Nalog bez ijednog unetog maksimuma bi inače nabrojao ceo blok, a
   * zid od osamnaest imena ne govori više od broja.
   */
  protected readonly missingMaxSummary = computed<string>(() => {
    const names = this.exercisesWithoutMax();
    if (names.length <= MISSING_NAMES_SHOWN) {
      return names.join(', ');
    }

    return `${names.slice(0, MISSING_NAMES_SHOWN).join(', ')} i još ${names.length - MISSING_NAMES_SHOWN}`;
  });

  protected openOneRepMaxSetup(): void {
    void this.router.navigateByUrl('/onboarding');
  }

  // First not-yet-finished session across the whole plan (weeks then sessions in order).
  protected readonly nextSessionId = computed<string | null>(() => {
    const plan = this.mesocycle();
    if (!plan) {
      return null;
    }
    for (const week of plan.weeks) {
      for (const session of week.sessions) {
        if (session.status === 'Planned' || session.status === 'InProgress') {
          return session.id;
        }
      }
    }
    return null;
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    // Maksimumi se učitavaju uz plan: bez njih se ne zna za koju vežbu odgovor postoji, a
    // neuspeh tog čitanja ne sme da obori ekran — tada spisak ostaje prazan, što je tiše
    // nego lažna tvrdnja da maksimuma nema.
    this.oneRepMaxService.load().subscribe({ error: () => undefined });

    this.mesocycleService.loadActive().subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);
        // 404 simply means "no active plan yet" — show the empty state, not an error.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          return;
        }
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam plan. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  protected goalLabel(goal: Goal): string {
    return goal === Goal.Strength ? 'Snaga' : 'Hipertrofija';
  }

  protected statusMeta(status: WorkoutSessionDto['status']): StatusMeta {
    switch (status) {
      case 'Completed':
        return { label: 'Završeno', tone: 'optimal' };
      case 'InProgress':
        return { label: 'U toku', tone: 'accent' };
      case 'Skipped':
        return { label: 'Preskočeno', tone: 'below' };
      default:
        return { label: 'Planirano', tone: 'neutral' };
    }
  }

  /**
   * Prazno stanje vodi na dugoročni plan, jedino mesto sa kog trening nastaje.
   * Ekran „Trening" sam ništa ne pravi niti briše: mezociklus je blok plana, pa bi
   * prekid usred plana ostavio blok bez svog treninga.
   */
  protected openPlan(): void {
    void this.router.navigateByUrl('/plan');
  }
}
