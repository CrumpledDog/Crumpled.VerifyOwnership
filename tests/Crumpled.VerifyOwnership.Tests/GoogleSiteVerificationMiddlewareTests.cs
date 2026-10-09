using Crumpled.VerifyOwnership.Middleware;
using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class GoogleSiteVerificationMiddlewareTests
{
    private static VerificationEntry Entry(string id) => new() { Id = id, PersonName = "Test", DateAdded = DateTimeOffset.UtcNow };

    private static GoogleSiteVerificationMiddleware CreateSut(IGoogleSiteVerificationStore store) =>
        new(store, NullLogger<GoogleSiteVerificationMiddleware>.Instance);

    [Fact]
    public async Task InvokeAsync_ConfiguredToken_WritesVerificationBody()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetEntries().Returns([Entry("abc123")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/googleabc123.html";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.False(nextCalled);
        Assert.Equal("text/plain", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal("google-site-verification: googleabc123.html", body);
    }

    [Fact]
    public async Task InvokeAsync_ConfiguredToken_GoogleLikeUserAgent_RecordsRequest()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetEntries().Returns([Entry("abc123")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/googleabc123.html";
        context.Request.Headers.UserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        store.Received(1).RecordRequestServed("abc123");
    }

    [Fact]
    public async Task InvokeAsync_ConfiguredToken_NonGoogleUserAgent_ServesFileButDoesNotRecordRequest()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetEntries().Returns([Entry("abc123")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/googleabc123.html";
        context.Request.Headers.UserAgent = "curl/8.11.0";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.False(nextCalled);
        Assert.Equal("text/plain", context.Response.ContentType);
        store.DidNotReceive().RecordRequestServed(Arg.Any<string>());
    }

    [Fact]
    public async Task InvokeAsync_UnconfiguredToken_CallsNext()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetEntries().Returns([Entry("abc123")]);
        var middleware = CreateSut(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/googleunknown.html";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        store.DidNotReceive().RecordRequestServed(Arg.Any<string>());
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedPath_CallsNext()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
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
        store.DidNotReceive().RecordRequestServed(Arg.Any<string>());
    }
}
