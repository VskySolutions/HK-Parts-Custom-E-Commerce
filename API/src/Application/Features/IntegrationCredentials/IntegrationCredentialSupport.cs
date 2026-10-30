using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

/// <summary>Summary row shown in the admin list — never carries secret fields.</summary>
public class IntegrationCredentialListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }

    /// <summary>Transaction/processing fee (% of order total) added at checkout for payment gateways; null when unset.</summary>
    public decimal? TransactionFeePercent { get; set; }

    /// <summary>
    /// The endpoint this row points at. Null for the integrations with no endpoint to choose (Stripe,
    /// Twilio, Azure Blob). With a sandbox and a live row per integration, the URL is what tells them
    /// apart at a glance — the Environment flag says which it claims to be, this says where it goes.
    /// </summary>
    public string? BaseUrl { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static IntegrationCredentialListItemDto From(IntegrationCredentialBase e, string? baseUrl = null) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Active = e.Active,
        IsProduction = e.IsProduction,
        TransactionFeePercent = e.TransactionFeePercent,
        BaseUrl = baseUrl,
        CreatedOnUtc = e.CreatedOnUtc,
        UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

/// <summary>
/// Shared CRUD plumbing for the homogeneous per-integration credential tables, keeping each provider slice
/// down to its typed fields. Soft-delete and audit stamping happen in <c>AppDbContext</c>.
/// </summary>
internal static class IntegrationCredentialSupport
{
    /// <summary>Trims a field, collapsing blank input to null.</summary>
    public static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// An integration's API host is configuration, not a constant compiled into an adapter: sandbox and live
    /// live at different hosts, and only the admin knows which account a credential belongs to. Blank passes
    /// so <c>NotEmpty()</c> owns the required message rather than both rules firing at once.
    /// </summary>
    public static bool BeAnAbsoluteHttpUrl(string? value)
        => string.IsNullOrWhiteSpace(value)
           || (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    /// <summary>
    /// Active row(s) first, then alphabetical — the shape the admin grid renders. <paramref name="baseUrl"/>
    /// projects the endpoint column; it is per-entity rather than on the base type because the integrations
    /// with no endpoint to choose should not carry a column they can never fill.
    /// </summary>
    public static async Task<IReadOnlyList<IntegrationCredentialListItemDto>> ListAsync<T>(
        DbSet<T> set, CancellationToken ct, Func<T, string?>? baseUrl = null) where T : IntegrationCredentialBase
    {
        var rows = await set.AsNoTracking()
            .OrderByDescending(x => x.Active)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
        return rows.Select(r => IntegrationCredentialListItemDto.From(r, baseUrl?.Invoke(r))).ToList();
    }

    public static async Task<T> GetAsync<T>(DbSet<T> set, Guid id, CancellationToken ct)
        where T : IntegrationCredentialBase
        => await set.FirstOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new NotFoundException(typeof(T).Name, id);

    public static async Task DeleteAsync<T>(
        DbSet<T> set, IApplicationDbContext db, Guid id, CancellationToken ct) where T : IntegrationCredentialBase
    {
        var entity = await set.FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new NotFoundException(typeof(T).Name, id);
        set.Remove(entity); // converted to a soft-delete on save by AppDbContext
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sets the Active flag on one row (the admin grid's quick toggle). Activating a row deactivates every
    /// other row of the same integration, keeping at most one active — the row the runtime resolver reads.
    /// </summary>
    public static async Task SetActiveAsync<T>(
        DbSet<T> set, IApplicationDbContext db, Guid id, bool active, CancellationToken ct) where T : IntegrationCredentialBase
    {
        var entity = await set.FirstOrDefaultAsync(x => x.Id == id, ct)
                     ?? throw new NotFoundException(typeof(T).Name, id);
        entity.Active = active;

        if (active)
        {
            var others = await set.Where(x => x.Active && x.Id != id).ToListAsync(ct);
            foreach (var other in others)
                other.Active = false;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads (by id) or creates the row and applies the common fields. When the row is being set Active,
    /// every other row of the same integration is deactivated so at most one stays active — that is the
    /// row the runtime resolver reads. Returns the tracked entity; the caller sets its typed fields and
    /// then saves.
    /// </summary>
    public static async Task<T> UpsertAsync<T>(
        DbSet<T> set, Guid? id, string name, bool active, bool isProduction, CancellationToken ct)
        where T : IntegrationCredentialBase, new()
    {
        T entity;
        if (id is { } existingId)
        {
            entity = await set.FirstOrDefaultAsync(x => x.Id == existingId, ct)
                     ?? throw new NotFoundException(typeof(T).Name, existingId);
        }
        else
        {
            entity = new T();
            set.Add(entity);
        }

        entity.Name = name.Trim();
        entity.Active = active;
        entity.IsProduction = isProduction;

        if (active)
        {
            var others = await set.Where(x => x.Active && x.Id != entity.Id).ToListAsync(ct);
            foreach (var other in others)
                other.Active = false;
        }

        return entity;
    }
}
