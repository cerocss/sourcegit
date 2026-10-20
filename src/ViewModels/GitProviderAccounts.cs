using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using Avalonia.Collections;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Stores logged-in Git provider accounts (GitHub / GitLab / self-hosted GitLab) in its own
    ///     file, and exposes their tokens to git (HTTPS) through process-level `GIT_CONFIG_*` variables,
    ///     so tokens are never written into any repository's config.
    /// </summary>
    public class GitProviderAccounts
    {
        public static GitProviderAccounts Instance { get; } = new();

        public AvaloniaList<Models.GitProviderAccount> Accounts { get; } = [];

        public void Add(Models.GitProviderAccount account)
        {
            Accounts.Add(account);
            Save();
            ApplyGitEnvironment();
        }

        public void Remove(Models.GitProviderAccount account)
        {
            Accounts.Remove(account);
            Save();
            ApplyGitEnvironment();
        }

        public void ApplyGitEnvironment()
        {
            if (!_baseCountCaptured)
            {
                _baseCountCaptured = true;
                int.TryParse(Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT"), out _baseCount);
            }

            var index = _baseCount;
            var seen = new HashSet<string>();
            foreach (var account in Accounts)
            {
                if (!seen.Add(account.Host))
                    continue; // First account for a host wins.

                var user = account.Type == Models.GitProviderType.GitHub ? "x-access-token" : "oauth2";
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{account.Token}"));
                Environment.SetEnvironmentVariable($"GIT_CONFIG_KEY_{index}", $"http.{account.Host}/.extraHeader");
                Environment.SetEnvironmentVariable($"GIT_CONFIG_VALUE_{index}", $"Authorization: Basic {basic}");
                index++;
            }

            for (var i = index; i < _appliedCount; i++)
            {
                Environment.SetEnvironmentVariable($"GIT_CONFIG_KEY_{i}", null);
                Environment.SetEnvironmentVariable($"GIT_CONFIG_VALUE_{i}", null);
            }

            _appliedCount = index;
            Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", index > 0 ? index.ToString() : null);
        }

        private GitProviderAccounts()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var list = JsonSerializer.Deserialize(File.ReadAllText(FilePath), Models.GitProviderJsonContext.Default.ListGitProviderAccount);
                    if (list != null)
                        Accounts.AddRange(list);
                }
            }
            catch
            {
                // Ignore a corrupted file.
            }
        }

        private void Save()
        {
            try
            {
                var content = JsonSerializer.Serialize(new List<Models.GitProviderAccount>(Accounts), Models.GitProviderJsonContext.Default.ListGitProviderAccount);
                if (!OperatingSystem.IsWindows() && !File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, string.Empty);
                    File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                File.WriteAllText(FilePath, content);
            }
            catch (Exception e)
            {
                Models.Notification.Send(null, $"Failed to save Git accounts: {e.Message}", true);
            }
        }

        private static string FilePath => Path.Combine(Native.OS.BasicDirectories.ConfigDir, "git_providers.json");

        private bool _baseCountCaptured = false;
        private int _baseCount = 0;
        private int _appliedCount = 0;
    }
}
