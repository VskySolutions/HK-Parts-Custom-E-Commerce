namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Raises operational alerts for admins to triage (WO-75 AC-TEN-003.5).</summary>
public interface IAdminAlertService
{
    Task RaiseAsync(string alertType, string title, string? message = null, string severity = "Warning", string? source = null, CancellationToken cancellationToken = default);
}
