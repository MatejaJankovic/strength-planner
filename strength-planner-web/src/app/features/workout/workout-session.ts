import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { extractErrorMessage } from '../../core/api/http-error';
import { SessionService } from '../../core/api/session.service';
import {
  CompleteSessionResultDto,
  ExercisePlanDto,
  SetLogDto,
  WorkoutSessionDto,
} from '../../core/models/training.models';
import { WeightStepper } from '../../shared/components/weight-stepper/weight-stepper';
import { StatChip } from '../../shared/components/stat-chip/stat-chip';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Loading } from '../../shared/components/loading/loading';

interface SetDraft {
  weightKg: number;
  reps: number;
  rir: number;
}

const REP_STEP_MIN = 1;
const REP_STEP_MAX = 100;

@Component({
  selector: 'app-workout-session',
  imports: [DecimalPipe, MatIconModule, WeightStepper, StatChip, EmptyState, Loading],
  templateUrl: './workout-session.html',
  styleUrl: './workout-session.scss',
})
export class WorkoutSession {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sessionService = inject(SessionService);

  private readonly sessionId = this.route.snapshot.paramMap.get('id') ?? '';

  protected readonly rirOptions = [0, 1, 2, 3, 4, 5];

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly starting = signal(false);
  protected readonly completing = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly toast = signal<string | null>(null);

  protected readonly session = signal<WorkoutSessionDto | null>(null);
  protected readonly summary = signal<CompleteSessionResultDto | null>(null);

  private readonly drafts = signal<Record<string, SetDraft>>({});
  private readonly editingSetId = signal<Record<string, string | null>>({});
  protected readonly savingPlanId = signal<string | null>(null);

  protected readonly status = computed(() => this.session()?.status ?? null);
  protected readonly isPlanned = computed(() => this.status() === 'Planned');
  protected readonly isInProgress = computed(() => this.status() === 'InProgress');
  protected readonly isCompleted = computed(() => this.status() === 'Completed');

