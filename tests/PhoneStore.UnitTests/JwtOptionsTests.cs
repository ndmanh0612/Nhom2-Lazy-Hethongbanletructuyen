using System.Security.Cryptography;
using FluentAssertions;
using PhoneStore.BuildingBlocks.Common;
using Xunit;

namespace PhoneStore.UnitTests;

public class JwtOptionsTests
{
    [Fact]
    public void Missing_signing_key_is_rejected_by_default()
    {
        var action = () => new JwtOptions().ResolveSigningKey();
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Temporary_development_key_is_shared_within_the_process()
    {
        var first = new JwtOptions { AllowDevelopmentSigningKey = true }.ResolveSigningKey();
        var second = new JwtOptions { AllowDevelopmentSigningKey = true }.ResolveSigningKey();
        first.Should().Be(second);
        first.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public void Explicit_short_key_does_not_silently_fall_back_to_development_key()
    {
        var options = new JwtOptions { SigningKey = "too-short", AllowDevelopmentSigningKey = true };
        var action = () => options.ResolveSigningKey();
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Configured_key_is_used()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        new JwtOptions { SigningKey = key }.ResolveSigningKey().Should().Be(key);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(30, 0)]
    [InlineData(-1, 7)]
    public void Non_positive_token_lifetimes_are_rejected(int minutes, int days)
    {
        var options = new JwtOptions
        {
            AccessTokenMinutes = minutes, RefreshTokenDays = days, AllowDevelopmentSigningKey = true
        };
        var action = () => options.ResolveSigningKey();
        action.Should().Throw<InvalidOperationException>();
    }
}
