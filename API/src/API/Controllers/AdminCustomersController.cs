using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Features.CustomerGroups;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin customer management: list/detail, profile edit, activation, tax exemption (REQ-TAX-003) and pricing-group assignment (REQ-CUS-003).</summary>
/// <remarks>Reads need module access only; every write endpoint additionally requires <c>Users.Write</c> ([RequirePermission]).</remarks>
[Route("api/admin/customers")]
[RequireModule(Modules.Customers)]
public class AdminCustomersController : ApiControllerBase
{
    /// <summary>List customers (paged), optionally filtered by name/email, email-verified, active and/or tax-exempt state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CustomerListItemDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] bool? emailVerified = null, [FromQuery] bool? isActive = null, [FromQuery] bool? isTaxExempt = null,
        [FromQuery] Guid? customerGroupId = null, [FromQuery] bool? hasCustomerGroup = null,
        [FromQuery] DateTime? registeredFromUtc = null, [FromQuery] DateTime? registeredToUtc = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListCustomersQuery(
            page, pageSize, search, emailVerified, isActive, isTaxExempt,
            customerGroupId, hasCustomerGroup, registeredFromUtc, registeredToUtc, sortBy, sortDescending)));

    /// <summary>
    /// Block several customers at once. Body: { customerIds, allMatching, filter, reason, internalNotes } —
    /// either the ticked ids, or (allMatching = true) every customer the list shows for <c>filter</c>.
    /// Already-blocked, staff and unknown ids are skipped. Returns { blocked, skipped }. Requires <c>Customers.Blacklist</c>.
    /// </summary>
    [HttpPost("blacklist/bulk")]
    [RequirePermission(Permissions.CustomersBlacklist)]
    public async Task<ActionResult<BulkBlacklistCustomersResult>> BulkBlacklist([FromBody] BulkBlacklistCustomersCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Guest customers (guest checkout §4.2): one row per normalized guest email, newest first, 25 per page.
    /// Search matches email, name, phone or order number; status = All / Active / Blocked; account = All / GuestOnly /
    /// Registered (guests who later registered stay listed).
    /// </summary>
    [HttpGet("guests")]
    public async Task<ActionResult<PaginatedList<GuestCustomerDto>>> ListGuests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null,
        [FromQuery] GuestStatusFilter status = GuestStatusFilter.All, [FromQuery] GuestAccountFilter account = GuestAccountFilter.All)
        => Ok(await Mediator.Send(new ListGuestCustomersQuery(page, pageSize, search, status, account)));

    /// <summary>A-17: the filtered guest list as a download: <c>format=csv</c> (default) or <c>format=xlsx</c> (Excel).</summary>
    [HttpGet("guests/export")]
    public async Task<IActionResult> ExportGuests(
        [FromQuery] string? search = null, [FromQuery] GuestStatusFilter status = GuestStatusFilter.All,
        [FromQuery] GuestAccountFilter account = GuestAccountFilter.All, [FromQuery] GuestExportFormat format = GuestExportFormat.Csv)
    {
        var bytes = await Mediator.Send(new ExportGuestCustomersQuery(search, status, account, format));
        var name = $"guest-customers-{DateTime.UtcNow:yyyyMMdd}";
        return format == GuestExportFormat.Xlsx
            ? File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx")
            : File(bytes, "text/csv", name + ".csv");
    }

    /// <summary>
    /// N-05: anonymise a guest's personal data on request (non-firearm orders; firearm orders are kept as the law
    /// requires). Returns how many orders were anonymised and how many firearm orders were kept.
    /// </summary>
    [HttpPost("guests/{guestId:guid}/anonymize")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<AnonymizeGuestResult>> AnonymizeGuest(Guid guestId)
        => Ok(await Mediator.Send(new AnonymizeGuestCommand(guestId)));

    /// <summary>A-14 / A-15: a guest's orders by opaque guest id (never the email in the URL), newest first.</summary>
    [HttpGet("guests/{guestId:guid}/orders")]
    public async Task<ActionResult<List<GuestOrderDto>>> ListGuestOrders(Guid guestId)
        => Ok(await Mediator.Send(new ListGuestCustomerOrdersQuery(guestId)));

    /// <summary>A-18: move the guest's orders onto the account registered with the same email.</summary>
    [HttpPost("guests/{guestId:guid}/link")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<LinkGuestOrdersResult>> LinkGuestOrders(Guid guestId)
        => Ok(await Mediator.Send(new LinkGuestOrdersCommand(guestId)));

    /// <summary>Admin creates a customer (identity + profile) and returns the new customer id. Email is marked verified or a verification email is sent, per the request.</summary>
    [HttpPost]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<Guid>> Create([FromBody] AdminCreateCustomerCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Get a single customer's full detail: profile, tax-exemption, roles, addresses and order history.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCustomerDetailQuery(id)));

    /// <summary>
    /// Update a customer's profile (first/last name, phone and custom information) and return the refreshed detail.
    /// Email is read-only and cannot be changed here. Requires <c>Users.Write</c>.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<CustomerDetailDto>> Update(Guid id, [FromBody] UpdateCustomerCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>
    /// Soft-delete a customer: the record is kept but hidden from customer lists/detail and the login is
    /// deactivated. Orders and other history are preserved. 404 if unknown, 409 if already deleted or a staff account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCustomerCommand(id));
        return NoContent();
    }

    /// <summary>
    /// Blacklist (block) a customer. Body: { reason, internalNotes }. Sets Customers.IsBlacklisted and appends a
    /// Blocked row to the blacklist history; the rest of the customer record is untouched, but the customer no
    /// longer appears in the Customers list. Returns the new history row id. 400 invalid, 404 unknown customer,
    /// 409 already blacklisted or a staff account. Requires <c>Customers.Blacklist</c>.
    /// </summary>
    [HttpPost("{id:guid}/blacklist")]
    [RequirePermission(Permissions.CustomersBlacklist)]
    public async Task<ActionResult<Guid>> Blacklist(Guid id, [FromBody] BlacklistCustomerCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>
    /// Reset a customer's password (admin). Body: { newPassword, confirmPassword }. The password is validated
    /// against the shared policy, stored hashed, and the customer's sessions are revoked. 204 on success;
    /// 400 invalid/mismatch, 404 unknown customer, 409 staff account. Requires <c>Users.Write</c>.
    /// </summary>
    [HttpPut("{id:guid}/password")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetCustomerPasswordCommand command)
    {
        await Mediator.Send(command with { CustomerId = id });
        return NoContent();
    }

    /// <summary>
    /// The customer's wishlist items (active wishlists only), newest first and paged — read-only Wishlist tab.
    /// 404 if the customer is unknown; an empty page when they have no wishlist or no items.
    /// </summary>
    [HttpGet("{id:guid}/wishlist")]
    public async Task<ActionResult<PaginatedList<CustomerWishlistItemDto>>> GetWishlist(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => Ok(await Mediator.Send(new GetCustomerWishlistItemsQuery(id, page, pageSize)));

    /// <summary>Get a customer's tax-exemption configuration.</summary>
    [HttpGet("{id:guid}/tax-exemption")]
    public async Task<ActionResult<CustomerTaxExemptionDto>> GetTaxExemption(Guid id)
        => Ok(await Mediator.Send(new GetCustomerTaxExemptionQuery(id)));

    /// <summary>Set (or clear) a customer's tax-exemption flag and supporting certificate / VAT id.</summary>
    [HttpPut("{id:guid}/tax-exemption")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<CustomerTaxExemptionDto>> SetTaxExemption(Guid id, [FromBody] SetCustomerTaxExemptionCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>Get the customer's store-credit balance and ledger (WO-48).</summary>
    [HttpGet("{id:guid}/store-credit")]
    public async Task<ActionResult<StoreCreditDto>> GetStoreCredit(Guid id)
        => Ok(await Mediator.Send(new GetCustomerStoreCreditQuery(id)));

    /// <summary>Manually grant store credit to the customer (WO-48).</summary>
    [HttpPost("{id:guid}/store-credit")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<StoreCreditDto>> IssueStoreCredit(Guid id, [FromBody] IssueStoreCreditCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>Get the customer's assigned pricing group, or null when they are on base pricing.</summary>
    [HttpGet("{id:guid}/group")]
    public async Task<ActionResult<CustomerGroupBriefDto?>> GetGroup(Guid id)
        => Ok(await Mediator.Send(new GetCustomerGroupForCustomerQuery(id)));

    /// <summary>
    /// Assign the customer's single pricing group, replacing any previous one (AC-CUS-003.2).
    /// Send customerGroupId = null to clear it and revert the customer to base pricing.
    /// </summary>
    [HttpPut("{id:guid}/group")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<CustomerGroupBriefDto?>> SetGroup(Guid id, [FromBody] AssignCustomerGroupCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>Activate or deactivate the customer's login (User.IsActive) — WO-117.</summary>
    [HttpPut("{id:guid}/active")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<CustomerDetailDto>> SetActive(Guid id, [FromBody] SetCustomerActiveCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>Mark the customer's email verified / unverified (User.EmailVerified). Unverified customers cannot sign in.</summary>
    [HttpPut("{id:guid}/email-verified")]
    [RequirePermission(Permissions.UsersWrite)]
    public async Task<ActionResult<CustomerDetailDto>> SetEmailVerified(Guid id, [FromBody] SetCustomerEmailVerifiedCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));

    /// <summary>Exempt the customer from the restricted-state shipping rule (SR-92), or remove the exemption.</summary>
    [HttpPut("{id:guid}/shipping-restriction-exempt")]
    public async Task<ActionResult<CustomerDetailDto>> SetShippingRestrictionExempt(
        Guid id, [FromBody] SetCustomerShippingRestrictionExemptCommand command)
        => Ok(await Mediator.Send(command with { CustomerId = id }));
}
