using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HKPartsEcommerce.Application;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ebay;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Infrastructure;
using HKPartsEcommerce.Infrastructure.Ebay;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Ebay;

/// <summary>Every eBay service and handler resolves from the real container (no database is touched).</summary>
public class EbayDependencyInjectionTests
{
    [Fact]
    public void Ebay_services_and_handlers_resolve()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=DiOnly;Integrated Security=True;TrustServerCertificate=True;",
                ["Storefront:PublicBaseUrl"] = "http://localhost:9000",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ICurrentUserService, FakeCurrentUser>();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.IsType<EbayClient>(sp.GetRequiredService<IEbayClient>());
        Assert.IsType<EbayOAuthClient>(sp.GetRequiredService<IEbayOAuthClient>());
        sp.GetRequiredService<IEbayAuthService>();
        sp.GetRequiredService<IEbayMetadataService>();
        sp.GetRequiredService<IEbayListingService>();
        sp.GetRequiredService<IEbayJobQueue>();
        sp.GetRequiredService<IEbayOAuthStateProtector>();
        sp.GetRequiredService<EbayOrderImportService>();
        sp.GetRequiredService<EbayShipmentSyncService>();
        sp.GetRequiredService<EbayCategoryRefreshService>();

        sp.GetRequiredService<IRequestHandler<SaveEbayListingCommand, EbayListingDto>>();
        sp.GetRequiredService<IRequestHandler<CreateEbayListingCommand, EbayListingDto>>();
        sp.GetRequiredService<IRequestHandler<CompleteEbayOAuthCommand, Guid>>();
        sp.GetRequiredService<IRequestHandler<SyncEbayLocationCommand, string>>();
    }
}
