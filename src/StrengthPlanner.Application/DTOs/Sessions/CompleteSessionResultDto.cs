namespace StrengthPlanner.Application.DTOs.Sessions;

public class CompleteSessionResultDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<CompletedExerciseSummaryDto> Exercises { get; set; } = new();
}
