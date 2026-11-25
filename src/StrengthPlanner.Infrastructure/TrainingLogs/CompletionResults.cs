using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Mesocycles;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// The answer to finishing a workout, and to saving a correction of one: both report the same
/// things, so both are built here.
/// </summary>
public static class CompletionResults
{
    public static CompleteSessionResultDto Build(
        WorkoutSession session,
        SessionOutcome outcome,
        DeloadOutcome? autoDeload,
        IReadOnlyList<SetAdjustment> volumeAdjustments,
        MacrocycleAdvance? nextBlock)
    {
        return new CompleteSessionResultDto
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Exercises = outcome.Summaries,
            // Prijavljuje se samo tekuća nedelja. Balansiranje dodiruje i one koje tek
            // dolaze (granice volumena su se možda pomerile ovim treningom), ali posledica
            // OVOG treninga koju korisnik može da vidi na svom planu je ono što mu preostaje
            // u ovoj nedelji.
            VolumeAdjustments = volumeAdjustments
                .Where(adjustment => adjustment.WeekNumber == session.TrainingWeek.WeekNumber)
                .Select(adjustment => new SetAdjustmentDto
                {
                    SessionId = adjustment.SessionId,
                    DayLabel = adjustment.DayLabel,
                    ExerciseName = adjustment.ExerciseName,
                    FromSets = adjustment.FromSets,
                    ToSets = adjustment.ToSets,
                    Muscle = adjustment.Muscle,
                    Reason = adjustment.Reason?.ToString()
                })
                .ToList(),
            AutoDeload = autoDeload is null
                ? null
                : new AutoDeloadDto
                {
                    TriggeredByWeek = autoDeload.TriggeredByWeek,
                    DeloadWeek = autoDeload.DeloadWeek,
                    FatigueScore = autoDeload.FatigueScore,
                    PlannedDeloadReleasedWeek = autoDeload.PlannedDeloadReleasedWeek
                },
            NextBlock = nextBlock is null
                ? null
                : new MacrocycleAdvanceDto
                {
                    PlanName = nextBlock.PlanName,
                    BlockOrder = nextBlock.BlockOrder,
                    BlockCount = nextBlock.BlockCount,
                    Goal = nextBlock.Goal.ToString(),
                    MesocycleId = nextBlock.MesocycleId,
                    MesocycleName = nextBlock.MesocycleName
                }
        };
    }
}
