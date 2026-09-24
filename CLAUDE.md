# StrengthPlanner — CLAUDE.md

Diplomski rad (bachelor thesis) project: a web system that plans strength training
mesocycles and auto-regulates load from logged sets. The thesis document
`Diplomski - Mateja Jankovic - verzija 2.1.docx` is the authoritative spec.

## Architecture

Clean / onion architecture. Dependencies point inward: `API → Infrastructure → Application → Domain`.
`Domain` has no project references.

```
src/StrengthPlanner.Domain          entities, enums, algorithms (pure, unit-tested core)
src/StrengthPlanner.Application     use-case interfaces, DTOs, workout templates, exceptions
src/StrengthPlanner.Infrastructure  EF Core, Identity, JWT, service implementations, seeding
src/StrengthPlanner.API             controllers, DI, Program.cs
tests/StrengthPlanner.Tests         xUnit tests for the domain algorithms
strength-planner-web                Angular 22 standalone SPA
```

Rules that follow from this layering:

- Algorithms go in `Domain/Algorithms` and must stay free of EF Core and DTOs.
- Services live in `Infrastructure`, their interfaces in `Application/Interfaces`.
- Controllers derive from `AuthorizedControllerBase` and call `GetUserId()`; never
  trust a user id coming from the request body or query string.
- Every query in a service must be scoped by `userId` — this is a single-user-per-account app.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project src/StrengthPlanner.API
```

```bash
npm --prefix strength-planner-web run build
```

Angular dev server: start it through the `web` configuration in `.claude/launch.json`
(preview tooling), never with a bare `npm start` in a background shell.

EF Core migrations use the **local** tool pinned in `.config/dotnet-tools.json` (8.0.10),
not a globally installed `dotnet-ef`:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project src/StrengthPlanner.Infrastructure --startup-project src/StrengthPlanner.API
dotnet dotnet-ef database update --project src/StrengthPlanner.Infrastructure --startup-project src/StrengthPlanner.API
```

## Environment

- .NET SDK 8.0.x (pinned by `global.json`), Node ≥ 22, PostgreSQL 18 running locally.
- Local dev connection string and JWT key live in **user-secrets** for
  `StrengthPlanner.API` — not in `appsettings.json`, and never committed.
- Frontend JWT is stored under the `strength-planner.token` key.
- Dev DB: `Host=localhost;Port=5432;Database=strengthplanner`.

## Language conventions

- **User-facing UI text: Serbian** (Latin script), matching the existing screens
  ("Trening", "Analitika", "Profil").
- **Code, identifiers, XML doc comments: English.**
- Short inline comments explaining *why* a training rule works the way it does may be
  in Serbian, as in the existing entity classes — keep whichever the surrounding file uses.
- **Commit messages and PR text: English.**

## Code style

- C#: file-scoped namespaces, `sealed` for algorithm classes, primary-constructor-free
  explicit constructors with `private readonly` fields, XML `<summary>` on public members
  that encode a training rule, and named lambda parameters (`exercise => exercise.Id`,
  not `x => x.Id`).
- Training constants belong in `Domain/Algorithms/TrainingConstants.cs`; do not inline
  magic numbers for weight steps, correction caps or deload factors.
- Angular: standalone components, signals, `inject()`, new control flow (`@if` / `@for`),
  templates in separate `.html` files. Follow the existing `features/<area>/<name>.ts` layout.
- Prefer extending the domain algorithm and unit-testing it over putting training logic
  inside a controller or an Angular component.

## Verification policy (required before every commit)

The user's standing requirement — do not commit on a red result:

1. `dotnet build` — must succeed.
2. `dotnet test` — the full suite must pass.
3. `npm --prefix strength-planner-web run build` — production build must succeed.
4. **Run the real app end-to-end** for anything user-visible: start the API and the
   Angular dev server, log in, click through the new feature in the browser, and
   capture a screenshot as proof. Manual browser verification is expected, not optional.

Add xUnit tests in `tests/StrengthPlanner.Tests` for every new or changed domain
algorithm — the thesis explicitly claims a unit-tested algorithmic core.

## Git workflow

- **Branches:** `feature/<kebab-case-english-name>`, branched off `main`.
  Example: `feature/failed-reps-logging`, `feature/macrocycles`.
- **One feature per branch, one PR per feature.**
- **Commits:** English, summary line plus a body explaining what changed and why.
  **No `Co-Authored-By: Claude` trailer and no "Generated with Claude Code" footer** —
  this is a thesis repository and the history should read as hand-written.
