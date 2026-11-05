using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ebay;

namespace HKPartsEcommerce.Infrastructure.Ebay;

public static class EbayServiceCollectionExtensions
{
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(30);
    /// <summary>The full category tree is tens of MB; it alone gets a longer attempt timeout.</summary>
    private static readonly TimeSpan CategoryTreeTimeout = TimeSpan.FromMinutes(3);

    public static IServiceCollection AddEbay(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EbayOptions>(configuration.GetSection(EbayOptions.SectionName));

        services.AddHttpClient<IEbayOAuthClient, EbayOAuthClient>()
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddResilienceHandler("ebay-oauth", ConfigurePipeline);

        services.AddHttpClient<IEbayClient, EbayClient>(c => c.Timeout = Timeout.InfiniteTimeSpan) // the pipeline owns timeouts
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddResilienceHandler("ebay-api", ConfigurePipeline);

        services.AddScoped<IEbayAuthService, EbayAuthService>();
        services.AddSingleton<IEbayApiLogSink, EbayApiLogSink>();
        services.AddScoped<IEbayMetadataService, EbayMetadataService>();
        services.AddScoped<IEbayListingService, EbayListingService>();
        services.AddSingleton<IEbayPublicUrlProvider, EbayPublicUrlProvider>();
        services.AddSingleton<IEbayOAuthStateProtector, EbayOAuthStateProtector>();

        services.AddScoped<EbayOrderImportService>();
        services.AddScoped<EbayShipmentSyncService>();
        services.AddScoped<EbayCategoryRefreshService>();

        services.AddSingleton<EbayJobQueue>();
        services.AddSingleton<IEbayJobQueue>(sp => sp.GetRequiredService<EbayJobQueue>());
        services.AddHostedService<EbayJobService>();
        return services;
    }

    private static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    /// <summary>
    /// 3 attempts in total, exponential backoff with jitter, honouring eBay's Retry-After (rate limits).
    /// 429 is always retried; 5xx / transport failures only for idempotent verbs, so a POST that may have
    /// reached eBay (createOffer, publish) is never sent twice.
    /// </summary>
    private static void ConfigurePipeline(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1),
            ShouldRetryAfterHeader = true,
            ShouldHandle = args =>
            {
                var request = args.Context.GetRequestMessage();
                var idempotent = request is null || request.Method != HttpMethod.Post;
                var retry = args.Outcome switch
                {
                    { Result.StatusCode: HttpStatusCode.TooManyRequests } => true,
                    { Result: { } response } when (int)response.StatusCode >= 500 => idempotent,
                    { Exception: HttpRequestException or TimeoutRejectedException } => idempotent,
                    _ => false,
                };
                return ValueTask.FromResult(retry);
            },
        });

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            TimeoutGenerator = args =>
            {
                var path = args.Context.GetRequestMessage()?.RequestUri?.AbsolutePath ?? string.Empty;
                var isFullTree = path.Contains("/category_tree/", StringComparison.Ordinal)
                                 && !path.Contains("get_item_aspects", StringComparison.Ordinal);
                return ValueTask.FromResult(isFullTree ? CategoryTreeTimeout : AttemptTimeout);
            },
        });
    }
}
