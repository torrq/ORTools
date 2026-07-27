using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ORTools.UI.Services;

/// <summary>
/// Checks the GitHub Releases API for newer versions of the application.
/// This is a UI-only service — it does not interact with WorkerCore or IPC.
/// </summary>
public static class UpdateCheckerService
{
    private static readonly HttpClient _http = CreateHttpClient();
    private static UpdateResult? _cached;

    private const string ReleaseUrl = "https://api.github.com/repos/torrq/ORTools/releases/latest";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub API requires a User-Agent header on all requests.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ORTools", GetCurrentVersion()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>
    /// Check for updates. Returns cached result unless <paramref name="forceRefresh"/> is true.
    /// This method never throws — all errors are returned in <see cref="UpdateResult.ErrorMessage"/>.
    /// </summary>
    public static async Task<UpdateResult> CheckAsync(bool forceRefresh = false)
    {
        if (_cached != null && !forceRefresh)
            return _cached;

        try
        {
            var response = await _http.GetAsync(ReleaseUrl).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return CacheAndReturn(new UpdateResult(
                    IsUpdateAvailable: false,
                    CurrentVersion: GetCurrentVersion(),
                    LatestVersion: "",
                    ReleaseUrl: null,
                    ErrorMessage: "Rate limit reached. Try again later."));
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No releases published on GitHub yet -> current version is up to date
                return CacheAndReturn(new UpdateResult(
                    IsUpdateAvailable: false,
                    CurrentVersion: GetCurrentVersion(),
                    LatestVersion: GetCurrentVersion(),
                    ReleaseUrl: "https://github.com/torrq/ORTools/releases",
                    ErrorMessage: null));
            }

            if (!response.IsSuccessStatusCode)
            {
                return CacheAndReturn(new UpdateResult(
                    IsUpdateAvailable: false,
                    CurrentVersion: GetCurrentVersion(),
                    LatestVersion: "",
                    ReleaseUrl: null,
                    ErrorMessage: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"));
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString() ?? "";

            var currentStr = GetCurrentVersion();
            var latestStr = StripVersionPrefix(tagName);

            if (!Version.TryParse(currentStr, out var currentVer) ||
                !Version.TryParse(latestStr, out var latestVer))
            {
                return CacheAndReturn(new UpdateResult(
                    IsUpdateAvailable: false,
                    CurrentVersion: currentStr,
                    LatestVersion: latestStr,
                    ReleaseUrl: htmlUrl,
                    ErrorMessage: "Could not parse version."));
            }

            var isNewer = latestVer > currentVer;

            return CacheAndReturn(new UpdateResult(
                IsUpdateAvailable: isNewer,
                CurrentVersion: currentStr,
                LatestVersion: latestStr,
                ReleaseUrl: htmlUrl,
                ErrorMessage: null));
        }
        catch (TaskCanceledException)
        {
            return CacheAndReturn(new UpdateResult(
                IsUpdateAvailable: false,
                CurrentVersion: GetCurrentVersion(),
                LatestVersion: "",
                ReleaseUrl: null,
                ErrorMessage: "Request timed out."));
        }
        catch (HttpRequestException ex)
        {
            return CacheAndReturn(new UpdateResult(
                IsUpdateAvailable: false,
                CurrentVersion: GetCurrentVersion(),
                LatestVersion: "",
                ReleaseUrl: null,
                ErrorMessage: $"Network error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return CacheAndReturn(new UpdateResult(
                IsUpdateAvailable: false,
                CurrentVersion: GetCurrentVersion(),
                LatestVersion: "",
                ReleaseUrl: null,
                ErrorMessage: $"Error: {ex.Message}"));
        }
    }

    private static UpdateResult CacheAndReturn(UpdateResult result)
    {
        _cached = result;
        return result;
    }

    /// <summary>
    /// Returns the current app version without the "v" prefix (e.g. "2.0").
    /// </summary>
    private static string GetCurrentVersion()
        => StripVersionPrefix(
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "0.0");

    /// <summary>
    /// Strips a leading "v" or "V" from a version string (e.g. "v2.1" → "2.1").
    /// </summary>
    private static string StripVersionPrefix(string version)
        => version.StartsWith('v') || version.StartsWith('V')
            ? version[1..]
            : version;
}

/// <summary>
/// Immutable result of an update check.
/// </summary>
public record UpdateResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string? ReleaseUrl,
    string? ErrorMessage);
