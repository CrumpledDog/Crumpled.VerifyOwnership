using Crumpled.VerifyOwnership.Controllers;
using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Services;
using Crumpled.VerifyOwnership.ViewModels;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class VerifyOwnershipConfigControllerTests
{
    private static VerificationEntry Entry(string id, string personName) =>
        new() { Id = id, PersonName = personName, DateAdded = DateTimeOffset.UtcNow };

    private static VerifyOwnershipConfigController CreateSut(
        out IGoogleSiteVerificationStore googleStore,
        out IBingSiteVerificationStore bingStore)
    {
        googleStore = Substitute.For<IGoogleSiteVerificationStore>();
        bingStore = Substitute.For<IBingSiteVerificationStore>();
        return new VerifyOwnershipConfigController(googleStore, bingStore);
    }

    [Fact]
    public void GoogleEntries_MapsEntriesAndPerIdLastRequestedTime()
    {
        var sut = CreateSut(out var googleStore, out _);
        googleStore.GetEntries().Returns([Entry("abc123", "Alice")]);
        var expected = DateTimeOffset.UtcNow;
        googleStore.GetLastRequestedTime("abc123").Returns(expected);

        var result = sut.GoogleEntries();

        var entry = Assert.Single(result.Entries);
        Assert.Equal("abc123", entry.Id);
        Assert.Equal("Alice", entry.PersonName);
        Assert.Equal(expected, entry.LastRequestedAt);
    }

    [Fact]
    public void SetGoogleEntries_DelegatesToGoogleStore()
    {
        var sut = CreateSut(out var googleStore, out _);
        var model = new GoogleVerificationEntriesRequestModel
        {
            Entries = [new GoogleVerificationEntryRequestModel { Id = "abc123", PersonName = "Alice" }],
        };

        var result = sut.SetGoogleEntries(model);

        Assert.IsType<OkResult>(result);
        googleStore.Received(1).SetEntries(Arg.Is<IEnumerable<VerificationEntry>>(e => e.Single().Id == "abc123"));
    }

    [Fact]
    public void BingEntries_MapsEntriesWithoutPerIdLastRequestedTime_AndSurfacesFileLastRequestedAt()
    {
        var sut = CreateSut(out _, out var bingStore);
        bingStore.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3", "Bob")]);
        var expected = DateTimeOffset.UtcNow;
        bingStore.GetLastRequestedTime().Returns(expected);

        var result = sut.BingEntries();

        var entry = Assert.Single(result.Entries);
        Assert.Equal("7B5625FF68322EE169CF17B5C4D878B3", entry.Id);
        Assert.Null(entry.LastRequestedAt);
        Assert.Equal(expected, result.FileLastRequestedAt);
    }

    [Fact]
    public void SetBingEntries_DelegatesToBingStore()
    {
        var sut = CreateSut(out _, out var bingStore);
        var model = new BingVerificationEntriesRequestModel
        {
            Entries = [new BingVerificationEntryRequestModel { Id = "7B5625FF68322EE169CF17B5C4D878B3", PersonName = "Bob" }],
        };

        var result = sut.SetBingEntries(model);

        Assert.IsType<OkResult>(result);
        bingStore.Received(1).SetEntries(Arg.Is<IEnumerable<VerificationEntry>>(e => e.Single().Id == "7B5625FF68322EE169CF17B5C4D878B3"));
    }
}
