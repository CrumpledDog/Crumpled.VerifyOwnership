using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class BingSiteVerificationStoreTests
{
    private static BingSiteVerificationStore CreateSut(
        IVerificationEntryStore entryStore,
        params VerificationCodeSeed[] seed) =>
        CreateSut(entryStore, Substitute.For<IVerificationRequestTracker>(), seed);

    private static BingSiteVerificationStore CreateSut(
        IVerificationEntryStore entryStore,
        IVerificationRequestTracker requestTracker,
        params VerificationCodeSeed[] seed)
    {
        var options = Substitute.For<IOptionsMonitor<BingSiteVerificationOptions>>();
        options.CurrentValue.Returns(new BingSiteVerificationOptions { VerificationCodes = seed });
        return new BingSiteVerificationStore(entryStore, requestTracker, options);
    }

    [Fact]
    public void GetEntries_ProviderNeverSeeded_SeedsFromOptionsAndPersists()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        entryStore.GetEntries("bing").Returns((IReadOnlyList<VerificationEntry>?)null);
        var sut = CreateSut(entryStore, new VerificationCodeSeed { Id = "7B5625FF68322EE169CF17B5C4D878B3", PersonName = "Alice" });

        var entries = sut.GetEntries();

        Assert.Equal(["7B5625FF68322EE169CF17B5C4D878B3"], entries.Select(e => e.Id));
        Assert.Equal(["Alice"], entries.Select(e => e.PersonName));
        entryStore.Received(1).SetEntries("bing", Arg.Is<IEnumerable<VerificationEntry>>(e => e.Single().Id == "7B5625FF68322EE169CF17B5C4D878B3"));
    }

    [Fact]
    public void GetEntries_ProviderExplicitlyEmpty_DoesNotReseed()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        entryStore.GetEntries("bing").Returns((IReadOnlyList<VerificationEntry>?)Array.Empty<VerificationEntry>());
        var sut = CreateSut(entryStore, new VerificationCodeSeed { Id = "seed1", PersonName = "Alice" });

        var entries = sut.GetEntries();

        Assert.Empty(entries);
        entryStore.DidNotReceive().SetEntries(Arg.Any<string>(), Arg.Any<IEnumerable<VerificationEntry>>());
    }

    [Fact]
    public void GetEntries_ProviderHasStoredEntries_ReturnsThem()
    {
        var stored = new[] { new VerificationEntry { Id = "abc", PersonName = "Bob", DateAdded = DateTimeOffset.UtcNow } };
        var entryStore = Substitute.For<IVerificationEntryStore>();
        entryStore.GetEntries("bing").Returns(stored);
        var sut = CreateSut(entryStore);

        var entries = sut.GetEntries();

        Assert.Same(stored, entries);
    }

    [Fact]
    public void SetEntries_DelegatesToEntryStoreScopedToBing()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var sut = CreateSut(entryStore);
        var entries = new[] { new VerificationEntry { Id = "abc", PersonName = "Bob", DateAdded = DateTimeOffset.UtcNow } };

        sut.SetEntries(entries);

        entryStore.Received(1).SetEntries("bing", entries);
    }

    [Fact]
    public void RecordFileServed_DelegatesToProviderLevelTrackerScopedToBing()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var requestTracker = Substitute.For<IVerificationRequestTracker>();
        var sut = CreateSut(entryStore, requestTracker);

        sut.RecordFileServed();

        requestTracker.Received(1).RecordProviderRequest("bing");
    }

    [Fact]
    public void GetLastRequestedTime_DelegatesToProviderLevelTrackerScopedToBing()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var requestTracker = Substitute.For<IVerificationRequestTracker>();
        var expected = DateTimeOffset.UtcNow;
        requestTracker.GetLastRequestedTime("bing").Returns(expected);
        var sut = CreateSut(entryStore, requestTracker);

        var result = sut.GetLastRequestedTime();

        Assert.Equal(expected, result);
    }
}
