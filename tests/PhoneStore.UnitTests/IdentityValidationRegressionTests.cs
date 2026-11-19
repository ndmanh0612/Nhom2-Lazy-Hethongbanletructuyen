using FluentAssertions;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Validators;
using PhoneStore.Modules.Identity.Domain.Entities;
using Xunit;

namespace PhoneStore.UnitTests;

public class IdentityValidationRegressionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Undefined_roles_cannot_be_created_or_assigned(int value)
    {
        var create = () => User.CreateWithRole("a@example.com", "hash", "Name", (UserRole)value);
        create.Should().Throw<ValidationException>();
        var staff = User.CreateStaff("a@example.com", "hash", "Name");
        var change = () => staff.ChangeRole((UserRole)value);
        change.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Staff_role_change_does_not_promote_a_customer()
    {
        var customer = User.CreateCustomer("a@example.com", "hash", "Name");
        var change = () => customer.ChangeRole(UserRole.ADMIN);
        change.Should().Throw<ValidationException>();
        customer.Role.Should().Be(UserRole.CUSTOMER);
    }

    [Fact]
    public void Blank_optional_phone_is_stored_as_null()
    {
        var user = User.CreateCustomer("a@example.com", "hash", "Name");
        user.UpdateProfile("New", "   ");
        user.Phone.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("99")]
    [InlineData("CUSTOMER")]
    public void Invalid_staff_roles_return_validation_failures(string? role)
    {
        new CreateStaffRequestValidator().Validate(
            new CreateStaffRequest("a@example.com", "Staff@123", "Name", role!, null))
            .IsValid.Should().BeFalse();
        new ChangeRoleRequestValidator().Validate(new ChangeRoleRequest(role!))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Staff_pagination_rejects_integer_overflow()
    {
        new StaffFilterQueryValidator().Validate(
            new StaffFilterQuery(null, null, null, int.MaxValue, 100))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Passwords_are_limited_by_utf8_bytes_on_every_password_endpoint()
    {
        var password = new string('é', 40) + "A1";
        new RegisterRequestValidator().Validate(new RegisterRequest("a@example.com", password, "Name", null)).IsValid.Should().BeFalse();
        new LoginRequestValidator().Validate(new LoginRequest("a@example.com", password)).IsValid.Should().BeFalse();
        new CreateStaffRequestValidator().Validate(new CreateStaffRequest("a@example.com", password, "Name", "STAFF", null)).IsValid.Should().BeFalse();
        new ChangePasswordRequestValidator().Validate(new ChangePasswordRequest("Old@1234", password)).IsValid.Should().BeFalse();
        new ResetPasswordRequestValidator().Validate(new ResetPasswordRequest(password)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Password_at_exactly_72_utf8_bytes_is_accepted()
    {
        var password = new string('é', 35) + "A1";
        new RegisterRequestValidator().Validate(new RegisterRequest("a@example.com", password, "Name", null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Address_updates_and_lock_reasons_have_bounded_input()
    {
        new UpdateAddressRequestValidator().Validate(new UpdateAddressRequest("", "bad", new string('x', 501))).IsValid.Should().BeFalse();
        new LockAccountRequestValidator().Validate(new LockAccountRequest(true, new string('x', 501))).IsValid.Should().BeFalse();
    }
}
