namespace HKPartsEcommerce.Application.Features.Setup;

/// <summary>Whether first-run setup is complete and whether a super admin exists.</summary>
public class SetupStatusDto
{
    public bool SetupCompleted { get; set; }
    public bool SuperAdminExists { get; set; }
}
