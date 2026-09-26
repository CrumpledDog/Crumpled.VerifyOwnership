using Crumpled.VerifyOwnership.Middleware;
using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class BingSiteVerificationMiddlewareTests
{
    private static VerificationEntry Entry(string id) => new() { Id = id, PersonName = "Test", DateAdded = DateTimeOffset.UtcNow };

    private static BingSiteVerificationMiddleware CreateSut(IBingSiteVerificationStore store) =>
        new(store, NullLogger<BingSiteVerificationMiddleware>.Instance);

    [Fact]
    public async Task InvokeAsync_ConfiguredEntries_WritesXmlBody()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/BingSiteAuth.xml";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.False(nextCalled);
        Assert.Equal("text/xml", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(
            "<?xml version=\"1.0\"?><users><user>7B5625FF68322EE169CF17B5C4D878B3</user></users>",
            body);
    }

    [Fact]
    public async Task InvokeAsync_MultipleConfiguredEntries_WritesOneUserElementPerEntryInOrder()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3"), Entry("AABBCCDDEEFF00112233445566778899")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/BingSiteAuth.xml";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal(
            "<?xml version=\"1.0\"?><users><user>7B5625FF68322EE169CF17B5C4D878B3</user><user>AABBCCDDEEFF00112233445566778899</user></users>",
            body);
    }

    [Fact]
    public async Task InvokeAsync_ConfiguredEntries_BingLikeUserAgent_RecordsFileServed()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/BingSiteAuth.xml";
        context.Request.Headers.UserAgent = "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        store.Received(1).RecordFileServed();
    }

    [Fact]
    public async Task InvokeAsync_ConfiguredEntries_NonBingUserAgent_ServesFileButDoesNotRecordFileServed()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/BingSiteAuth.xml";
        context.Request.Headers.UserAgent = "curl/8.11.0";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.False(nextCalled);
        Assert.Equal("text/xml", context.Response.ContentType);
        store.DidNotReceive().RecordFileServed();
    }

    [Fact]
    public async Task InvokeAsync_NoConfiguredEntries_CallsNext()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/BingSiteAuth.xml";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        store.DidNotReceive().RecordFileServed();
    }

    [Fact]
    public async Task InvokeAsync_LowercasePath_IsCaseSensitiveAndCallsNext()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        store.GetEntries().Returns([Entry("7B5625FF68322EE169CF17B5C4D878B3")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/bingsiteauth.xml";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        store.DidNotReceive().GetEntries();
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedPath_CallsNext()
    {
        var store = Substitute.For<IBingSiteVerificationStore>();
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/some-other-page";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        store.DidNotReceive().GetEntries();
        store.DidNotReceive().RecordFileServed();
    }
}
