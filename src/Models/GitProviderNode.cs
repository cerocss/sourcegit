using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    /// <summary>
    ///     A node of the "clone from provider" tree: either a group / organization (lazy-loaded when
    ///     expanded) or a repository leaf.
    /// </summary>
    public class GitProviderNode : ObservableObject
    {
        public string Name { get; init; }
        public GitProviderRepo Repo { get; init; } = null;
        public string SubgroupsUrl { get; init; } = null;
        public string ReposUrl { get; init; } = null;
        public bool IsPlaceholder { get; init; } = false;
        public bool IsGroup => Repo == null && !IsPlaceholder;
        public bool IsPrivate => Repo is { IsPrivate: true };
        public string Description => Repo?.Description ?? string.Empty;
        public AvaloniaList<GitProviderNode> Children { get; } = [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value && IsGroup && !_loadStarted)
                    _ = LoadAsync();
            }
        }

        public GitProviderNode(GitProviderAccount account, CancellationToken ct)
        {
            _account = account;
            _ct = ct;
        }

        public static GitProviderNode CreateGroup(GitProviderAccount account, CancellationToken ct, string name, string subgroupsUrl, string reposUrl)
        {
            var node = new GitProviderNode(account, ct) { Name = name, SubgroupsUrl = subgroupsUrl, ReposUrl = reposUrl };
            node.Children.Add(Placeholder(account, ct, "Loading...")); // Makes the expander visible.
            return node;
        }

        public static GitProviderNode CreateRepo(GitProviderAccount account, CancellationToken ct, GitProviderRepo repo)
        {
            return new GitProviderNode(account, ct) { Name = repo.FullName, Repo = repo };
        }

        private static GitProviderNode Placeholder(GitProviderAccount account, CancellationToken ct, string text)
        {
            return new GitProviderNode(account, ct) { Name = text, IsPlaceholder = true };
        }

        private async Task LoadAsync()
        {
            _loadStarted = true;
            var first = true; // Only touched on the UI thread.

            void Add(List<GitProviderNode> items)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_ct.IsCancellationRequested)
                        return;

                    if (first)
                    {
                        Children.Clear();
                        first = false;
                    }

                    Children.AddRange(items);
                });
            }

            try
            {
                await GitProviderClient.LoadChildrenAsync(_account, this, _ct, Add).ConfigureAwait(false);
                Dispatcher.UIThread.Post(() =>
                {
                    if (first && !_ct.IsCancellationRequested)
                    {
                        Children.Clear();
                        Children.Add(Placeholder(_account, _ct, "(empty)"));
                    }
                });
            }
            catch (Exception e)
            {
                if (_ct.IsCancellationRequested)
                    return;

                Dispatcher.UIThread.Post(() =>
                {
                    Children.Clear();
                    Children.Add(Placeholder(_account, _ct, $"Failed: {e.Message}"));
                    _loadStarted = false; // Collapse and expand again to retry.
                });
            }
        }

        private readonly GitProviderAccount _account;
        private readonly CancellationToken _ct;
        private bool _isExpanded = false;
        private bool _loadStarted = false;
    }
}
