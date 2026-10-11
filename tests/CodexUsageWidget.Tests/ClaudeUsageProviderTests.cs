using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using CodexUsageWidget.Infrastructure.Claude;

namespace CodexUsageWidget.Tests;

public sealed class ClaudeUsageProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CodexUsageWidget.Tests", Guid.NewGuid().ToString("N"));
    private string CredentialsPath => Path.Combine(_directory, "credentials.json");

    [Fact]
    public async Task UsesOnlyTheUsageEndpointAndDoesNotChangeCredentials()
    {
        WriteCredentials();
        var original = File.ReadAllText(CredentialsPath);
        var handler = new FakeHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.anthropic.com/api/oauth/usage", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            Assert.Equal("oauth-2025-04-20", Assert.Single(request.Headers.GetValues("anthropic-beta")));
            return Success();
        });
        await using var provider = new ClaudeUsageProvider(handler, CredentialsPath);
        var snapshot = await provider.ReadUsageAsync();
        Assert.Equal(2, snapshot.GeneralWindows.Count);
        Assert.Equal(original, File.ReadAllText(CredentialsPath));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("not-json")]
    [InlineData("{\"claudeAiOauth\":{\"accessToken\":\"test-token\",\"expiresAt\":1}}")]
    public async Task MissingOrExpiredLoginDoesNotSendARequest(string credentials)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(CredentialsPath, credentials);
        var handler = new FakeHandler(_ => Success());
        await using var provider = new ClaudeUsageProvider(handler, CredentialsPath);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadUsageAsync());
        Assert.Equal("Claude_LoginRequired", error.Message);
        Assert.Equal(0, handler.Count);
        Assert.Equal(credentials, File.ReadAllText(CredentialsPath));
    }

    [Fact]
    public async Task RepeatedRefreshRespectsRetryAfterAndRecovers()
    {
        WriteCredentials();
        var clock = new MutableClock();
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));
            return response;
        });
        await using var provider = new ClaudeUsageProvider(handler, CredentialsPath, clock);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadUsageAsync());
        Assert.Equal("Claude_RateLimited", error.Message);
        clock.Now = clock.Now.AddMinutes(9);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadUsageAsync());
        Assert.Equal(1, handler.Count);
        clock.Now = clock.Now.AddMinutes(1);
        handler.Respond = _ => Success();
        Assert.Equal(2, (await provider.ReadUsageAsync()).GeneralWindows.Count);
        Assert.Equal(2, handler.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Claude_LoginRequired")]
    [InlineData(HttpStatusCode.Forbidden, "Claude_LoginRequired")]
    [InlineData(HttpStatusCode.InternalServerError, "Claude_ServiceUnavailable")]
    public async Task ErrorBodiesNeverReachTheUi(HttpStatusCode status, string expected)
    {
        WriteCredentials();
        var handler = new FakeHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("sensitive-response") });
        await using var provider = new ClaudeUsageProvider(handler, CredentialsPath);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ReadUsageAsync());
        Assert.Equal(expected, error.Message);
    }

    private void WriteCredentials()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(CredentialsPath, """{"claudeAiOauth":{"accessToken":"test-token"}}""");
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"five_hour":{"utilization":42},"seven_day":{"utilization":67}}""")
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Count++;
            return Task.FromResult(Respond(request));
        }
    }
}
