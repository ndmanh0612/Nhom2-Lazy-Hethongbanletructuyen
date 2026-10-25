using FluentAssertions;
using PhoneStore.BuildingBlocks.Domain;
using Xunit;

namespace PhoneStore.UnitTests;

public class ResultTests
{
    [Fact]
    public void SuccessResult_ShouldHaveIsSuccessTrue()
    {
        var result = Result.Success("PhoneStore");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("PhoneStore");
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void FailureResult_ShouldContainError()
    {
        var error = Error.NotFound("PRODUCT_NOT_FOUND", "Không tìm thấy sản phẩm");
        var result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_FOUND");
    }
}
