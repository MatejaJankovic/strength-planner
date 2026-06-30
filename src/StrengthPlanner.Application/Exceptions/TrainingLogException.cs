namespace StrengthPlanner.Application.Exceptions;

public class TrainingLogException : Exception
{
    public TrainingLogErrorType ErrorType { get; }

    public TrainingLogException(TrainingLogErrorType errorType, string message) : base(message)
    {
        ErrorType = errorType;
    }
}
