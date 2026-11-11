using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// Admin customer profile edit (PUT /api/admin/customers/{id}): only first/last name and phone number
/// change; email and every other customer/user field is preserved. Backed by the real SQL Server test DB.
/// </summary>
public class UpdateCustomerTests : CatalogTestBase
{
    private (Guid customerId, string email) SeedCustomer()
    {
        using var db = NewContext();
        var email = $"{Guid.NewGuid():N}@test.local";
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}",
            Email = email,
            PasswordHash = "x",
            EmailVerified = true,
            IsActive = false,
            Customer = new Customer
            {
                FirstName = "Old",
                LastName = "Name",
                PhoneNumber = "111",
                WhatsAppPhoneNumber = "+1 222 333",
                WhatsAppOptIn = true,
                IsTaxExempt = true,
                VatId = "VAT-1",
            },
        };
        db.Users.Add(user);
        db.SaveChanges();
        return (user.Customer!.Id, email);
    }

    private static UpdateCustomerCommandHandler Handler(IApplicationDbContext db) => new(db, new DetailOnlyMediator(db));

    [Fact]
    public async Task Update_changes_profile_fields_and_returns_refreshed_detail()
    {
        var (id, email) = SeedCustomer();

        using var db = NewContext();
        var dto = await Handler(db).Handle(
            new UpdateCustomerCommand(id, "  New ", " Person ", " +44 (20) 7946-0958 "),
            CancellationToken.None);

        Assert.Equal("New", dto.FirstName);
        Assert.Equal("Person", dto.LastName);
        Assert.Equal("+44 (20) 7946-0958", dto.PhoneNumber);
        Assert.Equal(email, dto.Email);
    }

    [Fact]
    public async Task Update_preserves_fields_not_in_the_request()
    {
        var (id, email) = SeedCustomer();

        using (var db = NewContext())
            await Handler(db).Handle(new UpdateCustomerCommand(id, "New", null, null), CancellationToken.None);

        using var check = NewContext();
        var c = check.Customers.Single(x => x.Id == id);
        var u = check.Users.Single(x => x.Id == c.UserId);
        Assert.Equal(string.Empty, c.LastName);
        Assert.Null(c.PhoneNumber);
        // Untouched by the edit:
        Assert.Equal("+1 222 333", c.WhatsAppPhoneNumber);
        Assert.Equal(email, u.Email);
        Assert.False(u.IsActive);
        Assert.True(u.EmailVerified);
        Assert.True(c.WhatsAppOptIn);
        Assert.True(c.IsTaxExempt);
        Assert.Equal("VAT-1", c.VatId);
    }

    [Fact]
    public async Task Update_saves_custom_information_and_null_flag_leaves_it_unchanged()
    {
        var (id, _) = SeedCustomer();

        using (var db = NewContext())
        {
            var dto = await Handler(db).Handle(
                new UpdateCustomerCommand(id, "New", null, null, "  line 1\nline 2  ", true), CancellationToken.None);
            Assert.Equal("line 1\nline 2", dto.LongText);
            Assert.True(dto.AllowRestrictedCapacityPurchases);
        }

        using (var db = NewContext())
            await Handler(db).Handle(new UpdateCustomerCommand(id, "New", null, null, "   ", null), CancellationToken.None);

        using var check = NewContext();
        var c = check.Customers.Single(x => x.Id == id);
        Assert.Null(c.LongText);                       // blank clears
        Assert.True(c.AllowRestrictedCapacityPurchases); // not sent -> unchanged
    }

    [Fact]
    public async Task Update_unknown_customer_throws_not_found()
    {
        using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(new UpdateCustomerCommand(Guid.NewGuid(), "A", null, null), CancellationToken.None));
    }

    /// <summary>Routes the handler's follow-up <see cref="GetCustomerDetailQuery"/> to the real query handler.</summary>
    private sealed class DetailOnlyMediator : IMediator
    {
        private readonly IApplicationDbContext _db;
        public DetailOnlyMediator(IApplicationDbContext db) => _db = db;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetCustomerDetailQuery q)
                return (TResponse)(object)await new GetCustomerDetailQueryHandler(_db).Handle(q, cancellationToken);
            throw new NotSupportedException($"Unexpected request {request.GetType().Name}");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
