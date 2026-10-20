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
        private const int MAX_PAGES = 50;

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

        /// <summary>
        ///     Root nodes of the tree: personal projects, each top-level group / organization, and
        ///     (lazily) everything the account can access. Only the group list is fetched here.
        /// </summary>
        public static async Task<List<GitProviderNode>> ListRootsAsync(GitProviderAccount account, CancellationToken ct)
        {
            var isGitHub = account.Type == GitProviderType.GitHub;
            var host = account.Host;
            var roots = new List<GitProviderNode>();

            roots.Add(GitProviderNode.CreateGroup(account, ct, $"{account.Username} (personal)", null, isGitHub
                ? $"https://api.github.com/user/repos?per_page={PAGE_SIZE}&sort=updated&affiliation=owner"
                : $"{host}/api/v4/users/{Uri.EscapeDataString(account.Username)}/projects?simple=true&per_page={PAGE_SIZE}&order_by=last_activity_at&sort=desc"));

            var url = isGitHub
                ? $"https://api.github.com/user/orgs?per_page={PAGE_SIZE}"
                : $"{host}/api/v4/groups?min_access_level=10&top_level_only=true&per_page={PAGE_SIZE}&order_by=name";

            for (var page = 0; page < MAX_PAGES && url != null; page++)
            {
                var (doc, next) = await GetJsonPageAsync(account, url, ct).ConfigureAwait(false);
                url = next;

                using (doc)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                        roots.Add(isGitHub ? GitHubOrg(account, ct, item) : GitLabGroup(account, ct, item, Str(item, "full_path")));
                }
            }

            roots.Add(GitProviderNode.CreateGroup(account, ct, "All accessible projects (slow)", null, isGitHub
                ? $"https://api.github.com/user/repos?per_page={PAGE_SIZE}&sort=updated&affiliation=owner,collaborator,organization_member"
                : $"{host}/api/v4/projects?membership=true&simple=true&per_page={PAGE_SIZE}&order_by=last_activity_at&sort=desc&pagination=keyset"));
            return roots;
        }

        /// <summary>
        ///     Loads the children (subgroups, then projects) of a group node. `onItems` is invoked on a worker
        ///     thread as soon as each page arrives.
        /// </summary>
        public static async Task LoadChildrenAsync(GitProviderAccount account, GitProviderNode node, CancellationToken ct, Action<List<GitProviderNode>> onItems)
        {
            var isGitHub = account.Type == GitProviderType.GitHub;

            if (node.SubgroupsUrl != null)
            {
                var url = node.SubgroupsUrl;
                for (var page = 0; page < MAX_PAGES && url != null; page++)
                {
                    var (doc, next) = await GetJsonPageAsync(account, url, ct).ConfigureAwait(false);
                    url = next;

                    var groups = new List<GitProviderNode>();
                    using (doc)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                            groups.Add(GitLabGroup(account, ct, item, Str(item, "name")));
                    }

                    onItems(groups);
                }
            }

            if (node.ReposUrl != null)
            {
                var url = node.ReposUrl;
                for (var page = 0; page < MAX_PAGES && url != null; page++)
                {
                    var (doc, next) = await GetJsonPageAsync(account, url, ct).ConfigureAwait(false);
                    url = next;

                    var repos = new List<GitProviderNode>();
                    using (doc)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                            repos.Add(GitProviderNode.CreateRepo(account, ct, isGitHub ? GitHubRepo(item) : GitLabRepo(item)));
                    }

                    onItems(repos);
                }
            }
        }

        private static GitProviderNode GitHubOrg(GitProviderAccount account, CancellationToken ct, JsonElement item)
        {
            var login = Str(item, "login");
            return GitProviderNode.CreateGroup(account, ct, login, null,
                $"https://api.github.com/orgs/{Uri.EscapeDataString(login)}/repos?per_page={PAGE_SIZE}&sort=updated&type=all");
        }

        private static GitProviderNode GitLabGroup(GitProviderAccount account, CancellationToken ct, JsonElement item, string name)
        {
            var id = item.GetProperty("id").GetRawText();
            var api = $"{account.Host}/api/v4/groups/{id}";
            return GitProviderNode.CreateGroup(account, ct, name,
                $"{api}/subgroups?per_page={PAGE_SIZE}&order_by=name",
                $"{api}/projects?simple=true&per_page={PAGE_SIZE}&order_by=last_activity_at&sort=desc");
        }

        private static GitProviderRepo GitHubRepo(JsonElement item)
        {
            return new GitProviderRepo(
                Str(item, "full_name"),
                Str(item, "description"),
                item.TryGetProperty("private", out var p) && p.ValueKind == JsonValueKind.True,
                Str(item, "clone_url"),
                Str(item, "ssh_url"));
        }

        private static GitProviderRepo GitLabRepo(JsonElement item)
        {
            return new GitProviderRepo(
                Str(item, "path_with_namespace"),
                Str(item, "description"),
                Str(item, "visibility") != "public",
                Str(item, "http_url_to_repo"),
                Str(item, "ssh_url_to_repo"));
        }

        private static async Task<JsonDocument> GetJsonAsync(GitProviderAccount account, string url, CancellationToken ct)
        {
            var (doc, _) = await GetJsonPageAsync(account, url, ct).ConfigureAwait(false);
            return doc;
        }

        private static async Task<(JsonDocument, string)> GetJsonPageAsync(GitProviderAccount account, string url, CancellationToken ct)
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

            string next = null;
            if (resp.Headers.TryGetValues("Link", out var links))
            {
                foreach (var link in links)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(link, "<([^>]+)>;\\s*rel=\"next\"");
                    if (m.Success)
                        next = m.Groups[1].Value;
                }
            }

            var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return (doc, next);
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
