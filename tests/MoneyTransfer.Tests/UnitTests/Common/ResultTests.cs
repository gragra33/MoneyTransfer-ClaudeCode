using MoneyTransfer.Common;
using MoneyTransfer.Tests.Fixtures;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Common;

/// <summary>Tests for the <see cref="Result"/> and <see cref="Result{T}"/> types.</summary>
[Trait("Category", "Unit")]
public class ResultTests
{
    #region Result (non-generic)

    [Fact]
    public void Ok_ReturnsSuccessfulResult()
    {
        // Act
        var result = Result.Ok();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Fail_WithMessage_ReturnsFailedResultWithMessage()
    {
        // Arrange
        const string message = "Something went wrong.";

        // Act
        var result = Result.Fail(message);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(message);
    }

    [Fact]
    public void Fail_WithNullMessage_StoresNullError()
    {
        // Act
        var result = Result.Fail(null!);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    #endregion

    #region Result<T>

    [Fact]
    public void Ok_Generic_ReturnsSuccessfulResultWithValue()
    {
        // Arrange
        const int value = 42;

        // Act
        var result = Result.Ok(value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(value);
        result.Error.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Fail_Generic_ReturnsFailedResultWithMessage()
    {
        // Arrange
        const string message = "Not found.";

        // Act
        var result = Result.Fail<int>(message);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(message);
        result.Value.ShouldBe(default);
    }

    [Fact]
    public void Ok_Generic_WithNullValue_IsSuccessful()
    {
        // Act
        var result = Result.Ok<string?>(null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    #endregion

    #region TestHelpers extensions

    [Fact]
    public void ShouldSucceed_Extension_PassesForOkResult()
    {
        // Arrange
        var result = Result.Ok();

        // Act / Assert — must not throw
        Should.NotThrow(() => result.ShouldSucceed());
    }

    [Fact]
    public void ShouldFail_Extension_PassesForFailResult()
    {
        // Arrange
        var result = Result.Fail("error");

        // Act / Assert — must not throw
        Should.NotThrow(() => result.ShouldFail("error"));
    }

    #endregion
}
