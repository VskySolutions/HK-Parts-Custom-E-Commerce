namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>Outcome of a credential/storage connectivity test. Never carries credential values.</summary>
public record ConnectivityTestResult(bool Success, string Message, DateTime TestedAtUtc);