- **Pull requests:** push the branch, open the PR with `gh pr create`, then have the
  change reviewed by a review agent, fix whatever the review turns up, and merge the PR
  at the end. Do not leave PRs hanging.
- Never commit directly to `main`.
- Migrations are created **and applied to the local dev database** so features can be
  tested against real data.

## Documentation of work

For each implemented feature, write a plain markdown summary file under `docs/features/`
describing what was implemented and why. These are review notes for the user, not thesis
prose — no need for academic style.

## Scope note

Nine rounds of work, all merged to `main`. Every branch got its own PR, an agent code
review, fixes for what the review turned up, and a plain-language write-up in
`docs/features/`. (This line said "two rounds" until round 9 — a count in prose goes stale
the moment it is written, which is why the rounds below are a list and not a number.)

**Round 1 — five "future improvements" from the thesis conclusion:**

| Branch | PR |
|---|---|
| `chore/claude-md` (this file) | #1 |
| `feature/per-exercise-weight-step` | #2 |
| `feature/failed-reps-logging` | #3, rebuilt as #7 |
| `feature/adaptive-volume-landmarks` | #4 |
| `feature/auto-deload` | #5 |
| `feature/macrocycles` | #6 |

**Round 2 — derived from *Džepni priručnik o programiranju treninga*.** The full analysis
with an outcome per item is in [`docs/analiza-prirucnika.md`](docs/analiza-prirucnika.md).

| Branch | Handbook items | PR |
|---|---|---|
| `feature/stimulative-volume` | 1 (proximity-weighted volume), 2 (MAV) | #8 |
| `feature/experience-level` | 4 (level drives programming) | #9 |
| `feature/more-templates` | 8 (movement coverage, solved with templates) | #10 |
| `feature/periodization-models` | 3 (week-by-week periodization) | #11 |

Handbook items 5, 6, 7, 9 and 10 were **skipped by the user's decision**, not on cost —
the reasons are recorded next to each item in the analysis.

**Round 3 — security review of the whole application.** Findings, evidence and what is
still open are in [`docs/security.md`](docs/security.md); deployment steps in
[`docs/deployment-security.md`](docs/deployment-security.md).

| Branch | What it fixed | PR |
|---|---|---|
| `fix/session-data-leak` | Cached lifts of the previous account survived a logout | #12 |
| `fix/account-security` | Password policy, rate limiting, enumeration, password change with token revocation | #13 |
| `fix/deployment-hardening` | Security headers, non-root containers, least-privilege DB role, dependencies | #14 |

The review found **no IDOR and no SQL injection** — cross-user access was tested against the
running API, not just read.

**Round 4 — second security pass, against a 20-item checklist.** Everything is folded into
[`docs/security.md`](docs/security.md), which now answers each of the twenty items and says
plainly which three are not "done" and why.

| Branch | What it fixed | PR |
|---|---|---|
| `fix/row-level-security` | Row ownership enforced in the data layer, not only in each query | #16 |
| `fix/request-validation` | Enum tampering, unbounded email/password, SQL built in the DB init script | #17 |
| `fix/transport-and-abuse` | API security headers, working TLS, PBKDF2 iterations, bot protection | #18 |
| `chore/supply-chain` | Dev-dependency advisories, CI, gitleaks, purged backup branches | #19 |

TLS is no longer an item the repo cannot fix: `docker-compose.tls.yml` ships it, though it
has not been executed here because Docker was unavailable.

Three measurements in that round contradicted the expectation behind the change, and each is
recorded next to the fix rather than quietly dropped: the HTTPS redirect that redirected
nothing, the 500,000-character password that cost 130 ms rather than exhausting a core, and
the query filter that had to be switched off to prove it was doing the work.

**Round 5 — the set count finally aims at the volume target.** Write-up in
[`docs/features/weekly-volume-set-targets.md`](docs/features/weekly-volume-set-targets.md).

| Branch | What it added | PR |
|---|---|---|
| `feature/weekly-volume-set-targets` | Sets per exercise chosen so the week lands on each muscle's MAV, and re-balanced for the week's remaining sessions whenever one is completed | #37 |

MAV had existed since round 2 and was never read by the planner: every exercise got the same
set count from the experience level, so weekly volume per muscle was whatever the template
happened to add up to. `TargetSets` was not rendered on any screen either.

Three measurements from that round are recorded next to the change rather than dropped: the
greedy search piled the whole correction onto the week's first day until distance from the
prescription was made to cost something; the first attribution of *why* a proposal moved
looked at the state before balancing, where the pressure that caused the move does not exist
yet; and balancing cancelled an auto-deload created in the same request, because
`DeloadService` leaves `IsDeload` in the change tracker while the allocator asks the
database — the deload week came back carrying four sets per exercise instead of two.

