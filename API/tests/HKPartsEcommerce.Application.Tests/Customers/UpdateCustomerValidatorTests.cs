using HKPartsEcommerce.Application.Features.Customers;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>Input rules for the admin customer profile edit (no database needed).</summary>
public class UpdateCustomerValidatorTests
{
    private static readonly UpdateCustomerCommandValidator Validator = new();

    [Theory]
    [InlineData("", null, "FirstName")]
    [InlineData("   ", null, "FirstName")]
    [InlineData("Ann", "abc-not-a-phone", "PhoneNumber")]
    [InlineData("Ann", "12", "PhoneNumber")]
    public void Rejects_invalid_input(string first, string? phone, string property)
    {
        var result = Validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), first, null, phone));

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Rejects_empty_id_and_overlong_names()
    {
        var result = Validator.Validate(new UpdateCustomerCommand(Guid.Empty, new string('a', 201), new string('b', 201), null));

        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
        Assert.Contains(result.Errors, e => e.PropertyName == "FirstName");
        Assert.Contains(result.Errors, e => e.PropertyName == "LastName");
    }

    [Theory]
    [InlineData("+44 (20) 7946-0958")]
    [InlineData("555.123.4567")]
    [InlineData("9876543210")]
    public void Accepts_common_phone_formats(string phone)
    {
        Assert.True(Validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "Ann", null, phone)).IsValid);
    }

    [Fact]
    public void Rejects_long_text_over_4000_and_accepts_4000()
    {
        Assert.Contains(Validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "Ann", null, null, new string('x', 4001))).Errors,
            e => e.PropertyName == "LongText");
        Assert.True(Validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "Ann", null, null, new string('x', 4000), true)).IsValid);
    }

    [Fact]
    public void Create_validator_rejects_long_text_over_4000()
    {
        var result = new AdminCreateCustomerCommandValidator().Validate(
            new AdminCreateCustomerCommand("a@b.co", "Passw0rd!x", "Ann", null, null, LongText: new string('x', 4001)));
        Assert.Contains(result.Errors, e => e.PropertyName == "LongText");
    }

    [Fact]
    public void Accepts_blank_optionals()
    {
        Assert.True(Validator.Validate(new UpdateCustomerCommand(Guid.NewGuid(), "Ann", "", "  ")).IsValid);
    }
}
