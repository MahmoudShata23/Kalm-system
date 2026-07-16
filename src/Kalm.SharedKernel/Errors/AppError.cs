namespace Kalm.SharedKernel.Errors;

public sealed record AppError(string Code, string Message)
{
    public static AppError None { get; } = new("none", string.Empty);
}