That last one is the reason `SessionService.CompleteAsync` saves before it rebalances. The
ordering is load-bearing and commented as such.

**Round 6 — a list of fixes the user filed after using the app on a phone.** Every layout
bug on that list was invisible on a desktop.

| Branch | What it fixed | PR |
|---|---|---|
| `fix/mobile-layout-and-copy` | Nav bar wrapping to two rows and covering content, week picker breaking after week 4, plan title filling half the screen, unreadable block dropdowns; copy changes and em dashes | #39 |
| `fix/profile-fields` | Sex was free text, so registration wrote `male` and the profile offered `M` and never matched; now an enum. `TrainingDaysPerWeek` removed entirely | #40 → #42 |
| `feature/custom-workout-templates` | User-built templates: own days, exercises, sets and rep ranges, with periodization, deload and MAV rebalancing still applied on top | #41 |

Round 6 was reviewed as a **stack** — each PR opened on top of the previous one so all three
were reviewable before the first merged. That cost one mistake worth remembering: GitHub does
**not** retarget a stacked PR when its base merges while the base branch still exists, so
merging #40 put it back into `fix/mobile-layout-and-copy` instead of `main`. #42 landed the
same commits on `main`. Either delete the base branch on merge, or retarget the child to
`main` first (`gh pr edit <n> --base main`), which is what #41 did.

The set-count decision worth remembering: a custom template's numbers are the **week-1
prescription and the anchor**, not the final answer. `Periodization.ForWeek` already took the
base as a parameter, so custom templates needed no new algorithm — but the auto-deload path
did, because it recovered the base from the goal. `ExercisePlan.BaseRepRangeMin/Max` exist
because the rep shift cannot be inverted: `ForWeek` clamps, so two different bases produce
the same week.

**Round 7 — a second phone-use list, this one about navigation rather than layout.**

| Branch | What it changed | PR |
|---|---|---|
| `fix/template-editor-layout` | Sets/reps row: "Ponavljanja od" wrapped while its neighbours did not, so the three inputs sat at different heights | #44 |
| `chore/templates-in-profile` | "Moji šabloni" moved out of the mesocycle wizard and into Profil | #45 |
| `feature/macrocycle-first` | Training is created only through the plan; `/mesocycle` removed, deleting moved to plan level | #46 |
| `feature/macrocycle-block-preview` | Tap a block to see what is in it; removed the duplicate "Idi na trening" | #47 |

Stacking worked this round: children were retargeted with `gh pr edit <n> --base main`
before each merge, so nothing landed on the wrong base.

The finding that shaped round 7: **the two creation paths were never two things.**
`POST /api/mesocycles` built a macrocycle with a single block and returned its mesocycle, so
every mesocycle already belonged to a plan. Removing the second screen changed no data model
— it removed a second face on the same operation.

That duplication was also the reported "delete does not work". Deleting a mesocycle left its
block empty, and `EnsureCurrentBlockAsync` — which runs on **every read** of the plan — reads
an empty first block as "never generated" and regenerates it. Deletion and self-repair were
fighting, and self-repair won. The fix was not to weaken the repair but to remove the only
way to orphan a block: `POST`/`DELETE` on `/api/mesocycles` are gone, and deletion happens at
plan level, where `DeleteAsync` removes the blocks' mesocycles **explicitly** because the
block→mesocycle FK is `SetNull`. `PlanDeletionTests` pins that reasoning at the EF model
level, so flipping the FK later fails loudly instead of silently orphaning rows.

One regression had to be repaired in the same PR: the mesocycle screen was the only place
that showed a template's days, exercises and warnings, so the block wizard had to learn to
show them.

**Round 8 — a third phone-use list, this one about registration and the profile.** The
reference screenshots came from HEVY; the structure was taken, the dark palette was not,
because a theme is a decision about every screen.

| Branch | What it changed | PR |
|---|---|---|
| `feature/registration-wizard` | Registration became eight screens, one question each; `HeightCm` and `DisplayName` added | #49 |
| `feature/profile-identity` | Name and picture on the profile, all editing moved to `/profile/edit` | #50 |
| `feature/profile-dashboard` | Profile became a view with a dashboard; `/settings`, `/exercises`, account deletion | #51 |

Sequential, not stacked — each merged before the next branched, so the round-6 retargeting
trap could not recur.

Decisions worth keeping:

- **The account is created after the last question**, in one request. Abandoning the flow
  leaves nothing behind. The cost is that a taken email only surfaces at the end, which is
  why that step is second.
