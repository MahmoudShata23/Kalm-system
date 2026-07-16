namespace Kalm.SharedKernel.Errors;

public class Result
{
    protected Result(bool isSuccess, AppError error)
    {
        if (isSuccess && error != AppError.None)
        {
            throw new InvalidOperationException("Successful results cannot include an error.");
        }

        if (!isSuccess && error == AppError.None)
        {
            throw new InvalidOperationException("Failed results must include an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public AppError Error { get; }

    public static Result Success()
    {
        return new Result(true, AppError.None);
    }

    public static Result Failure(AppError error)
    {
        return new Result(false, error);
    }
}
