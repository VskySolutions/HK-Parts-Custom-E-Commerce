using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>
/// Hands out a valid access token per store, refreshing it with the stored refresh token when it is within
/// five minutes of expiry. The refreshed tokens are written with a targeted ExecuteUpdate (value converters
/// still encrypt them), so a refresh never flushes the caller's pending changes.
/// </summary>
public sealed class EbayAuthService : IEbayAuthService
{
    public const string ReconnectMessage = "Reconnect eBay";
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(5);

    private readonly IApplicationDbContext _db;
    private readonly IEbayOAuthClient _oauth;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EbayAuthService> _logger;

    public EbayAuthService(IApplicationDbContext db, IEbayOAuthClient oauth, IDateTimeProvider clock, ILogger<EbayAuthService> logger)
    {
        _db = db;
        _oauth = oauth;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EbaySession> GetSessionAsync(Guid storeId, bool allowDisabled = false, CancellationToken ct = default)
    {
        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null || (string.IsNullOrEmpty(account.RefreshToken) && string.IsNullOrEmpty(account.AccessToken)))
            throw new ServiceUnavailableException("eBay is not connected for this store. Connect eBay in Settings.");
        if (!account.IsEnabled && !allowDisabled)
            throw new ServiceUnavailableException("eBay is disabled for this store. Enable it in eBay Settings.");

        var now = _clock.UtcNow;
        var token = account.AccessToken;
        var needsRefresh = string.IsNullOrEmpty(token)
                           || account.AccessTokenExpiresAtUtc is null
                           || account.AccessTokenExpiresAtUtc.Value - now <= RefreshWindow;

        if (needsRefresh)
        {
            if (string.IsNullOrEmpty(account.RefreshToken)
                || (account.RefreshTokenExpiresAtUtc is DateTime refreshExpiry && refreshExpiry <= now))
                throw new ServiceUnavailableException(ReconnectMessage);

            EbayTokenResult refreshed;
            try
            {
                refreshed = await _oauth.RefreshAsync(account.Environment, account.RefreshToken, ct);
            }
            catch (Exception ex) when (ex is EbayApiException or HttpRequestException or TaskCanceledException or ServiceUnavailableException)
            {
                _logger.LogWarning("eBay token refresh failed for store {StoreId}: {Message}", storeId, ex.Message);
                throw new ServiceUnavailableException(ReconnectMessage);
            }

            token = refreshed.AccessToken;
            await SaveTokensAsync(storeId, refreshed, ct);
        }

        return new EbaySession(storeId, account.Environment, account.MarketplaceId, token!, account.LoggingEnabled);
    }

    private async Task SaveTokensAsync(Guid storeId, EbayTokenResult refreshed, CancellationToken ct)
    {
        if (refreshed.RefreshToken is null)
        {
            await _db.EbayAccounts.Where(a => a.StoreId == storeId).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.AccessToken, refreshed.AccessToken)
                .SetProperty(a => a.AccessTokenExpiresAtUtc, refreshed.AccessTokenExpiresAtUtc), ct);
            return;
        }

        await _db.EbayAccounts.Where(a => a.StoreId == storeId).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.AccessToken, refreshed.AccessToken)
            .SetProperty(a => a.AccessTokenExpiresAtUtc, refreshed.AccessTokenExpiresAtUtc)
            .SetProperty(a => a.RefreshToken, refreshed.RefreshToken)
            .SetProperty(a => a.RefreshTokenExpiresAtUtc, refreshed.RefreshTokenExpiresAtUtc), ct);
    }
}
