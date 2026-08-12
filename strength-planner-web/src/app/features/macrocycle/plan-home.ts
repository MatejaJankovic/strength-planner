import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MacrocycleService } from '../../core/api/macrocycle.service';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { extractErrorMessage } from '../../core/api/http-error';
import { Goal, CreateMacrocycleBlockDto, MacrocycleBlockDto } from '../../core/models/training.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

const TEMPLATES = [
  { key: 'full-body', name: 'Full Body' },
  { key: 'upper-lower', name: 'Upper/Lower' },
  { key: 'push-pull-legs', name: 'Push/Pull/Legs' },
];

const MIN_BLOCKS = 1;
const MAX_BLOCKS = 6;

@Component({
  selector: 'app-plan-home',
  imports: [DatePipe, MatIconModule, EmptyState, Loading],
  templateUrl: './plan-home.html',
  styleUrl: './plan-home.scss',
})
export class PlanHome {
  private readonly macrocycleService = inject(MacrocycleService);
  private readonly mesocycleService = inject(MesocycleService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notFound = signal(false);

  protected readonly plan = this.macrocycleService.active;

  protected readonly templates = TEMPLATES;
  protected readonly goalOptions = [
    { value: Goal.Hypertrophy, label: 'Hipertrofija' },
    { value: Goal.Strength, label: 'Snaga' },
  ];

  // --- wizard ------------------------------------------------------------------

  protected readonly creating = signal(false);
  protected readonly showWizard = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly planName = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly blocks = signal<CreateMacrocycleBlockDto[]>([]);

  protected readonly canAddBlock = computed(() => this.blocks().length < MAX_BLOCKS);
  protected readonly canRemoveBlock = computed(() => this.blocks().length > MIN_BLOCKS);
  protected readonly canSubmit = computed(
    () => this.planName().trim().length > 0 && !this.creating(),
  );

  /** Ukupno trajanje plana: svaki blok je četvoronedeljni mezociklus. */
  protected readonly totalWeeks = computed(() => this.blocks().length * 4);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.notFound.set(false);

    this.macrocycleService.loadActive().subscribe({
      next: () => this.loading.set(false),
      error: (err: unknown) => {
        this.loading.set(false);

        // 404 nije greška: korisnik jednostavno još nema plan. Keš mora da se isprazni,
        // inače bi na ekranu ostao stari plan a prazno stanje se ne bi ni prikazalo.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.macrocycleService.reset();
          this.notFound.set(true);
          return;
        }

        this.error.set(extractErrorMessage(err, 'Ne mogu da učitam plan. Pokušaj ponovo.'));
      },
    });
  }

  // --- wizard actions -----------------------------------------------------------

  protected openWizard(): void {
    this.showWizard.set(true);
    this.createError.set(null);

    // Početni raspored dolazi sa servera: smenjivanje ciljeva je trenažno pravilo i
    // živi u domenu, pa ga ekran ne izvodi ponovo za sebe.
    this.macrocycleService.suggestedBlocks(2, Goal.Hypertrophy, 'upper-lower').subscribe({
      next: (blocks) => this.blocks.set(blocks),
      error: () =>
        this.blocks.set([
          { goal: Goal.Hypertrophy, templateKey: 'upper-lower' },
          { goal: Goal.Strength, templateKey: 'upper-lower' },
        ]),
    });
  }

  protected closeWizard(): void {
    this.showWizard.set(false);
  }

  protected setPlanName(value: string): void {
    this.planName.set(value);
  }

  protected setStartDate(value: string): void {
    this.startDate.set(value);
  }

  protected addBlock(): void {
    if (!this.canAddBlock()) {
      return;
    }

    // Novi blok podrazumevano smenjuje cilj prethodnog i nasleđuje njegov šablon.
    this.blocks.update((blocks) => {
      const last = blocks[blocks.length - 1];
      const goal = last.goal === Goal.Hypertrophy ? Goal.Strength : Goal.Hypertrophy;

      return [...blocks, { goal, templateKey: last.templateKey }];
    });
  }

  protected removeBlock(index: number): void {
    if (!this.canRemoveBlock()) {
      return;
    }

    this.blocks.update((blocks) => blocks.filter((_, i) => i !== index));
  }

  protected setBlockGoal(index: number, raw: string): void {
    const goal = Number(raw) as Goal;
    this.blocks.update((blocks) =>
      blocks.map((block, i) => (i === index ? { ...block, goal } : block)),
    );
  }

  protected setBlockTemplate(index: number, templateKey: string): void {
    this.blocks.update((blocks) =>
      blocks.map((block, i) => (i === index ? { ...block, templateKey } : block)),
    );
  }

  protected create(): void {
    if (!this.canSubmit()) {
      return;
    }

    this.creating.set(true);
    this.createError.set(null);

    this.macrocycleService
      .create({
        name: this.planName().trim(),
        startDate: this.startDate(),
        blocks: this.blocks(),
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.showWizard.set(false);
          this.notFound.set(false);
          // Novi plan uvek gasi stari mezociklus i pravi novi, pa keširani trening
          // više ne važi.
          this.mesocycleService.reset();
        },
        error: (err: unknown) => {
          this.creating.set(false);
          this.createError.set(
            extractErrorMessage(err, 'Plan nije napravljen. Proveri podatke i pokušaj ponovo.'),
          );
        },
      });
  }

  // --- display helpers ----------------------------------------------------------

  protected goalLabel(goal: Goal): string {
    return goal === Goal.Strength ? 'Snaga' : 'Hipertrofija';
  }

  /**
   * Srpski ima tri oblika množine: 1 blok, 2–4 bloka, 5+ blokova. Brojevi 11–14 idu
   * u poslednji oblik bez obzira na poslednju cifru.
   */
  protected plural(count: number, one: string, few: string, many: string): string {
    const lastTwo = Math.abs(count) % 100;
    const last = lastTwo % 10;

    if (lastTwo >= 11 && lastTwo <= 14) {
      return many;
    }

    if (last === 1) {
      return one;
    }

    return last >= 2 && last <= 4 ? few : many;
  }

  protected blockLabel(count: number): string {
    return this.plural(count, 'blok', 'bloka', 'blokova');
  }

  protected weekLabel(count: number): string {
    return this.plural(count, 'nedelja', 'nedelje', 'nedelja');
  }

  protected statusLabel(block: MacrocycleBlockDto): string {
    switch (block.status) {
      case 'completed':
        return 'Završen';
      case 'active':
        return 'U toku';
      default:
        return 'Na čekanju';
    }
  }

  protected progressPct(block: MacrocycleBlockDto): number {
    if (block.totalSessions === 0) {
      return 0;
    }

    return Math.round((block.completedSessions / block.totalSessions) * 100);
  }

  protected openWorkout(): void {
    void this.router.navigateByUrl('/workout');
  }
}
