using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CodexUsageWidget.Application;
using CodexUsageWidget.Domain;

namespace CodexUsageWidget.Infrastructure.Claude;

// Reads the existing CLI login without changing or copying its credentials.
public sealed class ClaudeUsageProvider : IUsageProvider
{
    private readonly HttpClient _client;
    private readonly string _credentialsPath;
    private readonly TimeProvider _clock;
    private DateTimeOffset _nextAttempt;
    private string? _lastError;
    private UsageSnapshot? _cached;
    private bool _disposed;

    public ClaudeUsageProvider(
        HttpMessageHandler? handler = null,
        string? credentialsPath = null,
        TimeProvider? clock = null)
    {
        _client = handler is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            : new HttpClient(handler);
        _client.Timeout = Timeout.InfiniteTimeSpan;
        _credentialsPath = credentialsPath ??
            Environment.GetEnvironmentVariable("USAGE_WIDGET_CLAUDE_CREDENTIALS") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", ".credentials.json");
        _clock = clock ?? TimeProvider.System;
    }

    public event EventHandler? RateLimitsChanged { add { } remove { } }
    public event EventHandler<string>? DiagnosticMessage { add { } remove { } }

    public async Task<UsageSnapshot> ReadUsageAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.GetUtcNow();
        if (now < _nextAttempt)
        {
            if (_lastError is not null)
            {
                throw new InvalidOperationException(_lastError);
            }

            if (_cached is not null)
            {
                return _cached;
            }
        }

        try
        {
            var token = await ReadAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.anthropic.com/api/oauth/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = response.Headers.RetryAfter;
                var wait = retry?.Delta ?? (retry?.Date - now) ?? TimeSpan.FromMinutes(5);
                _nextAttempt = now.Add(wait > TimeSpan.Zero ? wait : TimeSpan.FromMinutes(5));
                throw new InvalidOperationException("Claude_RateLimited");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("Claude_LoginRequired");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Claude_ServiceUnavailable");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _cached = ClaudeUsageParser.Parse(payload.RootElement, _clock.GetUtcNow());
            _lastError = null;
            _nextAttempt = now.AddSeconds(15);
            return _cached;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            RememberError("Claude_NetworkError", now);
            throw new InvalidOperationException("Claude_NetworkError");
        }
        catch (JsonException)
        {
            RememberError("Claude_InvalidResponse", now);
            throw new InvalidOperationException("Claude_InvalidResponse");
        }
        catch (FormatException)
        {
            RememberError("Claude_LoginRequired", now);
            throw new InvalidOperationException("Claude_LoginRequired");
        }
        catch (InvalidOperationException ex)
        {
            RememberError(ex.Message, now);
            throw;
        }
    }

    private void RememberError(string key, DateTimeOffset now)
    {
        _lastError = key;
        if (_nextAttempt <= now)
        {
            _nextAttempt = now.AddMinutes(1);
        }
    }

    private async Task<string> ReadAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(_credentialsPath);
            using var credentials = await JsonDocument.ParseAsync(stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (credentials.RootElement.ValueKind != JsonValueKind.Object ||
                !credentials.RootElement.TryGetProperty("claudeAiOauth", out var oauth) ||
                oauth.ValueKind != JsonValueKind.Object ||
                !oauth.TryGetProperty("accessToken", out var accessToken) ||
                accessToken.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(accessToken.GetString()))
            {
                throw new InvalidOperationException("Claude_LoginRequired");
            }

            if (oauth.TryGetProperty("expiresAt", out var expires) &&
                expires.ValueKind == JsonValueKind.Number && expires.TryGetInt64(out var milliseconds) &&
                milliseconds <= _clock.GetUtcNow().ToUnixTimeMilliseconds())
            {
                throw new InvalidOperationException("Claude_LoginRequired");
            }

            return accessToken.GetString()!;
        }
        catch (IOException)
        {
            throw new InvalidOperationException("Claude_LoginRequired");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Claude_LoginRequired");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Claude_LoginRequired");
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _client.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
