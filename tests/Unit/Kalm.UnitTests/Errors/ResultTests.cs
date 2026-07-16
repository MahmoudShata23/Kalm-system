using Kalm.SharedKernel.Errors;

namespace Kalm.UnitTests.Errors;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResultWithoutError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(AppError.None, result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithError()
    {
        var error = new AppError("foundation.example", "Example failure.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }
}
