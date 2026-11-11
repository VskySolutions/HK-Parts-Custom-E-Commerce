namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// Kind of identity / licensing document an admin keeps on file for a customer (Customer → Documents tab).
/// Stored as int — existing values must never be renumbered (saved documents reference them); add new ones at the end.
/// Display labels and dropdown order live in <c>Application.Features.CustomerDocuments.CustomerDocumentTypes</c>.
/// </summary>
public enum CustomerDocumentType
{
    FirearmLicensePermit = 1,
    MilitaryId = 2,
    LawEnforcementId = 3,
    DriversLicense = 4,
    GovernmentIssuedPhotoId = 5,
    FirearmTrainingCertificate = 6,
    LicenseCopy = 7,
    AddressProof = 8,
    AdditionalSupportingDocuments = 9,
}
