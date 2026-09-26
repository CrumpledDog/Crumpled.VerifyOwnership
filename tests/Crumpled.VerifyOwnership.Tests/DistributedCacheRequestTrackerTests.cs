using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crumpled.VerifyOwnership.Tests;

public class DistributedCacheRequestTrackerTests
{
    private static IDistributedCache CreateCache() =>
        new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider().GetRequiredService<IDistributedCache>();

    private static DistributedCacheRequestTracker CreateSut(IDistributedCache cache) =>
        new(cache, NullLogger<DistributedCacheRequestTracker>.Instance);

    private static DistributedCacheRequestTracker CreateSut(IDistributedCache cache, TimeSpan requestThrottleWindow) =>
        new(cache, NullLogger<DistributedCacheRequestTracker>.Instance, requestThrottleWindow);

    [Fact]
    public void GetLastRequestedTime_NeverRecorded_ReturnsNull()
    {
        var sut = CreateSut(CreateCache());

        Assert.Null(sut.GetLastRequestedTime("google", "abc"));
    }

    [Fact]
    public void RecordRequest_ThenGetLastRequestedTime_RoundTrips()
    {
        var sut = CreateSut(CreateCache());

        sut.RecordRequest("google", "abc");
        var recorded = sut.GetLastRequestedTime("google", "abc");

        Assert.NotNull(recorded);
        Assert.True(DateTimeOffset.UtcNow - recorded.Value < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RecordRequest_RapidRepeatedCalls_OnlyPersistsOnce()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache);

        sut.RecordRequest("google", "abc");
        var first = sut.GetLastRequestedTime("google", "abc");
        sut.RecordRequest("google", "abc");
        var second = sut.GetLastRequestedTime("google", "abc");

        Assert.Equal(first, second);
    }

    [Fact]
    public void RecordRequest_WindowElapsed_PersistsAgain()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache, TimeSpan.Zero);

        sut.RecordRequest("google", "abc");
        var first = sut.GetLastRequestedTime("google", "abc");
        Thread.Sleep(10);
        sut.RecordRequest("google", "abc");
        var second = sut.GetLastRequestedTime("google", "abc");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RecordRequest_DoesNotDisturbOtherIdsOrProviders()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache);

        sut.RecordRequest("google", "abc");
        sut.RecordRequest("google", "xyz");
        sut.RecordRequest("bing", "abc");

        Assert.NotNull(sut.GetLastRequestedTime("google", "abc"));
        Assert.NotNull(sut.GetLastRequestedTime("google", "xyz"));
        Assert.NotNull(sut.GetLastRequestedTime("bing", "abc"));
        Assert.Null(sut.GetLastRequestedTime("bing", "xyz"));
    }

    [Fact]
    public void GetLastRequestedTime_CacheCleared_ReturnsNullWithoutThrowing()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache);
        sut.RecordRequest("google", "abc");

        cache.Remove("crumpled:verifyownership:lastrequested:google:abc");

        Assert.Null(sut.GetLastRequestedTime("google", "abc"));
    }

    [Fact]
    public void GetLastRequestedTime_ProviderLevel_NeverRecorded_ReturnsNull()
    {
        var sut = CreateSut(CreateCache());

        Assert.Null(sut.GetLastRequestedTime("bing"));
    }

    [Fact]
    public void RecordProviderRequest_ThenGetLastRequestedTime_RoundTrips()
    {
        var sut = CreateSut(CreateCache());

        sut.RecordProviderRequest("bing");
        var recorded = sut.GetLastRequestedTime("bing");

        Assert.NotNull(recorded);
        Assert.True(DateTimeOffset.UtcNow - recorded.Value < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RecordProviderRequest_RapidRepeatedCalls_OnlyPersistsOnce()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache);

        sut.RecordProviderRequest("bing");
        var first = sut.GetLastRequestedTime("bing");
        sut.RecordProviderRequest("bing");
        var second = sut.GetLastRequestedTime("bing");

        Assert.Equal(first, second);
    }

    [Fact]
    public void RecordProviderRequest_WindowElapsed_PersistsAgain()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache, TimeSpan.Zero);

        sut.RecordProviderRequest("bing");
        var first = sut.GetLastRequestedTime("bing");
        Thread.Sleep(10);
        sut.RecordProviderRequest("bing");
        var second = sut.GetLastRequestedTime("bing");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RecordProviderRequest_DoesNotDisturbOtherProvidersOrPerIdKeys()
    {
        var cache = CreateCache();
        var sut = CreateSut(cache);

        sut.RecordProviderRequest("bing");
        sut.RecordRequest("google", "abc");

        Assert.NotNull(sut.GetLastRequestedTime("bing"));
        Assert.Null(sut.GetLastRequestedTime("google"));
        Assert.NotNull(sut.GetLastRequestedTime("google", "abc"));
    }
}
