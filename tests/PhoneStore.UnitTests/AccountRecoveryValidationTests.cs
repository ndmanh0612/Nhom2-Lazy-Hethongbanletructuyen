using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Application.Validators;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Services;
using Xunit;

namespace PhoneStore.UnitTests;

public sealed class AccountRecoveryValidationTests
{
    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("+84912345678", "+84912345678")]
    public void Sms_phone_is_normalized(string input, string expected) => AccountRecoveryService.NormalizePhone(input).Should().Be(expected);

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("not-a-phone")] [InlineData("0912")]
    public void Invalid_sms_phone_is_rejected(string? input) => ((Action)(() => AccountRecoveryService.NormalizePhone(input))).Should().Throw<ValidationException>();

    [Fact]
    public void Changing_phone_removes_verified_status_but_name_change_preserves_it()
    {
        var user = User.CreateCustomer("test@example.com", "hash", "Name", "0912345678");
        user.VerifyPhone("+84912345678");
        user.UpdateProfile("Changed", "0912345678"); user.VerifiedPhone.Should().NotBeNull();
        user.UpdateProfile("Changed", "0987654321"); user.VerifiedPhone.Should().BeNull();
    }

    [Fact]
    public void Challenges_expire_lock_out_and_cannot_reverify_after_success_or_consumption()
    {
        var now = DateTimeOffset.UtcNow;
        var row = VerificationChallenge.Create(Guid.NewGuid(), 1, "password-reset", "email", "a@example.com", "hash", now);
        row.CanVerify(now.AddMinutes(5)).Should().BeFalse();
        row.CanVerify(now).Should().BeTrue();
        for (var i = 0; i < 5; i++) row.FailAttempt();
        row.CanVerify(now).Should().BeFalse();
        var verified = VerificationChallenge.Create(Guid.NewGuid(), 1, "password-reset", "email", "a@example.com", "hash", now);
        verified.Verify("token-hash", now); verified.CanVerify(now).Should().BeFalse();
        verified.Consume(now); verified.ConsumedAt.Should().Be(now);
    }

    [Theory]
    [InlineData("")] [InlineData("12345678")] [InlineData("abcdefgh")] [InlineData("short1")]
    public void Reset_rejects_empty_or_weak_passwords(string password) => new CompleteRecoveryRequestValidator()
        .Validate(new CompleteRecoveryRequest(Guid.NewGuid(), new string('A', 64), password)).IsValid.Should().BeFalse();

    [Fact]
    public void Reset_rejects_multibyte_password_over_bcrypt_limit() => new CompleteRecoveryRequestValidator()
        .Validate(new CompleteRecoveryRequest(Guid.NewGuid(), new string('A', 64), new string('ấ', 25) + "1")).IsValid.Should().BeFalse();

    [Fact]
    public void Query_filters_reject_unbounded_or_unapproved_fields()
    {
        var validator = new CustomerFilterQueryValidator();
        validator.Validate(new CustomerFilterQuery(null, null, "passwordHash")).IsValid.Should().BeFalse();
        validator.Validate(new CustomerFilterQuery(null, null, Page: int.MaxValue, PageSize: 100)).IsValid.Should().BeFalse();
        validator.Validate(new CustomerFilterQuery(null, null, PageSize: 101)).IsValid.Should().BeFalse();
        validator.Validate(new CustomerFilterQuery(null, null)).IsValid.Should().BeTrue();
        new AuditFilterQueryValidator().Validate(new AuditFilterQuery(null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Unconfigured_delivery_never_reports_success()
    {
        var service = new RecoveryDelivery(Options.Create(new RecoveryOptions()), new ClientFactory(new Handler(HttpStatusCode.Created, "{}")));
        var send = () => service.SendAsync("email", "a@example.com", "123456", default);
        var error = await send.Should().ThrowAsync<AppException>();
        error.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        error.Which.Code.Should().Be("RECOVERY_NOT_CONFIGURED");
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, "{\"status\":\"queued\"}", true)]
    [InlineData(HttpStatusCode.Created, "{\"status\":\"failed\"}", false)]
    [InlineData(HttpStatusCode.Created, "{}", false)]
    [InlineData(HttpStatusCode.Unauthorized, "private-provider-response", false)]
    [InlineData(HttpStatusCode.TemporaryRedirect, "", false)]
    public async Task Sms_reports_only_provider_acceptance_and_sanitizes_failures(HttpStatusCode status, string body, bool success)
    {
        var handler = new Handler(status, body);
        var options = new RecoveryOptions { Sms = new() { AccountSid = "AC" + new string('a', 32), AuthToken = "test-only-token", From = "+12025550123" } };
        var service = new RecoveryDelivery(Options.Create(options), new ClientFactory(handler));
        var action = () => service.SendAsync("sms", "+84912345678", "123456", default);
        if (success) await action.Should().NotThrowAsync();
        else
        {
            var error = await action.Should().ThrowAsync<AppException>();
            error.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            error.Which.Message.Should().NotContain(body == "" ? "test-only-token" : body);
        }
        handler.Path.Should().Contain("https://api.twilio.com/2010-04-01/Accounts/");
        handler.RequestBody.Should().Contain("To=%2B84912345678").And.Contain("123456");
    }
    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Path;
        public string? RequestBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsoluteUri;
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
