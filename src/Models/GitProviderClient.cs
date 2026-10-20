using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Minimal REST client for the GitHub and GitLab APIs (personal access tokens).
    /// </summary>
    public static class GitProviderClient
    {
        private const int PAGE_SIZE = 100;
        private const int MAX_PAGES = 30;

        private static readonly HttpClient _http = CreateClient();

        /// <summary>
        ///     Validates the token and returns the login name of the user.
        /// </summary>
        public static async Task<string> GetUsernameAsync(GitProviderAccount account, CancellationToken ct = default)
        {
            var url = account.Type == GitProviderType.GitHub
                ? "https://api.github.com/user"
                : $"{account.Host}/api/v4/user";

            using var doc = await GetJsonAsync(account, url, ct);
            var key = account.Type == GitProviderType.GitHub ? "login" : "username";
            return doc.RootElement.GetProperty(key).GetString();
        }

        public static async Task<List<GitProviderRepo>> ListReposAsync(GitProviderAccount account, CancellationToken ct = default)
        {
            var repos = new List<GitProviderRepo>();
            for (var page = 1; page <= MAX_PAGES; page++)
            {
                var url = account.Type == GitProviderType.GitHub
                    ? $"https://api.github.com/user/repos?per_page={PAGE_SIZE}&page={page}&sort=updated&affiliation=owner,collaborator,organization_member"
                    : $"{account.Host}/api/v4/projects?membership=true&simple=true&per_page={PAGE_SIZE}&page={page}&order_by=last_activity_at";

                using var doc = await GetJsonAsync(account, url, ct);
                var count = 0;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    count++;
                    if (account.Type == GitProviderType.GitHub)
                    {
                        repos.Add(new GitProviderRepo(
                            Str(item, "full_name"),
                            Str(item, "description"),
                            item.TryGetProperty("private", out var p) && p.ValueKind == JsonValueKind.True,
                            Str(item, "clone_url"),
                            Str(item, "ssh_url")));
                    }
                    else
                    {
                        repos.Add(new GitProviderRepo(
                            Str(item, "path_with_namespace"),
                            Str(item, "description"),
                            Str(item, "visibility") != "public",
                            Str(item, "http_url_to_repo"),
                            Str(item, "ssh_url_to_repo")));
                    }
                }

                if (count < PAGE_SIZE)
                    break;
            }

            return repos;
        }

        private static async Task<JsonDocument> GetJsonAsync(GitProviderAccount account, string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (account.Type == GitProviderType.GitHub)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.Token);
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
            }
            else
            {
                req.Headers.Add("PRIVATE-TOKEN", account.Token);
            }

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase} ({url})");

            var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        }

        private static string Str(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : string.Empty;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SourceGit");
            return client;
        }
    }
}
