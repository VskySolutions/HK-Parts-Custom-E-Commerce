using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Inquiries;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Commerce;

/// <summary>
/// Inquiries are stored as orders so they reuse lines, addresses, routing and status history — which
/// makes keeping them out of the order and revenue views the load-bearing part of the design. These
/// cover that separation and the pipeline that ends in conversion.
/// </summary>
public class InquiryLifecycleTests : CatalogTestBase
{
    private sealed class StubCommerceMode : ICommerceModeService
    {
        private readonly CommerceModeSettings _settings;
        public StubCommerceMode(CommerceMode mode) =>
            _settings = CommerceModeSettings.Default with { Mode = mode };

        public Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);

        public Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings.IsInquiryOnly);
    }

    private static readonly DateTime Now = new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc);

    private Guid SeedStore()
    {
        using var db = NewContext();
        var store = new Store { Name = "Main", IsEnabled = true, ContactEmail = "store@example.com" };
        db.Stores.Add(store);
        db.SaveChanges();
        return store.Id;
    }

    private Guid SeedInquiry(Guid? storeId = null, InquiryStatus status = InquiryStatus.New, string company = "Acme")
    {
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = $"INQ-{Guid.NewGuid():N}"[..20],
            Status = OrderStatus.Inquiry,
            IsInquiry = true,
            InquiryStatus = status,
            PaymentStatus = PaymentStatus.NotRequired,
            AssignedStoreId = storeId,
            PlacedOnUtc = Now,
            CurrencyCode = "USD",
            Subtotal = 250m,
            TotalAmount = 250m,
            CompanyName = company,
            CustomerNote = "Need 40 units by month end.",
            ShippingAddress = new Address
            {
                FirstName = "Dana",
                LastName = "Reed",
                Email = "dana@acme.test",
                PhoneNumber = "+15550100",
                CountryCode = string.Empty,
                PostalCode = string.Empty,
                AddressLine1 = string.Empty,
                City = string.Empty,
            },
        };
        order.Lines.Add(new OrderLineItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Widget",
            Quantity = 40,
            UnitPrice = 6.25m,
            OriginalUnitPrice = 6.25m,
            LineTotal = 250m,
        });
        db.Orders.Add(order);
        db.SaveChanges();
        return order.Id;
    }

    private Guid SeedPaidOrder()
    {
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Captured,
            PlacedOnUtc = Now,
            CurrencyCode = "USD",
            Subtotal = 50m,
            TotalAmount = 50m,
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order.Id;
    }

    private Guid SeedPaidOrderWithContact(string firstName, string email)
    {
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Captured,
            PlacedOnUtc = Now,
            CurrencyCode = "USD",
            TotalAmount = 50m,
            ShippingAddress = new Address { FirstName = firstName, Email = email },
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order.Id;
    }

    [Fact]
    public async Task Inquiries_never_appear_in_the_order_list()
    {
        SeedInquiry();
        var orderId = SeedPaidOrder();

        using var db = NewContext();
        var page = await new ListOrdersQueryHandler(db)
            .Handle(new ListOrdersQuery(null), CancellationToken.None);

        Assert.Equal(orderId, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Orders_never_appear_in_the_inquiry_list()
    {
        var inquiryId = SeedInquiry();
        SeedPaidOrder();

        using var db = NewContext();
        var page = await new ListInquiriesQueryHandler(db)
            .Handle(new ListInquiriesQuery(), CancellationToken.None);

        Assert.Equal(inquiryId, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Order_search_matches_the_contact_on_the_linked_address()
    {
        // Regression: the contact fields are [NotMapped] read-throughs over Address, so searching them
        // directly is untranslatable and throws rather than returning nothing.
        SeedInquiry();
        var orderId = SeedPaidOrderWithContact("Priya", "priya@example.test");

        using var db = NewContext();
        var page = await new ListOrdersQueryHandler(db)
            .Handle(new ListOrdersQuery(null, Search: "priya@example"), CancellationToken.None);

        Assert.Equal(orderId, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Inquiry_list_filters_on_status_and_search()
    {
        SeedInquiry(status: InquiryStatus.New, company: "Acme");
        SeedInquiry(status: InquiryStatus.Quoted, company: "Globex");

        using var db = NewContext();
        var handler = new ListInquiriesQueryHandler(db);

        var quoted = await handler.Handle(new ListInquiriesQuery(Status: "Quoted"), CancellationToken.None);
        Assert.Equal("Globex", Assert.Single(quoted.Items).CompanyName);

        // "Globex" appears only in CompanyName — both seeds share an @acme.test contact email, so
        // searching "Acme" would legitimately match each of them.
        var searched = await handler.Handle(new ListInquiriesQuery(Search: "Globex"), CancellationToken.None);
        Assert.Equal("Globex", Assert.Single(searched.Items).CompanyName);
    }

    [Fact]
    public async Task Contact_details_survive_a_contact_only_submission()
    {
        // The address row is created even with no postal fields, because ContactName/Email/Phone are
        // [NotMapped] read-throughs over it. Without it the lead would arrive anonymous.
        var id = SeedInquiry();

        using var db = NewContext();
        var dto = await new GetInquiryQueryHandler(db).Handle(new GetInquiryQuery(id), CancellationToken.None);

        Assert.Equal("Dana Reed", dto.ContactName);
        Assert.Equal("dana@acme.test", dto.ContactEmail);
        Assert.Equal("+15550100", dto.ContactPhone);
    }

    [Fact]
    public async Task Status_can_be_advanced_and_notes_recorded()
    {
        var id = SeedInquiry();

        using var db = NewContext();
        var dto = await new UpdateInquiryCommandHandler(db)
            .Handle(new UpdateInquiryCommand(id, "InReview", "Called, awaiting spec."), CancellationToken.None);

        Assert.Equal("InReview", dto.InquiryStatus);
        Assert.Equal("Called, awaiting spec.", dto.InternalNotes);
    }

    [Fact]
    public async Task Converted_cannot_be_set_by_hand()
    {
        // Converting has to create the payable order alongside the status, so the manual path must not be
        // able to leave a record claiming to be converted with no order behind it.
        var id = SeedInquiry();

        using var db = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => new UpdateInquiryCommandHandler(db)
            .Handle(new UpdateInquiryCommand(id, "Converted", null), CancellationToken.None));
    }

    [Fact]
    public async Task Converting_turns_the_inquiry_into_an_order_awaiting_payment()
    {
        var storeId = SeedStore();
        var id = SeedInquiry(storeId, InquiryStatus.Accepted);

        using (var db = NewContext())
        {
            var order = await new ConvertInquiryToOrderCommandHandler(
                    db, new StubCommerceMode(CommerceMode.Standard), new FakeCurrentUser(), new FixedClock(Now))
                .Handle(new ConvertInquiryToOrderCommand(id, 275m, "Agreed on the call."), CancellationToken.None);

            Assert.Equal(OrderStatus.Pending.ToString(), order.Status);
            Assert.Equal(275m, order.TotalAmount);
        }

        using var verify = NewContext();
        var row = verify.Orders.Single(o => o.Id == id);
        Assert.False(row.IsInquiry);
        Assert.Equal(InquiryStatus.Converted, row.InquiryStatus);
        Assert.Equal(PaymentStatus.AwaitingPayment, row.PaymentStatus);
        // Converting in place keeps the whole trail from request to order.
        Assert.Contains(verify.OrderStatusHistory.Where(h => h.OrderId == id),
            h => h.FromStatus == OrderStatus.Inquiry && h.ToStatus == OrderStatus.Pending);
    }

    [Fact]
    public async Task Converted_inquiry_shows_up_as_an_order()
    {
        var storeId = SeedStore();
        var id = SeedInquiry(storeId, InquiryStatus.Accepted);

        using (var db = NewContext())
        {
            await new ConvertInquiryToOrderCommandHandler(
                    db, new StubCommerceMode(CommerceMode.Standard), new FakeCurrentUser(), new FixedClock(Now))
                .Handle(new ConvertInquiryToOrderCommand(id, null, null), CancellationToken.None);
        }

        using var db2 = NewContext();
        var orders = await new ListOrdersQueryHandler(db2).Handle(new ListOrdersQuery(null), CancellationToken.None);
        Assert.Equal(id, Assert.Single(orders.Items).Id);

        var inquiries = await new ListInquiriesQueryHandler(db2)
            .Handle(new ListInquiriesQuery(), CancellationToken.None);
        Assert.Empty(inquiries.Items);
    }

    [Fact]
    public async Task Inquiry_only_tenant_cannot_convert()
    {
        // There is no online order to convert into; the request is closed out offline instead.
        var storeId = SeedStore();
        var id = SeedInquiry(storeId, InquiryStatus.Accepted);

        using var db = NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new ConvertInquiryToOrderCommandHandler(
                db, new StubCommerceMode(CommerceMode.InquiryOnly), new FakeCurrentUser(), new FixedClock(Now))
            .Handle(new ConvertInquiryToOrderCommand(id, null, null), CancellationToken.None));

        Assert.Contains("inquiry-only mode", ex.Message);
    }

    [Fact]
    public async Task Unassigned_inquiry_cannot_be_converted()
    {
        // Nothing would fulfil it, and the existing order screens all assume a store.
        var id = SeedInquiry(storeId: null, status: InquiryStatus.Accepted);

        using var db = NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => new ConvertInquiryToOrderCommandHandler(
                db, new StubCommerceMode(CommerceMode.Standard), new FakeCurrentUser(), new FixedClock(Now))
            .Handle(new ConvertInquiryToOrderCommand(id, null, null), CancellationToken.None));
    }
}
