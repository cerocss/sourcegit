using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    public enum GitProviderType
    {
        GitHub,
        GitLab,
        GitLabSelfHosted,
    }

    public class GitProviderAccount
    {
        public GitProviderType Type { get; set; } = GitProviderType.GitHub;
        public string Host { get; set; } = string.Empty; // Base URL, e.g. https://github.com
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                var kind = Type == GitProviderType.GitHub ? "GitHub" : "GitLab";
                var uri = new Uri(Host);
                return $"{kind} - {Username} ({uri.Host})";
            }
        }

        public static string NormalizeHost(GitProviderType type, string host)
        {
            switch (type)
            {
                case GitProviderType.GitHub:
                    return "https://github.com";
                case GitProviderType.GitLab:
                    return "https://gitlab.com";
            }

            host = (host ?? string.Empty).Trim().TrimEnd('/');
            if (host.Length == 0)
                return host;
            if (!host.Contains("://", StringComparison.Ordinal))
                host = "https://" + host;
            return host;
        }
    }

    public record GitProviderRepo(string FullName, string Description, bool IsPrivate, string HttpsUrl, string SshUrl);

    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(List<GitProviderAccount>))]
    internal partial class GitProviderJsonContext : JsonSerializerContext { }
}