- **The 1RM step lives on `/onboarding`, not inside `/register`.** It needs a token, and
  while it was a `@switch` case a page refresh dropped an authenticated user back to
  question one. `REGISTRATION_STEP_COUNT` is shared because two routes draw one progress bar.
- **A picture's type is decided from its bytes**, never from `Content-Type` or the file
  name — that type is handed to every browser that opens the profile. SVG is deliberately
  refused: it is an image that carries script.
- **`PUT /api/auth/profile` replaces the profile wholesale**, so a client that omits a field
  erases it. That bit once, in the same PR that introduced the fields.
  `ProfileReplacementTests` now asserts by reflection that every writable `Profile` property
  is carried by `UpdateProfileDto`, with a named exclusion list.
- **Account deletion order follows the foreign keys**: templates, then the account, then the
  user's own exercises — in one transaction. `UserWorkoutTemplate` and
  `Exercise.CreatedByUserId` carry a bare `Guid` with no FK to the account, so no cascade
  reaches them. `AccountDeletionTests` walks the model and demands every owner-carrying
  entity be cascaded or named in the by-hand list.

Four measurements from this round are recorded next to the code rather than dropped:

`canContinue` was a `computed()` over `form.controls.*.valid`. `computed` tracks signals and
`AbstractControl.valid` is not one, so it evaluated once against the empty form and stayed
false — the wizard's first pass stopped on question one with a dead button. No form test
would catch it. The same construct later guarded account deletion, and this time it was
tested.

A clamped `MeasureInput` displayed the value the model had rejected, because Angular does
not rewrite `[value]` when the signal has not changed. The identical defect was already
documented in `profile-home.ts` for the weight-step select — and reintroduced anyway. Both
now write the value back by hand, and both have a test that goes red without it.

The `/onboarding` screen overflowed a phone by 21px, and **had done so since it existed** —
the exercise `<select>` wants the width of its longest option, and `min-width: 0` permits
shrinking without changing the min-content contribution a grid parent asks for.
`grid-template-columns: minmax(0, 1fr)` is the fix.

The deletion confirmation word was compared with `CurrentCultureIgnoreCase`, and it ends in
`I`. On a Turkish-culture host the account would have been undeletable, with a message
telling the user to type what they had just typed. Nothing pins the culture anywhere in the
project. A test now asserts the old comparison genuinely fails under `tr-TR`, so the reason
for `OrdinalIgnoreCase` is measured rather than asserted.

One misdiagnosis is recorded too, because it nearly landed: `/profile/edit` opened the
workout screen and I blamed route prefix-matching, reordered the routes and commented that
as measured. It was not. A stuck dev server was failing to compile a stylesheet that did
not exist yet, so the lazy import rejected and the router fell to the catch-all. The order
was restored, then changed again on its own merits with an honest comment.

**Do not commit a document that lists unfixed weaknesses of the live app: this repository is
public.** Security notes describe what is closed and how it is verified; anything still open
is stated at a level useful to the owner, not to an attacker.

Round 8 also added `shared/styles/_form-shell.scss` and
`shared/components/subscreen-header`. Both exist because the same duplication appeared
twice: card and field rules copied across profile screens, then the sub-screen header
copied across the three screens that use those rules. When a third screen wants a block,
move it — an `@use` reaching into another feature's folder hides that the rules are no
longer that feature's.

**Round 9 — an audit of the training logic itself, against the thesis and the handbook.**
The user asked for every inconsistency in how sets are entered, volume is counted and load
is raised. Eight of them changed the weight the lifter is handed, and that is section A —
the part that was built. Analysis and outcomes per finding are in each write-up.

| Branch | What it fixed | PR |
|---|---|---|
| `fix/load-progression` | Reaching the top of the rep range could hold or *lower* the load while the summary showed an arrow up; a set below the range with reserve raised it | #60 |
| `fix/progression-reference-and-summary` | Progression started from the **average** of the session's weights, a skipped exercise carried full load into a deload, and the arrow did not describe the number beside it | #61 |
| `fix/e1rm-reliability` | A set far from failure produced an e1RM, and one inflated estimate stayed the block's starting load for eight weeks | #62 |
| `feature/bodyweight-load` | A pull-up was logged as 0 kg, so its e1RM was 0, its tonnage 0, and progression offered "+2.5 kg" on a set that was never loaded | #63 |

Sequential, each merged before the next branched.