  protected readonly loggedSetCount = computed(() =>
    (this.session()?.exercisePlans ?? []).reduce((total, plan) => total + plan.setLogs.length, 0),
  );

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.sessionService.getById(this.sessionId).subscribe({
      next: (session) => {
        this.session.set(session);
        this.initDrafts(session);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          extractErrorMessage(err, 'Ne mogu da učitam trening. Proveri vezu i pokušaj ponovo.'),
        );
      },
    });
  }

  private initDrafts(session: WorkoutSessionDto): void {
    const drafts: Record<string, SetDraft> = {};
    for (const plan of session.exercisePlans) {
      drafts[plan.id] = {
        weightKg: plan.targetWeightKg ?? 0,
        reps: plan.repRangeMax,
        rir: plan.targetRir,
      };
    }
    this.drafts.set(drafts);
  }

  // --- draft accessors ------------------------------------------------------

  protected draftOf(planId: string): SetDraft {
    return this.drafts()[planId] ?? { weightKg: 0, reps: 1, rir: 0 };
  }

  protected setWeight(planId: string, weightKg: number): void {
    this.patchDraft(planId, { weightKg: Math.max(0, weightKg) });
  }

  protected stepReps(planId: string, direction: 1 | -1): void {
    const next = clamp(this.draftOf(planId).reps + direction, REP_STEP_MIN, REP_STEP_MAX);
    this.patchDraft(planId, { reps: next });
  }

  protected setReps(planId: string, raw: string): void {
    const parsed = Number(raw);
    if (Number.isNaN(parsed)) {
      return;
    }
    this.patchDraft(planId, { reps: clamp(Math.round(parsed), REP_STEP_MIN, REP_STEP_MAX) });
  }

  protected setRir(planId: string, rir: number): void {
    this.patchDraft(planId, { rir });
  }

  private patchDraft(planId: string, patch: Partial<SetDraft>): void {
    this.drafts.update((drafts) => ({
      ...drafts,
      [planId]: { ...this.draftOf(planId), ...patch },
    }));
  }

  // --- start / complete -----------------------------------------------------

  protected start(): void {
    if (this.starting()) {
      return;
    }
    this.starting.set(true);
    this.actionError.set(null);

    this.sessionService.start(this.sessionId).subscribe({
      next: (session) => {
        this.session.set(session);
        this.initDrafts(session);
        this.starting.set(false);
      },
      error: (err: unknown) => {
        this.starting.set(false);
        this.actionError.set(extractErrorMessage(err, 'Ne mogu da započnem trening. Pokušaj ponovo.'));
      },
    });
  }

  protected complete(): void {
    if (this.completing()) {
      return;
    }
    this.completing.set(true);
    this.actionError.set(null);

    this.sessionService.complete(this.sessionId).subscribe({
      next: (result) => {
        this.summary.set(result);
        this.session.update((session) =>
          session ? { ...session, status: 'Completed' } : session,
        );
        this.completing.set(false);
        this.showToast('Trening završen');
        window.scrollTo({ top: 0 });
      },
      error: (err: unknown) => {
        this.completing.set(false);
        this.actionError.set(extractErrorMessage(err, 'Ne mogu da završim trening. Pokušaj ponovo.'));
      },
    });
  }

  // --- set logging ----------------------------------------------------------

  protected editingId(planId: string): string | null {
    return this.editingSetId()[planId] ?? null;
  }

  protected canSubmitSet(planId: string): boolean {
    const draft = this.draftOf(planId);
    return draft.reps >= REP_STEP_MIN && this.savingPlanId() !== planId;
  }

  protected submitSet(plan: ExercisePlanDto): void {
    if (!this.canSubmitSet(plan.id)) {
      return;
    }

    const draft = this.draftOf(plan.id);
    const request = { weightKg: draft.weightKg, reps: draft.reps, rir: draft.rir };
    const editingId = this.editingId(plan.id);
    this.savingPlanId.set(plan.id);
    this.actionError.set(null);

    if (editingId) {
      this.sessionService.updateSet(editingId, request).subscribe({
        next: (saved) => {
          this.replaceSet(plan.id, editingId, saved);
          this.setEditing(plan.id, null);
          this.savingPlanId.set(null);
        },
        error: (err: unknown) => {
          this.savingPlanId.set(null);
          this.actionError.set(extractErrorMessage(err, 'Izmena serije nije sačuvana.'));
        },
      });
      return;
    }

    // Optimistic append: show the set immediately, reconcile with the server id.
    const tempId = `temp-${Date.now()}`;
    const optimistic: SetLogDto = {
      id: tempId,
      exercisePlanId: plan.id,
      setNumber: plan.setLogs.length + 1,
      weightKg: request.weightKg,
      reps: request.reps,
      rir: request.rir,
      performedAt: new Date().toISOString(),
    };
    this.appendSet(plan.id, optimistic);

    this.sessionService.addSet(plan.id, request).subscribe({
      next: (saved) => {
        this.replaceSet(plan.id, tempId, saved);
        this.savingPlanId.set(null);
      },
      error: (err: unknown) => {
        this.removeSet(plan.id, tempId);
        this.savingPlanId.set(null);
        this.actionError.set(extractErrorMessage(err, 'Serija nije sačuvana. Pokušaj ponovo.'));
      },
    });
  }

  protected editSet(planId: string, set: SetLogDto): void {
    this.patchDraft(planId, { weightKg: set.weightKg, reps: set.reps, rir: set.rir });
    this.setEditing(planId, set.id);
  }

  protected cancelEdit(planId: string): void {
    this.setEditing(planId, null);
  }

  protected deleteSet(planId: string, set: SetLogDto): void {
    const snapshot = this.session()?.exercisePlans.find((plan) => plan.id === planId)?.setLogs ?? [];
    this.removeSet(planId, set.id);
    if (this.editingId(planId) === set.id) {
      this.setEditing(planId, null);
    }

    this.sessionService.deleteSet(set.id).subscribe({
      error: (err: unknown) => {
        // Restore the list on failure.
        this.updatePlanSets(planId, () => snapshot);
        this.actionError.set(extractErrorMessage(err, 'Brisanje serije nije uspelo.'));
      },
    });
  }

  protected back(): void {
    void this.router.navigateByUrl('/workout');
  }

  // --- immutable helpers ----------------------------------------------------

  private setEditing(planId: string, setId: string | null): void {
    this.editingSetId.update((map) => ({ ...map, [planId]: setId }));
  }

  private appendSet(planId: string, set: SetLogDto): void {
    this.updatePlanSets(planId, (logs) => [...logs, set]);
  }

  private replaceSet(planId: string, oldId: string, set: SetLogDto): void {
    this.updatePlanSets(planId, (logs) => logs.map((log) => (log.id === oldId ? set : log)));
  }

  private removeSet(planId: string, setId: string): void {
    this.updatePlanSets(planId, (logs) => logs.filter((log) => log.id !== setId));
  }

  private updatePlanSets(planId: string, updater: (logs: SetLogDto[]) => SetLogDto[]): void {
    this.session.update((session) => {
      if (!session) {
        return session;
      }
      return {
        ...session,
        exercisePlans: session.exercisePlans.map((plan) =>
          plan.id === planId ? { ...plan, setLogs: updater(plan.setLogs) } : plan,
        ),
      };
    });
  }

  private showToast(message: string): void {
    this.toast.set(message);
    setTimeout(() => this.toast.set(null), 3200);
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
