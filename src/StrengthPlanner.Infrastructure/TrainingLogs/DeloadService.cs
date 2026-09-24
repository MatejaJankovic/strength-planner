using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Ocenjuje umor iz završene nedelje i, ako je prešao prag, pretvara sledeću nedelju u
/// deload. Planirani deload u četvrtoj nedelji ostaje kao donja granica — ovo ga samo
/// može povući ranije kada podaci to traže.
/// </summary>
public sealed class DeloadService
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _landmarks;

    public DeloadService(AppDbContext db, VolumeLandmarkService landmarks)
    {
        _db = db;
        _landmarks = landmarks;
    }

    /// <summary>
    /// Ocenjuje svaku završenu nedelju mezociklusa koja još nema ocenu i po potrebi
    /// pretvara sledeću u deload. Ocena se upisuje uslovnim UPDATE-om, pa se nedelja
    /// ne može oceniti dvaput ni kada dva zahteva istovremeno završe njene sesije.
    /// </summary>
    public async Task<DeloadOutcome?> EvaluatePendingWeeksAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        var pendingWeeks = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && !week.IsDeload
                           && week.FatigueScore == null
                           && week.Sessions.All(session => session.Status == SessionStatus.Completed))
            .OrderBy(week => week.WeekNumber)
            .Select(week => new { week.Id, week.WeekNumber })
            .ToListAsync(cancellationToken);

        foreach (var week in pendingWeeks)
        {
            var evaluated = await EvaluateWeekAsync(
                userId,
                mesocycleId,
                week.Id,
                week.WeekNumber,
                cancellationToken);

            // Prekid posle prve konverzije je namerno. Pretvaranje nedelje u deload je
            // tek promena u change trackeru, pa bi upit za sledeću nedelju u narednom
            // krugu i dalje video staro stanje u bazi i mogao da pretvori i nju. Kada se
            // nadoknađuje više nedelja odjednom, ionako je ispravno stati na prvom
            // deload-u: ono što sledi posle njega više nije ista situacija.
            if (evaluated is not null)
            {
                return evaluated;
            }
        }

        return null;
    }

    private async Task<DeloadOutcome?> EvaluateWeekAsync(
        Guid userId,
        Guid mesocycleId,
        Guid weekId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        var fatigue = await BuildWeeklyFatigueAsync(userId, mesocycleId, weekId, weekNumber, cancellationToken);

        // Nedelja bez ijedne upisane serije nema šta da kaže o umoru, ali mora da dobije
        // ocenu — inače ostaje "neocenjena" zauvek i svaki naredni završetak treninga
        // je iznova učitava.
        var score = fatigue is null ? 0m : FatigueEvaluator.Score(fatigue);

        // Upis ocene je ujedno i preuzimanje nedelje: drugi zahtev vidi ocenu i odustaje.
        var claimed = await _db.TrainingWeeks
            .Where(week => week.Id == weekId
                           && week.Mesocycle.UserId == userId
                           && week.FatigueScore == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(week => week.FatigueScore, score),
                cancellationToken);

        // Prag zavisi od nivoa iskustva. Početnik ga nema: priručnik je izričit da
        // "početnici ne treba da razmišljaju o ovome", a i signali od kojih se ocena gradi
        // su kod njih najmanje pouzdani — RIR procenjuju loše jer staju na pečenju misleći
        // da su na otkazu. Nepotreban deload ih košta nedelje napretka, pa im ostaje samo
        // planirani deload na kraju bloka.
        var threshold = ExperienceProgramming.DeloadThreshold(
            await GetExperienceLevelAsync(userId, cancellationToken));

        if (claimed == 0 || threshold is null || score < threshold.Value)
        {
            return null;
        }

        // Deload se sme staviti samo na nedelju koja još nije počela: prepisivanje
        // ciljeva već odrađenog ili započetog treninga bi falsifikovalo istoriju, a
        // korisniku koji je usred nedelje menjalo plan pod rukama.
        var nextWeek = await _db.TrainingWeeks
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && week.WeekNumber == weekNumber + 1
                           && !week.IsDeload
                           && week.Sessions.All(session => session.Status == SessionStatus.Planned))
            .FirstOrDefaultAsync(cancellationToken);

        // Nema sledeće nedelje, već je deload, ili je počela — ocena je upisana, ali
        // nema šta da se menja.
        if (nextWeek is null)
        {
            return null;
        }

        // Propis nedelje zavisi od modela: kod periodizovanog bloka nedelja koja postaje
        // deload nosi rep-opseg i RIR svoje faze, a ne cilja. Bez ovoga bi „rasterećenje"
        // ostalo na propisu faze intenziteta — kod hipertrofije doslovno serije do otkaza.
        var block = await _db.Mesocycles
            .AsNoTracking()
            .Where(mesocycle => mesocycle.Id == mesocycleId && mesocycle.UserId == userId)
            .Select(mesocycle => new { mesocycle.Goal, mesocycle.PeriodizationModel })
            .FirstOrDefaultAsync(cancellationToken);

        // Bez mezociklusa nema ni cilja ni modela, a pretpostavljati ih (ravan blok, RIR
        // iz samog plana) znaci propisati nedelju iz pogodjenih vrednosti. Upit je vec
        // filtriran po korisniku, pa je null ovde "nije njegov blok".
        if (block is null)
        {
            return null;
        }

        var goal = GoalPrescriptions.ForGoal(block.Goal);
        var model = block.PeriodizationModel;

        await ApplyDeloadAsync(
            userId,
            mesocycleId,
            weekId,
            nextWeek.Id,
            nextWeek.WeekNumber,
            model,
            goal,
            cancellationToken);

        nextWeek.IsDeload = true;
        nextWeek.IsAutoDeload = true;

        var plannedDeloadRestored = await RestorePlannedDeloadAsync(
            userId,
            mesocycleId,
            nextWeek.WeekNumber,
            model,
            goal,
            cancellationToken);

        return new DeloadOutcome(weekNumber, nextWeek.WeekNumber, score, plannedDeloadRestored);
    }

    private async Task<ExperienceLevel> GetExperienceLevelAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;
    }

    /// <summary>
    /// Mezociklus nosi jedan deload. Kada ga umor povuče ranije, planirani deload na
    /// kraju se vraća u običnu trenažnu nedelju — inače bi blok ostao sa dva rasterećenja,
    /// a u lošijem slučaju i sa izolovanim pojedinačnim nedeljama treninga između njih.
    /// Vraća broj nedelje koja je oslobođena, ili null.
    ///
    /// Oslobođena nedelja preuzima propis nedelje koja je upravo žrtvovana za deload: taj
    /// deo bloka time nije izgubljen nego pomeren za nedelju dana. Kod ravnog bloka su svi
    /// propisi isti, pa se ponaša kao i ranije.
    /// </summary>
    private async Task<int?> RestorePlannedDeloadAsync(
        Guid userId,
        Guid mesocycleId,
        int autoDeloadWeekNumber,
        PeriodizationModel model,
        GoalPrescription goal,
        CancellationToken cancellationToken)
    {
        var planned = await _db.TrainingWeeks
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && week.IsDeload
                           && !week.IsAutoDeload
                           && week.WeekNumber != autoDeloadWeekNumber
                           && week.Sessions.All(session => session.Status == SessionStatus.Planned))
            .FirstOrDefaultAsync(cancellationToken);

        if (planned is null)
        {
            return null;
        }

        // Polazni broj serija bloka se čita iz nedelje koja nosi osnovu, a izvodi se iz
        // druge nedelje samo kada je i ona postala deload. Ne čita se iz profila namerno:
        // korisnik koji je usred bloka promenio nivo iskustva ne sme time da promeni oblik
        // već napravljenog plana.
        var baseSetsFromBaseWeek = await LoadBaseSetsAsync(
            userId,
            mesocycleId,
            model,
            cancellationToken);

        var reference = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && !plan.WorkoutSession.TrainingWeek.IsDeload
                           && plan.WorkoutSession.TrainingWeek.WeekNumber != autoDeloadWeekNumber)
            .Select(plan => new
            {
                plan.WorkoutSession.DayLabel,
                plan.ExerciseId,
                plan.PrescribedSets,
                plan.BaseRepRangeMax,
                plan.WorkoutSession.TrainingWeek.WeekNumber
            })
            .ToListAsync(cancellationToken);

        // Propisani broj serija, ne predloženi: predlog je pomeren balansiranjem volumena,
        // pa bi obrtanje periodizacije nad njim vratilo polaznu vrednost koju blok nikada
        // nije imao.
        var baseSetsByExerciseAndDay = reference
            .GroupBy(item => (item.DayLabel, item.ExerciseId))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    if (baseSetsFromBaseWeek.TryGetValue(group.Key, out var storedBaseSets))
                    {
                        return storedBaseSets;
                    }

                    // Rezerva: osnovni opseg ide uz broj serija, jer kada Epley granica
                    // pojede pomeraj ponavljanja, nedelja ga nosi kao seriju — pa se ista
                    // brojka razlaze drugacije za hipertrofijsku nego za vezbu snage.
                    var sample = group.First();
                    return Periodization.BaseSetsFrom(
                        model,
                        sample.WeekNumber,
                        sample.PrescribedSets,
                        sample.BaseRepRangeMax);
                });

        var plans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
            .Where(plan => plan.WorkoutSession.TrainingWeekId == planned.Id)
            .ToListAsync(cancellationToken);

        foreach (var plan in plans)
        {
            if (!baseSetsByExerciseAndDay.TryGetValue(
                    (plan.WorkoutSession.DayLabel, plan.ExerciseId),
                    out var baseSets))
            {
                continue;
            }

            // Nedelja preuzima propis one koja je postala deload — dakle faze koja bi
            // inače ispala iz bloka.
            //
            // Osnovni opseg se čita iz samog plana, ne iz cilja: vežba iz ličnog šablona
            // nosi opseg koji je korisnik uneo, pa bi opseg cilja ovde tiho prepisao njegov
            // unos. Za ugrađen šablon su te dve vrednosti iste, pa se ponaša kao i ranije.
            var prescription = Periodization.ForWeek(
                model,
                autoDeloadWeekNumber,
                plan.BaseRepRangeMin,
                plan.BaseRepRangeMax,
                // RIR cilja, nikada onaj koji plan nosi: plan ove nedelje je planirani
                // deload, pa nosi RIR deload-a (cilj + DeloadRirShift). Propisati nedelju
                // iz njega znacilo bi trenaznu nedelju dve rezerve laksu od plana.
                goal.TargetRir,
                baseSets);

            // Nedelja dobija nov propis, pa se sidro pomera zajedno sa predlogom;
            // balansiranje volumena posle ovoga kreće od nove vrednosti.
            plan.TargetSets = prescription.Sets;
            plan.PrescribedSets = prescription.Sets;
            plan.RepRangeMin = prescription.RepRangeMin;
            plan.RepRangeMax = prescription.RepRangeMax;
            plan.TargetRir = prescription.TargetRir;
        }

        planned.IsDeload = false;

        return planned.WeekNumber;
    }

    /// <summary>
    /// Pretvara nedelju u deload: prepolovljene serije i 90% opterećenja koje je STVARNO
    /// korišćeno u prethodnoj nedelji. Opterećenja se preračunavaju jer ih je progresija
    /// već popunila dok se prethodna nedelja završavala.
    /// </summary>
    private async Task ApplyDeloadAsync(
        Guid userId,
        Guid mesocycleId,
        Guid completedWeekId,
        Guid deloadWeekId,
        int deloadWeekNumber,
        PeriodizationModel model,
        GoalPrescription goal,
        CancellationToken cancellationToken)
    {
        // Serije se grupišu u memoriji, jer referentnu težinu bira domensko pravilo
        // (WorkingLoad): najteža podignuta, a ne prosek svih. Prosek je jednu back-off
        // seriju pretvarao u niži deload od 90% onoga što je vežbač zaista radio. Radi se
        // o jednoj nedelji jednog korisnika, pa je i skup mali.
        var completedSets = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == completedWeekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new
            {
                set.ExercisePlan.ExerciseId,
                set.ExercisePlan.WorkoutSession.DayLabel,
                set.WeightKg,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.BodyweightLoadKg
            })
            .ToListAsync(cancellationToken);

        // Cuva se UKUPNO opterecenje reference: 90% se primenjuje na ono sto je telo
        // zaista pomerilo, pa se tek onda vraca u dodate kilograme.
        var usedByExerciseAndDay = completedSets
            .GroupBy(set => (set.ExerciseId, set.DayLabel))
            .ToDictionary(
                group => group.Key,
                group => WorkingLoad.Select(group
                        .Select(set => new LoggedSet(
                            set.WeightKg,
                            set.Reps,
                            set.Rir,
                            set.IsFailure,
                            set.BodyweightLoadKg))
                        .ToList())!
                    .ReferenceTotalLoadKg);

        // Rezerva za vežbu bez ijedne upisane serije je propis ZAVRŠENE nedelje, ne cilj
        // same deload nedelje: taj cilj je progresija upravo popunila, pa bi 90% od njega
        // bilo izvedeno iz težine koja nikada nije podignuta.
        var plannedInCompletedWeek = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSession.TrainingWeekId == completedWeekId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.TargetWeightKg != null)
            .Select(plan => new
            {
                plan.ExerciseId,
                plan.WorkoutSession.DayLabel,
                plan.TargetWeightKg
            })
            .ToListAsync(cancellationToken);

        var plannedByExerciseAndDay = plannedInCompletedWeek
            .GroupBy(plan => (plan.ExerciseId, plan.DayLabel))
            .ToDictionary(group => group.Key, group => group.First().TargetWeightKg!.Value);

        var plans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
            .Where(plan => plan.WorkoutSession.TrainingWeekId == deloadWeekId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.Status == SessionStatus.Planned)
            .ToListAsync(cancellationToken);

        var exerciseIds = plans.Select(plan => plan.ExerciseId).Distinct().ToList();
        var weightStepByExerciseId = await WeightStepResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);
        var bodyweightByExerciseId = await BodyweightPortionResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);
        var baseSetsByExerciseAndDay = await LoadBaseSetsAsync(
            userId,
            mesocycleId,
            model,
            cancellationToken);

        foreach (var plan in plans)
        {
            // Serije se polove u odnosu na POLAZNI broj bloka, ne u odnosu na ono što ta
            // nedelja nosi: nedelja faze volumena nosi seriju više, pa bi polovljenje
            // njene vrednosti dalo preobiman deload.
            // Obrtanje ide nad propisom: predlog je u međuvremenu pomeren balansiranjem
            // volumena, pa bi polovljenje njegove vrednosti dalo deload izveden iz broja
            // koji periodizacija nikada nije propisala.
            var baseSets = baseSetsByExerciseAndDay.TryGetValue(
                (plan.WorkoutSession.DayLabel, plan.ExerciseId),
                out var storedBaseSets)
                ? storedBaseSets
                : Periodization.BaseSetsFrom(
                    model,
                    deloadWeekNumber,
                    plan.PrescribedSets,
                    plan.BaseRepRangeMax);
            plan.TargetSets = Periodization.DeloadSets(baseSets);
            plan.PrescribedSets = plan.TargetSets;

            // Rasterećenje vraća osnovni rep-opseg, a RIR diže iznad cilja. Kod
            // periodizovanog bloka je opseg jedina stvar koja sprečava deload propisan do
            // otkaza; RIR je druga, i nju je deload ranije ispuštao.
            //
            // Osnova je opseg tog plana, a ne cilja: kod ličnog šablona to je opseg koji je
            // korisnik uneo za tu vežbu. Za ugrađen šablon su iste vrednosti.
            plan.RepRangeMin = plan.BaseRepRangeMin;
            plan.RepRangeMax = plan.BaseRepRangeMax;

            plan.TargetRir = Periodization.DeloadRir(goal.TargetRir);

            var key = (plan.ExerciseId, plan.WorkoutSession.DayLabel);
            var bodyweightLoadKg = BodyweightPortionResolver.PortionFor(
                bodyweightByExerciseId,
                plan.ExerciseId);

            // Rezerve nose DODATE kilograme (to je ono sto plan cuva), pa im se deo tela
            // dodaje ovde; odradjena referenca je vec ukupna.
            decimal? baseTotalKg = usedByExerciseAndDay.TryGetValue(key, out var used)
                ? used
                : plannedByExerciseAndDay.TryGetValue(key, out var planned)
                    ? planned + bodyweightLoadKg
                    // Poslednja rezerva je cilj same nedelje koja postaje deload: kada je
                    // progresija upisan taj cilj, nedelja još nije bila rasterećenje, pa je
                    // to puna težina. Bez ove grane bi ostala nedirnuta, dakle 100%.
                    : plan.TargetWeightKg + bodyweightLoadKg;

            if (baseTotalKg is null)
            {
                continue;
            }

            plan.TargetWeightKg = BodyweightLoad.AddedTarget(
                baseTotalKg.Value * TrainingConstants.DeloadWeightFactor,
                bodyweightLoadKg,
                WeightStepResolver.StepFor(weightStepByExerciseId, plan.ExerciseId));
        }
    }

    /// <summary>
    /// Polazni broj serija bloka, po paru (naziv dana, vežba), pročitan iz nedelje koja
    /// nosi osnovu — one bez ijednog pomeraja (<see cref="Periodization.BaseWeekNumber"/>).
    ///
    /// Čita se, a ne izvodi. Obrtanje pomeraja radi samo nad redom koji je upisala ova
    /// verzija pravila; zatečeni red nosi broj koji je propisala neka starija, pa bi iz
    /// njega izvedena osnova bila nedelja koju blok nikada nije imao. Prazno kada je i sama
    /// osnovna nedelja postala deload — tada pozivalac pada na izvođenje.
    /// </summary>
    private async Task<Dictionary<(string DayLabel, Guid ExerciseId), int>> LoadBaseSetsAsync(
        Guid userId,
        Guid mesocycleId,
        PeriodizationModel model,
        CancellationToken cancellationToken)
    {
        var baseWeekNumber = Periodization.BaseWeekNumber(model);

        var plans = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.TrainingWeek.WeekNumber == baseWeekNumber
                           && !plan.WorkoutSession.TrainingWeek.IsDeload)
            .Select(plan => new
            {
                plan.WorkoutSession.DayLabel,
                plan.ExerciseId,
                plan.PrescribedSets
            })
            .ToListAsync(cancellationToken);

        return plans
            .GroupBy(plan => (plan.DayLabel, plan.ExerciseId))
            .ToDictionary(group => group.Key, group => group.First().PrescribedSets);
    }

    /// <summary>
    /// Skuplja četiri signala umora iz nedelje. Vraća null kada nedelja nema nijednu
    /// odrađenu seriju — o umoru se tada nema šta zaključiti.
    /// </summary>
    private async Task<WeeklyFatigue?> BuildWeeklyFatigueAsync(
        Guid userId,
        Guid mesocycleId,
        Guid weekId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        var sets = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == weekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new SetSignal(
                set.ExercisePlan.ExerciseId,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.WeightKg,
                set.BodyweightLoadKg,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin))
            .ToListAsync(cancellationToken);

        if (sets.Count == 0)
        {
            return null;
        }

        // RIR se meri samo nad dovršenim serijama i efektivnim RIR-om, istim koji koristi
        // progresija (vidi FatigueEvaluator.AverageRirDeviation).
        var rirDeviation = FatigueEvaluator.AverageRirDeviation(sets.Select(set => new RirSample(
            new WorkingSet(set.Reps, set.Rir, set.IsFailure),
            set.RepRangeMin,
            set.TargetRir)));

        // Koliko ispod cilja dovršena serija unutar opsega može da padne: RIR ne ide ispod
        // nule. Serija koja je stala ispod donje granice opsega ume i niže (kapacitet se meri
        // prema dnu opsega), pa ocena takvo odstupanje ograniči na punu težinu signala.
        var achievableDeficit = sets.Max(set => (decimal)set.TargetRir);
        var failureShare = (decimal)sets.Count(set => set.IsFailure) / sets.Count;

        var e1RmChange = await GetE1RmChangeShareAsync(userId, mesocycleId, weekNumber, sets, cancellationToken);
        var volumeShare = await GetVolumeVsMrvShareAsync(userId, weekId, cancellationToken);

        return new WeeklyFatigue(
            rirDeviation,
            achievableDeficit,
            failureShare,
            e1RmChange,
            volumeShare);
    }

    /// <summary>
    /// Relativna promena snage u odnosu na poslednju uporedivu nedelju, po pravilu iz
    /// <see cref="StrengthChange"/>. Nula kada poređenja nema — nedostatak podatka ne sme
    /// da se protumači kao pad.
    /// </summary>
    private async Task<decimal> GetE1RmChangeShareAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        IReadOnlyList<SetSignal> currentSets,
        CancellationToken cancellationToken)
    {
        if (weekNumber <= 1)
        {
            return 0m;
        }

        // Poređenje ide sa poslednjom nedeljom koja NIJE bila deload: deload serije su
        // namerno submaksimalne, pa bi nedelja posle deload-a uvek izgledala kao skok.
        var comparableWeekNumber = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && week.WeekNumber < weekNumber
                           && !week.IsDeload)
            .OrderByDescending(week => week.WeekNumber)
            .Select(week => (int?)week.WeekNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (comparableWeekNumber is null)
        {
            return 0m;
        }

        var previousSets = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == comparableWeekNumber
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new SetSignal(
                set.ExercisePlan.ExerciseId,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.WeightKg,
                set.BodyweightLoadKg,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin))
            .ToListAsync(cancellationToken);

        if (previousSets.Count == 0)
        {
            return 0m;
        }

        // Poređenje je domensko pravilo (StrengthChange): ista vežba, isti broj efektivnih
        // ponavljanja, tolerancija jednog. Bez uslova uporedivosti je serija na vrhu jedne
        // nedelje naspram serije na dnu sledeće — obe po propisu — čitala kao pad od skoro
        // deset odsto, dvostruko od 5% na kojima signal dobija punu težinu.
        //
        // Nedostatak uporedivog para je nedostatak dokaza, pa nosi nulu, kao i nedostatak
        // uporedive nedelje iznad.
        return StrengthChange.ChangeShare(
            currentSets.Select(ToStrengthSample),
            previousSets.Select(ToStrengthSample)) ?? 0m;
    }

    private static StrengthSample ToStrengthSample(SetSignal set)
    {
        return new StrengthSample(set.ExerciseId, set.Reps, set.Rir, set.TotalLoadKg);
    }

    /// <summary>
    /// Najveći odnos odrađenog volumena i MRV-a među mišićnim grupama. Koristi lične
    /// granice ako ih korisnik ima, pa je i ovaj signal prilagođen njemu.
    /// </summary>
    private async Task<decimal> GetVolumeVsMrvShareAsync(
        Guid userId,
        Guid weekId,
        CancellationToken cancellationToken)
    {
        var responses = await _landmarks.GetWeeklyResponsesAsync(userId, weekId, cancellationToken);
        if (responses.Count == 0)
        {
            return 0m;
        }

        var landmarks = await _landmarks.GetEffectiveAsync(userId, cancellationToken);
        decimal highest = 0m;

        foreach (var (muscleGroupId, response) in responses)
        {
            if (landmarks.TryGetValue(muscleGroupId, out var landmark) && landmark.Mrv > 0)
            {
                // Sirovi zbir, ne stimulativni: MRV je granica OPORAVKA, a oporavak troši
                // svaka odrađena serija — i ona daleko od otkaza, koja stimulus ne pravi.
                // Sa stimulativnim zbirom bi nedelja puna lakših serija delovala kao
                // odmor i tiho ugasila deload koji treba da se desi.
                highest = Math.Max(highest, response.RawSets / landmark.Mrv);
            }
        }

        return highest;
    }

    private sealed record SetSignal(
        Guid ExerciseId,
        int Reps,
        int Rir,
        bool IsFailure,
        decimal WeightKg,
        decimal BodyweightLoadKg,
        int TargetRir,
        int RepRangeMin)
    {
        /// <summary>Everything the set moved, body included.</summary>
        public decimal TotalLoadKg => WeightKg + BodyweightLoadKg;
    }
}

/// <summary>Rezultat automatskog deload-a, za poruku korisniku posle treninga.</summary>
/// <param name="TriggeredByWeek">Nedelja iz koje je umor izračunat.</param>
/// <param name="DeloadWeek">Nedelja koja je pretvorena u deload.</param>
/// <param name="FatigueScore">Ocena umora, 0 do 1.</param>
/// <param name="PlannedDeloadReleasedWeek">
/// Nedelja u kojoj je planirani deload otpao jer mezociklus nosi samo jedan; null kada
/// planiranog deload-a nije ni bilo ili je već započet.
/// </param>
public sealed record DeloadOutcome(
    int TriggeredByWeek,
    int DeloadWeek,
    decimal FatigueScore,
    int? PlannedDeloadReleasedWeek);
