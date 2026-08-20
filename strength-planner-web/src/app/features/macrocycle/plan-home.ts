import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MacrocycleService } from '../../core/api/macrocycle.service';
import { MesocycleService } from '../../core/api/mesocycle.service';
import { extractErrorMessage } from '../../core/api/http-error';
import {
  Goal,
  CreateMacrocycleBlockDto,
  MacrocycleBlockDto,
  PeriodizationModel,
  SetAllocation,
  TrainingWeekDto,
  WorkoutTemplateDto,
} from '../../core/models/training.models';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

/**
 * Rezerva ako spisak šablona ne stigne: čarobnjak mora da ponudi bar nešto, a
 * `upper-lower` je i podrazumevani izbor za predlog blokova.
 */
const FALLBACK_TEMPLATE_KEY = 'upper-lower';

/** Blok snage gradi intenzitet, blok hipertrofije volumen — model prati cilj. */
function modelForGoal(goal: Goal): PeriodizationModel {
  return goal === Goal.Strength ? PeriodizationModel.Linear : PeriodizationModel.Inverse;
}

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

  protected readonly templates = signal<WorkoutTemplateDto[]>([]);

  // Lični šabloni se u padajućem meniju odvajaju u svoju grupu: kad ih ima, korisnik ih
  // traži po imenu koje je sam dao, a ne među sedam ugrađenih.
  protected readonly customTemplates = computed(() =>
    this.templates().filter((template) => template.isCustom),
  );

  protected readonly builtInTemplates = computed(() =>
    this.templates().filter((template) => !template.isCustom),
  );

  protected readonly goalOptions = [
    { value: Goal.Hypertrophy, label: 'Hipertrofija' },
    { value: Goal.Strength, label: 'Snaga' },
  ];

  /**
   * Raspored nedelja, sa objasnjenjem sta radi unetim brojevima.
   *
   * Objasnjenje nije ukras. Tvoj opseg ponavljanja nije propis za prvu nedelju nego
   * sidro, a nedelje su pomaci od njega: kod linearnog sidro pada na trecu nedelju, prva
   * je faza volumena (+3 ponavljanja), poslednje su faza intenziteta. To je definicija
   * periodizacije, ali nigde nije pisalo — pa je prijavljeno kao greska: "uneo sam 8-12,
   * a prva nedelja kaze 11-12".
   */
  protected readonly modelOptions = [
    {
      value: PeriodizationModel.Flat,
      label: 'Ravan',
      weeks: 4,
      effect: 'Tvoj opseg ponavljanja svake nedelje. Cetvrta je deload.',
    },
    {
      value: PeriodizationModel.Linear,
      label: 'Linearan',
      weeks: 6,
      effect:
        'Krece sa vise ponavljanja nego sto si uneo, pa se spusta ka tezim serijama. Tvoj opseg dolazi u 3. nedelji.',
    },
    {
      value: PeriodizationModel.Inverse,
      label: 'Obrnut',
      weeks: 6,
      effect:
        'Krece sa manje ponavljanja nego sto si uneo, pa raste ka volumenu. Tvoj opseg dolazi u 3. nedelji.',
    },
  ];

  /**
   * Ko odlucuje o broju serija.
   *
   * Balansiranje po ciljnom volumenu je zateceno ponasanje i ostaje podrazumevano, jer
   * ugradjeni sabloni nose raspored vezbi a ne nameru o volumenu. Kod licnog sablona je
   * obrnuto: otkucano "3 serije" je namera, i menjati je tiho je bilo pogresno.
   */
  protected readonly allocationOptions = [
    {
      value: SetAllocation.TargetVolume,
      label: 'Prilagodi ciljnom volumenu',
      effect: 'Broj serija se podesava tako da nedelja pogodi ciljni volumen po misicu.',
    },
    {
      value: SetAllocation.FollowTemplate,
      label: 'Prati moj sablon',
      effect:
        'Ostaje tacno onoliko serija koliko si uneo. Nedeljni volumen moze ostati ispod cilja i sistem ga nece ispravljati.',
    },
  ];

  // --- wizard ------------------------------------------------------------------

  protected readonly creating = signal(false);
  protected readonly showWizard = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly deleting = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  // --- pregled bloka ---------------------------------------------------------------

  protected readonly expandedBlockId = signal<string | null>(null);
  protected readonly previewLoading = signal(false);
  protected readonly previewError = signal<string | null>(null);

  /**
   * Prva nedelja otvorenog bloka. Drži se jedan blok otvoren, pa je jedno mesto dovoljno -
   * keš po bloku bi rastao bez potrebe, a plan ima najviše šest blokova.
   */
  protected readonly previewWeek = signal<TrainingWeekDto | null>(null);

  protected readonly planName = signal('');
  protected readonly startDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly blocks = signal<CreateMacrocycleBlockDto[]>([]);

  protected readonly canAddBlock = computed(() => this.blocks().length < MAX_BLOCKS);
  protected readonly canRemoveBlock = computed(() => this.blocks().length > MIN_BLOCKS);
  protected readonly canSubmit = computed(
    () => this.planName().trim().length > 0 && !this.creating(),
  );

  /** Ukupno trajanje plana. Blok traje 4 ili 6 nedelja, zavisno od modela. */
  protected readonly totalWeeks = computed(() =>
    this.blocks().reduce((weeks, block) => weeks + this.modelWeeks(block.periodizationModel), 0),
  );

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

    // Spisak šablona dolazi sa servera - ekran ga više ne drži zakucanog, pa se novi
    // šabloni pojave i ovde. Prvi u spisku je samo polazna vrednost padajućeg menija;
    // koji šablon blok nosi bira korisnik.
    this.mesocycleService.templates().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.seedBlocks(templates[0]?.key);
      },
      error: () => {
        // Prazan padajući spisak bi zaključao izbor šablona; jedna stavka je dovoljna
        // da čarobnjak ostane upotrebljiv.
        this.templates.set([
          { key: FALLBACK_TEMPLATE_KEY, name: 'Upper/Lower', isCustom: false, note: null, days: [] },
        ]);
        this.seedBlocks(FALLBACK_TEMPLATE_KEY);
      },
    });
  }

  /**
   * Početni raspored blokova dolazi sa servera: smenjivanje ciljeva je trenažno pravilo i
   * živi u domenu, pa ga ekran ne izvodi ponovo za sebe.
   */
  private seedBlocks(templateKey: string | undefined): void {
    const key = templateKey ?? FALLBACK_TEMPLATE_KEY;

    this.macrocycleService.suggestedBlocks(2, Goal.Hypertrophy, key).subscribe({
      next: (blocks) => this.blocks.set(blocks),
      error: () =>
        this.blocks.set([
          {
            goal: Goal.Hypertrophy,
            templateKey: key,
            periodizationModel: PeriodizationModel.Inverse,
            setAllocation: SetAllocation.TargetVolume,
          },
          {
            goal: Goal.Strength,
            templateKey: key,
            periodizationModel: PeriodizationModel.Linear,
            setAllocation: SetAllocation.TargetVolume,
          },
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

      return [
        ...blocks,
        {
          goal,
          templateKey: last.templateKey,
          periodizationModel: modelForGoal(goal),
          setAllocation: last.setAllocation,
        },
      ];
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
      // Model prati cilj, isto kao pri dodavanju bloka i u predlogu sa servera; korisnik
      // ga i dalje može promeniti posle.
      blocks.map((block, i) =>
        i === index ? { ...block, goal, periodizationModel: modelForGoal(goal) } : block,
      ),
    );
  }

  protected setBlockTemplate(index: number, templateKey: string): void {
    this.blocks.update((blocks) =>
      blocks.map((block, i) => (i === index ? { ...block, templateKey } : block)),
    );
  }

  protected setBlockModel(index: number, raw: string): void {
    const periodizationModel = Number(raw) as PeriodizationModel;
    this.blocks.update((blocks) =>
      blocks.map((block, i) => (i === index ? { ...block, periodizationModel } : block)),
    );
  }

  /** Koliko nedelja nosi blok sa datim modelom. */
  protected setBlockAllocation(index: number, raw: string): void {
    const setAllocation = Number(raw) as SetAllocation;
    this.blocks.update((blocks) =>
      blocks.map((block, i) => (i === index ? { ...block, setAllocation } : block)),
    );
  }

  protected modelEffect(model: PeriodizationModel): string {
    return this.modelOptions.find((option) => option.value === model)?.effect ?? '';
  }

  protected allocationEffect(allocation: SetAllocation): string {
    return this.allocationOptions.find((option) => option.value === allocation)?.effect ?? '';
  }

  protected modelWeeks(model: PeriodizationModel): number {
    return this.modelOptions.find((option) => option.value === model)?.weeks ?? 4;
  }

  protected modelLabel(model: PeriodizationModel): string {
    return this.modelOptions.find((option) => option.value === model)?.label ?? 'Ravan';
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

  /** Ukupno trajanje postojećeg plana; blokovi mogu biti različite dužine. */
  protected planWeeks(plan: { blocks: MacrocycleBlockDto[] }): number {
    return plan.blocks.reduce((weeks, block) => weeks + block.durationWeeks, 0);
  }

  protected goalLabel(goal: Goal): string {
    return goal === Goal.Strength ? 'Snaga' : 'Hipertrofija';
  }

  /**
   * Upozorenje izabranog šablona. Padajući spisak prikazuje samo naziv, pa bi se bez ovoga
   * dugoročan plan mogao sastaviti od šablona čije ograničenje korisnik nikad ne vidi.
   */
  protected templateNote(templateKey: string): string | null {
    return this.templates().find((template) => template.key === templateKey)?.note ?? null;
  }

  /**
   * Dani i vežbe izabranog šablona. Rezervni šablon nema dane, pa se prazan spisak vraća
   * kao null da se ne bi prikazao prazan okvir.
   */
  protected templateDays(templateKey: string): WorkoutTemplateDto['days'] | null {
    const days = this.templates().find((template) => template.key === templateKey)?.days;

    return days && days.length > 0 ? days : null;
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

  /**
   * Otvara i zatvara pregled bloka.
   *
   * Blok koji je generisan ima svoj mezociklus, pa se pokazuje šta u njemu zaista stoji.
   * Blok koji čeka red ga nema - njegov trening nastaje tek kad mu dođe red, od tadašnjih
   * 1RM vrednosti - pa se pokazuje šablon iz kog će nastati. To su dve različite stvari i
   * ekran ih ne sme prikazati kao istu.
   */
  protected toggleBlock(block: MacrocycleBlockDto): void {
    if (this.expandedBlockId() === block.id) {
      this.expandedBlockId.set(null);
      return;
    }

    this.expandedBlockId.set(block.id);
    this.previewError.set(null);
    this.previewWeek.set(null);

    if (block.mesocycleId) {
      this.loadPreviewWeek(block.mesocycleId);
      return;
    }

    // Spisak šablona treba i pregledu, ne samo čarobnjaku; učitava se jednom.
    if (this.templates().length === 0) {
      this.loadTemplates();
    }
  }

  private loadPreviewWeek(mesocycleId: string): void {
    this.previewLoading.set(true);

    this.mesocycleService.byId(mesocycleId).subscribe({
      next: (mesocycle) => {
        this.previewLoading.set(false);
        this.previewWeek.set(
          [...mesocycle.weeks].sort((left, right) => left.weekNumber - right.weekNumber)[0] ?? null,
        );
      },
      error: (err: unknown) => {
        this.previewLoading.set(false);
        this.previewError.set(
          extractErrorMessage(err, 'Ne mogu da učitam pregled bloka. Pokušaj ponovo.'),
        );
      },
    });
  }

  private loadTemplates(): void {
    this.previewLoading.set(true);

    this.mesocycleService.templates().subscribe({
      next: (templates) => {
        this.previewLoading.set(false);
        this.templates.set(templates);
      },
      error: (err: unknown) => {
        this.previewLoading.set(false);
        this.previewError.set(
          extractErrorMessage(err, 'Ne mogu da učitam šablon bloka. Pokušaj ponovo.'),
        );
      },
    });
  }

  protected openWorkout(): void {
    void this.router.navigateByUrl('/workout');
  }

  // --- brisanje plana -------------------------------------------------------------

  protected requestDelete(): void {
    this.deleteError.set(null);
    this.confirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  /**
   * Briše ceo plan, sa mezociklusima svih blokova. Brisanje pojedinačnog mezociklusa više
   * ne postoji: blok bez svog treninga je stanje koje plan sam popravlja tako što ga
   * ponovo generiše, pa je ranije obrisan mezociklus umeo da se vrati.
   */
  protected confirmDelete(): void {
    const current = this.plan();
    if (!current || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.macrocycleService.delete(current.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        this.notFound.set(true);
        // Trening je nestao zajedno sa planom; keširani mezociklus više ne postoji.
        this.mesocycleService.reset();
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.confirmingDelete.set(false);
        this.deleteError.set(
          extractErrorMessage(err, 'Brisanje nije uspelo. Pokušaj ponovo.'),
        );
      },
    });
  }
}
