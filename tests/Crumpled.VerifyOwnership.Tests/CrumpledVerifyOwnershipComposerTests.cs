using Crumpled.VerifyOwnership.Composers;
using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Services;

namespace Crumpled.VerifyOwnership.Tests;

public class CrumpledVerifyOwnershipComposerTests
{
    private static ServiceProvider BuildServiceProvider(bool useDistributedCache, bool registerDistributedCache)
    {
        var services = new ServiceCollection();

        var optionsMonitor = Substitute.For<IOptionsMonitor<VerifyOwnershipOptions>>();
        optionsMonitor.CurrentValue.Returns(new VerifyOwnershipOptions { UseDistributedCache = useDistributedCache });
        services.AddSingleton(optionsMonitor);

        services.AddMemoryCache();
        services.AddSingleton(Substitute.For<IKeyValueService>());
        services.AddSingleton<ILogger<KeyValueVerificationEntryStore>>(NullLogger<KeyValueVerificationEntryStore>.Instance);
        services.AddSingleton<ILogger<DistributedCacheRequestTracker>>(NullLogger<DistributedCacheRequestTracker>.Instance);

        services.AddSingleton<KeyValueVerificationEntryStore>();
        services.AddSingleton<DistributedCacheRequestTracker>();

        if (registerDistributedCache)
        {
            services.AddDistributedMemoryCache();
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void SelectRequestTracker_OptionOff_UsesKeyValueStore()
    {
        var provider = BuildServiceProvider(useDistributedCache: false, registerDistributedCache: false);

        var tracker = CrumpledVerifyOwnershipComposer.SelectRequestTracker(provider);

        Assert.IsType<KeyValueVerificationEntryStore>(tracker);
    }

    [Fact]
    public void SelectRequestTracker_OptionOnWithDistributedCacheRegistered_UsesDistributedCacheTracker()
    {
        var provider = BuildServiceProvider(useDistributedCache: true, registerDistributedCache: true);

        var tracker = CrumpledVerifyOwnershipComposer.SelectRequestTracker(provider);

        Assert.IsType<DistributedCacheRequestTracker>(tracker);
    }

    [Fact]
    public void SelectRequestTracker_OptionOnWithoutDistributedCacheRegistered_Throws()
    {
        // The flag is trusted as-is - no presence-detection fallback (a registered IDistributedCache
        // doesn't reliably indicate a real, farm-wide-shared cache is configured, so we don't try to guess).
        // A misconfigured flag should fail loudly rather than silently under-deliver.
        var provider = BuildServiceProvider(useDistributedCache: true, registerDistributedCache: false);

        Assert.Throws<InvalidOperationException>(() => CrumpledVerifyOwnershipComposer.SelectRequestTracker(provider));
    }
}
