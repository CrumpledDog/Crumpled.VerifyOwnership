using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Services;

namespace Crumpled.VerifyOwnership.Tests;

public class KeyValueGoogleSiteVerificationStoreTests
{
    private static KeyValueGoogleSiteVerificationStore CreateSut(
        IKeyValueService keyValueService,
        params string[] seedCodes)
    {
        var options = Substitute.For<IOptionsMonitor<GoogleSiteVerificationOptions>>();
        options.CurrentValue.Returns(new GoogleSiteVerificationOptions { VerificationCodes = seedCodes });
        return new KeyValueGoogleSiteVerificationStore(keyValueService, options);
    }

    [Fact]
    public void GetCodes_NoStoredValue_SeedsFromOptionsAndPersists()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue("crumpled.verifyownership.codes").Returns((string?)null);
        var sut = CreateSut(keyValueService, "seed1", "seed2");

        var codes = sut.GetCodes();

        Assert.Equal(["seed1", "seed2"], codes);
        keyValueService.Received(1).SetValue("crumpled.verifyownership.codes", Arg.Is<string>(v => v.Contains("seed1") && v.Contains("seed2")));
    }

    [Fact]
    public void GetCodes_StoredValue_ReturnsDeserializedCodes()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        keyValueService.GetValue("crumpled.verifyownership.codes").Returns("""["abc","def"]""");
        var sut = CreateSut(keyValueService);

        var codes = sut.GetCodes();

        Assert.Equal(["abc", "def"], codes);
    }

    [Fact]
    public void SetCodes_TrimsDeduplicatesAndDropsEmpty()
    {
        var keyValueService = Substitute.For<IKeyValueService>();
        var sut = CreateSut(keyValueService);

        sut.SetCodes([" abc ", "abc", "", "  ", "def"]);

        keyValueService.Received(1).SetValue("crumpled.verifyownership.codes", """["abc","def"]""");
    }
}
