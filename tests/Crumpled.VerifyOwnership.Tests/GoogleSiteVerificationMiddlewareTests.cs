using Crumpled.VerifyOwnership.Middleware;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Crumpled.VerifyOwnership.Tests;

public class GoogleSiteVerificationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ConfiguredToken_WritesVerificationBody()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetCodes().Returns(["abc123"]);
        var middleware = new GoogleSiteVerificationMiddleware(store);

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
    public async Task InvokeAsync_UnconfiguredToken_CallsNext()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        store.GetCodes().Returns(["abc123"]);
        var middleware = new GoogleSiteVerificationMiddleware(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/googleunknown.html";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UnrelatedPath_CallsNext()
    {
        var store = Substitute.For<IGoogleSiteVerificationStore>();
        var middleware = new GoogleSiteVerificationMiddleware(store);

        var context = new DefaultHttpContext();
        context.Request.Path = "/some-other-page";

        var nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        store.DidNotReceive().GetCodes();
    }
}
