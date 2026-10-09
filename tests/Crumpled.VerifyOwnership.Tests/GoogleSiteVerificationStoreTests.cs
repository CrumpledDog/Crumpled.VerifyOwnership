using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class GoogleSiteVerificationStoreTests
{
    private static GoogleSiteVerificationStore CreateSut(
        IVerificationEntryStore entryStore,
        params VerificationCodeSeed[] seed) =>
        CreateSut(entryStore, Substitute.For<IVerificationRequestTracker>(), seed);

    private static GoogleSiteVerificationStore CreateSut(
        IVerificationEntryStore entryStore,
        IVerificationRequestTracker requestTracker,
        params VerificationCodeSeed[] seed)
    {
        var options = Substitute.For<IOptionsMonitor<GoogleSiteVerificationOptions>>();
        options.CurrentValue.Returns(new GoogleSiteVerificationOptions { VerificationCodes = seed });
        return new GoogleSiteVerificationStore(entryStore, requestTracker, options);
    }

    [Fact]
    public void GetEntries_ProviderNeverSeeded_SeedsFromOptionsAndPersists()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        entryStore.GetEntries("google").Returns((IReadOnlyList<VerificationEntry>?)null);
        var sut = CreateSut(entryStore, new VerificationCodeSeed { Id = "seed1", PersonName = "Alice" });

        var entries = sut.GetEntries();

        Assert.Equal(["seed1"], entries.Select(e => e.Id));
        Assert.Equal(["Alice"], entries.Select(e => e.PersonName));
        entryStore.Received(1).SetEntries("google", Arg.Is<IEnumerable<VerificationEntry>>(e => e.Single().Id == "seed1"));
    }

    [Fact]
    public void GetEntries_ProviderExplicitlyEmpty_DoesNotReseed()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        entryStore.GetEntries("google").Returns((IReadOnlyList<VerificationEntry>?)Array.Empty<VerificationEntry>());
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
        entryStore.GetEntries("google").Returns(stored);
        var sut = CreateSut(entryStore);

        var entries = sut.GetEntries();

        Assert.Same(stored, entries);
    }

    [Fact]
    public void SetEntries_DelegatesToEntryStoreScopedToGoogle()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var sut = CreateSut(entryStore);
        var entries = new[] { new VerificationEntry { Id = "abc", PersonName = "Bob", DateAdded = DateTimeOffset.UtcNow } };

        sut.SetEntries(entries);

        entryStore.Received(1).SetEntries("google", entries);
    }

    [Fact]
    public void RecordRequestServed_DelegatesToTrackerScopedToGoogle()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var requestTracker = Substitute.For<IVerificationRequestTracker>();
        var sut = CreateSut(entryStore, requestTracker);

        sut.RecordRequestServed("abc");

        requestTracker.Received(1).RecordRequest("google", "abc");
    }

    [Fact]
    public void GetLastRequestedTime_DelegatesToTrackerScopedToGoogle()
    {
        var entryStore = Substitute.For<IVerificationEntryStore>();
        var requestTracker = Substitute.For<IVerificationRequestTracker>();
        var expected = DateTimeOffset.UtcNow;
        requestTracker.GetLastRequestedTime("google", "abc").Returns(expected);
        var sut = CreateSut(entryStore, requestTracker);

        var result = sut.GetLastRequestedTime("abc");

        Assert.Equal(expected, result);
    }
}
