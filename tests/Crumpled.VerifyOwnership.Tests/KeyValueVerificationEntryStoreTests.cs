using System.Text.Json;
using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Services;

namespace Crumpled.VerifyOwnership.Tests;

public class KeyValueVerificationEntryStoreTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static KeyValueVerificationEntryStore CreateSut(IKeyValueService keyValueService) =>
        new(keyValueService, new MemoryCache(new MemoryCacheOptions()), NullLogger<KeyValueVerificationEntryStore>.Instance);

    private static KeyValueVerificationEntryStore CreateSut(IKeyValueService keyValueService, TimeSpan requestThrottleWindow) =>
        new(keyValueService, new MemoryCache(new MemoryCacheOptions()), NullLogger<KeyValueVerificationEntryStore>.Instance, requestThrottleWindow);

    [Fact]
    public void ReloadDocument_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("{not valid json");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadDocument();

        Assert.Empty(document);
    }

    [Fact]
    public void ReloadDocument_NonObjectRoot_TreatedAsEmpty()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("[1,2,3]");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadDocument();

        Assert.Empty(document);
    }

    [Fact]
    public void ReloadDocument_ProviderValueNotArray_DropsOnlyThatProvider()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"google":"not-an-array","bing":[]}""");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadDocument();

        Assert.False(document.ContainsKey("google"));
        Assert.True(document.ContainsKey("bing"));
        Assert.Empty(document["bing"]);
    }

    [Fact]
    public void ReloadDocument_MalformedEntryInArray_DropsOnlyThatEntry()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns(
            """{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"},{"personName":"NoId"}]}""");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadDocument();

        var googleEntries = document["google"];
        Assert.Single(googleEntries);
        Assert.Equal("abc", googleEntries[0].Id);
    }

    [Fact]
    public void ReloadDocument_TransientReadFailure_KeepsLastKnownGood()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns(
            _ => """{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""",
            _ => throw new InvalidOperationException("transient failure"));
        var sut = CreateSut(keyValueService);

        var first = sut.ReloadDocument();
        var second = sut.ReloadDocument();

        Assert.Equal(first["google"].Select(e => e.Id), second["google"].Select(e => e.Id));
    }

    [Fact]
    public void GetEntries_ProviderAbsentFromDocument_ReturnsNull()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"bing":[]}""");
        var sut = CreateSut(keyValueService);

        Assert.Null(sut.GetEntries("google"));
        Assert.NotNull(sut.GetEntries("bing"));
        Assert.Empty(sut.GetEntries("bing")!);
    }

    [Fact]
    public void GetEntries_CachesDocument_SecondCallDoesNotHitKeyValueService()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns(
            """{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""");
        var sut = CreateSut(keyValueService);

        _ = sut.GetEntries("google");
        _ = sut.GetEntries("google");

        keyValueService.Received(1).GetValue(Arg.Any<string>());
    }

    [Fact]
    public void SetEntries_LeavesOtherProvidersUntouched()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        var existingDocument = """{"bing":[{"id":"xyz","personName":"Carol","dateAdded":"2026-01-01T00:00:00+00:00"}]}""";
        keyValueService.GetValue(Arg.Any<string>()).Returns(existingDocument);
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut(keyValueService);

        sut.SetEntries("google", [new VerificationEntry { Id = "abc", PersonName = "Alice", DateAdded = DateTimeOffset.UtcNow }]);

        keyValueService.Received(1).TrySetValue(
            Arg.Any<string>(),
            existingDocument,
            Arg.Is<string>(json => json.Contains("\"bing\"") && json.Contains("xyz") && json.Contains("\"google\"") && json.Contains("abc")));
    }

    [Fact]
    public void SetEntries_ExistingId_PreservesOriginalDateAdded()
    {
        var originalDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var keyValueService = Substitute.For<IKeyValueService>();
        var existingDocument = $$"""{"google":[{"id":"abc","personName":"Alice","dateAdded":"{{originalDate:O}}"}]}""";
        keyValueService.GetValue(Arg.Any<string>()).Returns(existingDocument);
        string? capturedJson = null;
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedJson = callInfo.ArgAt<string>(2);
                return true;
            });
        var sut = CreateSut(keyValueService);

        sut.SetEntries("google", [new VerificationEntry { Id = "abc", PersonName = "Alice Updated", DateAdded = DateTimeOffset.UtcNow }]);

        Assert.NotNull(capturedJson);
        var updatedDocument = JsonSerializer.Deserialize<Dictionary<string, VerificationEntry[]>>(capturedJson!, SerializerOptions);
        var updatedEntry = updatedDocument!["google"].Single();
        Assert.Equal(originalDate, updatedEntry.DateAdded);
        Assert.Equal("Alice Updated", updatedEntry.PersonName);
    }

    [Fact]
    public void SetEntries_ConcurrentWriteLostRace_RetriesAndSucceeds()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("{}");
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false, true);
        var sut = CreateSut(keyValueService);

        sut.SetEntries("google", [new VerificationEntry { Id = "abc", PersonName = "Alice", DateAdded = DateTimeOffset.UtcNow }]);

        keyValueService.Received(2).GetValue(Arg.Any<string>());
        keyValueService.Received(2).TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void SetEntries_FirstEverWrite_UsesPlainSetValueRatherThanTrySetValue()
    {
        // Confirmed against a real IKeyValueService: TrySetValue(key, null, newValue) reports no match
        // rather than treating null as "row doesn't exist yet", so the first-ever write must go through
        // SetValue directly rather than looping through TrySetValue's compare-and-swap.
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);
        var sut = CreateSut(keyValueService);

        sut.SetEntries("google", [new VerificationEntry { Id = "abc", PersonName = "Alice", DateAdded = DateTimeOffset.UtcNow }]);

        keyValueService.Received(1).SetValue(Arg.Any<string>(), Arg.Any<string>());
        keyValueService.DidNotReceive().TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordRequest_RapidRepeatedCalls_OnlyPersistsOnce()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""");
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut(keyValueService);

        sut.RecordRequest("google", "abc");
        sut.RecordRequest("google", "abc");

        keyValueService.Received(1).TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordRequest_WindowElapsed_PersistsAgain()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""");
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut(keyValueService, TimeSpan.Zero);

        sut.RecordRequest("google", "abc");
        sut.RecordRequest("google", "abc");

        keyValueService.Received(2).TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordRequest_UnconfiguredId_IsNoOp()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""");
        var sut = CreateSut(keyValueService);

        sut.RecordRequest("google", "unknown-id");

        keyValueService.DidNotReceive().TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        keyValueService.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordRequest_PreservesOtherFieldsAndOtherProviders()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        var existingDocument = """
            {"google":[{"id":"abc","personName":"Alice","dateAdded":"2020-01-01T00:00:00+00:00"}],"bing":[{"id":"xyz","personName":"Carol","dateAdded":"2021-01-01T00:00:00+00:00"}]}
            """;
        keyValueService.GetValue(Arg.Any<string>()).Returns(existingDocument);
        string? capturedJson = null;
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedJson = callInfo.ArgAt<string>(2);
                return true;
            });
        var sut = CreateSut(keyValueService);

        sut.RecordRequest("google", "abc");

        Assert.NotNull(capturedJson);
        Assert.Contains("\"bing\"", capturedJson);
        Assert.Contains("xyz", capturedJson);
        Assert.Contains("\"personName\":\"Alice\"", capturedJson);
        Assert.Contains("\"dateAdded\":\"2020-01-01T00:00:00", capturedJson);
        Assert.Contains("\"lastRequestedAt\"", capturedJson);
    }

    [Fact]
    public void GetLastRequestedTime_NeverRecorded_ReturnsNull()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""");
        var sut = CreateSut(keyValueService);

        Assert.Null(sut.GetLastRequestedTime("google", "abc"));
    }

    [Fact]
    public void GetLastRequestedTime_AfterRecordRequest_ReturnsRecordedTimestamp()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        string? current = """{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00"}]}""";
        keyValueService.GetValue(Arg.Any<string>()).Returns(_ => current);
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                current = callInfo.ArgAt<string>(2);
                return true;
            });
        var sut = CreateSut(keyValueService);

        sut.RecordRequest("google", "abc");
        var recorded = sut.GetLastRequestedTime("google", "abc");

        Assert.NotNull(recorded);
        Assert.True(DateTimeOffset.UtcNow - recorded.Value < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetEntries_NeverExposesLastRequestedAt()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns(
            """{"google":[{"id":"abc","personName":"Alice","dateAdded":"2026-01-01T00:00:00+00:00","lastRequestedAt":"2026-02-01T00:00:00+00:00"}]}""");
        var sut = CreateSut(keyValueService);

        var entries = sut.GetEntries("google");

        var entry = Assert.Single(entries!);
        Assert.Equal(new VerificationEntry { Id = "abc", PersonName = "Alice", DateAdded = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }, entry);
    }

    [Fact]
    public void ReloadProviderRequestedDocument_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("{not valid json");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadProviderRequestedDocument();

        Assert.Empty(document);
    }

    [Fact]
    public void ReloadProviderRequestedDocument_NonObjectRoot_TreatedAsEmpty()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("[1,2,3]");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadProviderRequestedDocument();

        Assert.Empty(document);
    }

    [Fact]
    public void ReloadProviderRequestedDocument_OneProviderValueNotParseableTimestamp_DropsOnlyThatProvider()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"bing":"not-a-timestamp","google":"2026-01-01T00:00:00+00:00"}""");
        var sut = CreateSut(keyValueService);

        var document = sut.ReloadProviderRequestedDocument();

        Assert.False(document.ContainsKey("bing"));
        Assert.True(document.ContainsKey("google"));
    }

    [Fact]
    public void RecordProviderRequest_RapidRepeatedCalls_OnlyPersistsOnce()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut(keyValueService);

        sut.RecordProviderRequest("bing");
        sut.RecordProviderRequest("bing");

        keyValueService.Received(1).SetValue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordProviderRequest_WindowElapsed_PersistsAgain()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("""{"bing":"2026-01-01T00:00:00+00:00"}""");
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var sut = CreateSut(keyValueService, TimeSpan.Zero);

        sut.RecordProviderRequest("bing");
        sut.RecordProviderRequest("bing");

        keyValueService.Received(2).TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordProviderRequest_CrossProviderIsolation_DoesNotDisturbOtherProviders()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        var existingDocument = """{"google":"2020-01-01T00:00:00+00:00"}""";
        keyValueService.GetValue(Arg.Any<string>()).Returns(existingDocument);
        string? capturedJson = null;
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                capturedJson = callInfo.ArgAt<string>(2);
                return true;
            });
        var sut = CreateSut(keyValueService);

        sut.RecordProviderRequest("bing");

        Assert.NotNull(capturedJson);
        Assert.Contains("\"google\":\"2020-01-01T00:00:00", capturedJson);
        Assert.Contains("\"bing\"", capturedJson);
    }

    [Fact]
    public void RecordProviderRequest_ConcurrentWriteLostRace_RetriesAndSucceeds()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns("{}");
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false, true);
        var sut = CreateSut(keyValueService);

        sut.RecordProviderRequest("bing");

        keyValueService.Received(2).TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void RecordProviderRequest_FirstEverWrite_UsesPlainSetValueRatherThanTrySetValue()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);
        var sut = CreateSut(keyValueService);

        sut.RecordProviderRequest("bing");

        keyValueService.Received(1).SetValue(Arg.Any<string>(), Arg.Any<string>());
        keyValueService.DidNotReceive().TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void GetLastRequestedTime_ProviderLevel_NeverRecorded_ReturnsNull()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);
        var sut = CreateSut(keyValueService);

        Assert.Null(sut.GetLastRequestedTime("bing"));
    }

    [Fact]
    public void GetLastRequestedTime_ProviderLevel_AfterRecordProviderRequest_ReturnsRecordedTimestamp()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        string? current = null;
        keyValueService.GetValue(Arg.Any<string>()).Returns(_ => current);
        keyValueService.TrySetValue(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                current = callInfo.ArgAt<string>(2);
                return true;
            });
        keyValueService.When(x => x.SetValue(Arg.Any<string>(), Arg.Any<string>()))
            .Do(callInfo => current = callInfo.ArgAt<string>(1));
        var sut = CreateSut(keyValueService);

        sut.RecordProviderRequest("bing");
        var recorded = sut.GetLastRequestedTime("bing");

        Assert.NotNull(recorded);
        Assert.True(DateTimeOffset.UtcNow - recorded.Value < TimeSpan.FromMinutes(1));
    }
}
