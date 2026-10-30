using Microsoft.Extensions.Caching.Memory;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Commerce;
using HKPartsEcommerce.Infrastructure.Settings;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Commerce;

/// <summary>
/// The tenant commerce-mode switch (REQ-INQ-001). This decides whether the storefront can take money at
/// all, so the failure direction matters: anything missing or unreadable must leave the shop selling
/// normally rather than silently turning it into a catalogue.
/// </summary>
public class CommerceModeServiceTests : CatalogTestBase
{
    private void SeedSetting(string key, string? value)
    {
        using var db = NewContext();
        db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = value, ValueType = "string" });
        db.SaveChanges();
    }

    private async Task<CommerceModeSettings> ResolveAsync()
    {
        using var db = NewContext();
        var settings = new SettingsService(
            db, new MemoryCache(new MemoryCacheOptions()), new FakeCurrentUser(), new FixedClock(new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc)));
        return await new CommerceModeService(settings).GetAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Unconfigured_install_keeps_selling_normally()
    {
        var mode = await ResolveAsync();

        Assert.Equal(CommerceMode.Standard, mode.Mode);
        Assert.False(mode.IsInquiryOnly);
        Assert.True(mode.ShowPrices);
        Assert.True(mode.CollectAddress);
    }

    [Fact]
    public async Task InquiryOnly_is_read_from_the_setting()
    {
        SeedSetting(CommerceSettingKeys.Mode, "InquiryOnly");

        var mode = await ResolveAsync();

        Assert.Equal(CommerceMode.InquiryOnly, mode.Mode);
        Assert.True(mode.IsInquiryOnly);
    }

    [Fact]
    public async Task Unparseable_mode_falls_back_to_standard()
    {
        // A hand-edited or half-migrated row must not be able to switch payments off by accident.
        SeedSetting(CommerceSettingKeys.Mode, "not-a-mode");

        Assert.Equal(CommerceMode.Standard, (await ResolveAsync()).Mode);
    }

    [Fact]
    public async Task Blank_button_label_falls_back_to_the_default()
    {
        SeedSetting(CommerceSettingKeys.ButtonLabel, "   ");

        Assert.Equal("Request a Quote", (await ResolveAsync()).InquiryButtonLabel);
    }

    [Fact]
    public async Task Bool_settings_are_read_and_a_garbage_value_falls_back()
    {
        SeedSetting(CommerceSettingKeys.CollectAddress, "false");
        SeedSetting(CommerceSettingKeys.ShowPrices, "yes-please");

        var mode = await ResolveAsync();

        Assert.False(mode.CollectAddress);
        // Unreadable → the safer default (prices visible), not a silent "off".
        Assert.True(mode.ShowPrices);
    }

    [Fact]
    public async Task Empty_default_store_resolves_to_null()
    {
        SeedSetting(CommerceSettingKeys.DefaultStoreId, "");

        Assert.Null((await ResolveAsync()).DefaultStoreId);
    }

    [Fact]
    public async Task Default_store_is_parsed_when_set()
    {
        var storeId = Guid.NewGuid();
        SeedSetting(CommerceSettingKeys.DefaultStoreId, storeId.ToString());

        Assert.Equal(storeId, (await ResolveAsync()).DefaultStoreId);
    }
}
