using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// One logged set as the client sees it.
///
/// Three services returned this shape and each built it by hand, which is a defect waiting
/// for the next column: adding <see cref="SetLog.BodyweightLoadKg"/> to two of the three
/// compiled perfectly and shipped a set that read "0 kg" the moment it was logged and
/// "TM × 12" after a reload. An object initializer does not have to be complete, so nothing
/// but a browser could notice.
/// </summary>
public static class SetLogMapper
{
    public static SetLogDto ToDto(SetLog setLog)
    {
        ArgumentNullException.ThrowIfNull(setLog);

        return new SetLogDto
        {
            Id = setLog.Id,
            ExercisePlanId = setLog.ExercisePlanId,
            SetNumber = setLog.SetNumber,
            WeightKg = setLog.WeightKg,
            Reps = setLog.Reps,
            Rir = setLog.Rir,
            IsFailure = setLog.IsFailure,
            BodyweightLoadKg = setLog.BodyweightLoadKg,
            PerformedAt = setLog.PerformedAt
        };
    }
}
