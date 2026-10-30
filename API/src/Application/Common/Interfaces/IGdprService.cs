namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// GDPR data-subject operations for a customer (WO-23, REQ-CUS): the right to data portability
/// (<see cref="ExportCustomerDataAsync"/>) and the right to erasure (<see cref="AnonymizeCustomerAsync"/>).
/// Erasure anonymises personal data in place while preserving order/accounting records, so financial
/// history stays intact and no <see cref="HKPartsEcommerce.Domain.Entities.Order"/> rows are removed.
/// </summary>
public interface IGdprService
{
    /// <summary>
    /// Gathers <em>all</em> personal data held for the customer — profile, login email, phone, address
    /// book, order summaries (including line items), product reviews, product Q&amp;A and newsletter status
    /// — into a single indented JSON document and returns it as UTF-8 bytes ready for download.
    /// Throws <see cref="Exceptions.NotFoundException"/> when the customer does not exist.
    /// </summary>
    Task<byte[]> ExportCustomerDataAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymises the customer's personal data in place: blanks the profile name/phone, replaces the linked
    /// login's email with a non-reversible placeholder and disables it, and redacts the personal fields of
    /// their address book — while keeping order records and their accounting-relevant geography. The whole
    /// operation is committed in a single <c>SaveChanges</c>. Throws
    /// <see cref="Exceptions.NotFoundException"/> when the customer does not exist.
    /// </summary>
    Task AnonymizeCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