The finding that shaped the round: **the regression test for the load rule passed by
accident.** `ComputeNext_DoesNotDriveWeightDown_...` used 100 kg, and 100 kg happens to sit
inside the band (~42–125 kg on a 2.5 kg step) where the old formula's `-3% + step` cancels
out. At 160 kg the same rule *lowered* the weight eight sessions in a row while reporting a
rise. One number chosen for a test decided whether a whole rule was believed to work, so it
was replaced by a grid `[Theory]` plus property tests over steps × weights × ranges ×
target RIR, with the old formula kept as an oracle for everywhere the change was not
intended. 34 of them fail against the old rule.

Decisions worth keeping:

- **The range reset pays the RIR shortfall.** Coming back to the bottom of the range is
  worth `max - min` reps, so a top-of-range session steps up whenever
  `deviation + (max - min) >= 0` — by Epley exactly the condition under which the next
  prescription can be lifted at the used load. Only a narrow week with a large target RIR
  (11–12 at RIR 2) fails it, and there the load holds. Reaching the top never lowers it.
- **The reference load is the heaviest lifted, not the average.** A lighter set counts
  toward it only if it went to failure: failing at 90 kg means failing at least as early at
  100. A lighter set that kept reserve says nothing about the heavier one.
- **Rounding must not reverse the correction.** The step is how finely a load can be
  expressed; the sign of the correction is the decision. At 102 kg with -1% the old code
  rounded 100.98 up to 102.5.
- **A set far from failure is not evidence of a maximum.** `CanEstimateFrom` is one
  predicate (load > 0, reps ≤ 12, RIR ≤ 3) shared by the summary, the fatigue score and the
  baseline; 100 kg × 12 at RIR 5 "reads" 157 kg where the same set to failure reads 140.
- **Body mass is load.** Every rule works on `added + body`, and the result converts back to
  what goes on the belt — rounded in *added* space, because the step exists on the belt and
  not on the body.

Seven measurements from this round contradicted the expectation behind the change, and each
is recorded next to the code rather than dropped:

1. The 100 kg test above.
2. **The rebound after a deload, which nobody had reported.** A week following a deload
   progressed from the deload's 90%, so the load came back below what had been earned. It
   exists whenever fatigue pulls a deload forward and releases the planned one. Found by a
   completeness critic on the design, not by use.
3. **The migration's backfill had to be abandoned.** The plan was to write `mass × share`
   into every existing set. Counting first showed that not one bodyweight set was logged at
   0 kg — all 119 carried 38–77 kg, the number the lifter typed for the whole movement — so
   the backfill would have counted the same body twice and pushed a pull-up estimate from
   ~90 kg to ~200. Only plans of sessions that had not started were converted.
4. **A set read `0 kg` when logged and `TM × 12` after a reload.** `SetLog → SetLogDto` was
   built by hand in three services and the new column reached two of them; an object
   initializer need not be complete, so it compiled and typechecked. The fix deleted the
   third place: `SetLogMapper.ToDto` is the only path now. Nothing but the running app could
   have found it.
5. **A test was green for a wrong implementation.** Every `AddedTarget` case carried a body
   portion of 80 kg with a 2.5 kg step, and 80 = 32 × 2.5 — for a portion on the step grid,
   rounding the total and rounding the added load are *identically equal*. Flipping the
   implementation kept all 489 tests green. The cases now use portions that are not
   multiples of the step (51.2 at 1 kg, 68 at 2.5), and the property grid asserts the
   proposal is always on the grid; the flipped version fails 5 tests.
6. **`UndoDeload` invented load at the floor** — a regression introduced by the same branch
   that made it portion-aware. A deload target of zero is where the clamp engaged, so the
   load it came from is gone; dividing by 0.90 anyway "restored" `80 / 0.9 - 80 = 8.88`, and
   a lifter who does pull-ups with nothing added came back prescribed `TM + 8 kg`. A clamp is
   not invertible, and understating is the safe direction here.
7. **The write-up's headline measurement used a step the exercise does not have.** It said a
   pull-up goes from 13 kg to 17.5; a pull-up steps by 1 kg (`EquipmentWeightStep` for
   "Bodyweight"), so the real numbers are 12 → 16. The test reached 17.5 only because it
   passed the step by hand — a test that supplies a constant the app derives will happily
   describe an app that does not exist.

One process note, because it cost two green runs: **a review agent proved a point by editing
the working tree and did not put it back.** It flipped `AddedTarget` to total-space rounding
to measure that the suite stayed green, reported that correctly, and left the edit on disk;
the next two `dotnet test` runs were against its version. Check `git status` after a review
agent has run, before believing a result.

Deliberately **out of scope**: i18n, full-history analytics, undulating periodization,
PWA/offline, changing an already-generated block's periodization model, email delivery (so no
password reset and no email confirmation), and a dark theme (the reference screenshots for
round 8 were dark; only their structure was taken).
