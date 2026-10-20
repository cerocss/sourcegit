using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class GitProviderRepoPicker : ChromelessWindow
    {
        public Models.GitProviderRepo Result { get; private set; } = null;

        public GitProviderRepoPicker()
        {
            CloseOnESC = true;
            InitializeComponent();

            CmbAccount.ItemsSource = ViewModels.GitProviderAccounts.Instance.Accounts;
            if (CmbAccount.ItemCount > 0)
                CmbAccount.SelectedIndex = 0;
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            base.OnClosed(e);
        }

        private async void OnAccountChanged(object _, SelectionChangedEventArgs e)
        {
            if (CmbAccount.SelectedItem is not Models.GitProviderAccount account)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _repos = [];
            ApplyFilter();
            TxtStatus.Text = "Loading repositories...";

            try
            {
                var repos = await Models.GitProviderClient.ListReposAsync(account, token);
                if (token.IsCancellationRequested)
                    return;

                _repos = repos;
                TxtStatus.Text = repos.Count == 0 ? "No repositories found." : string.Empty;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    TxtStatus.Text = $"Failed to load repositories: {ex.Message}";
            }
        }

        private void OnFilterChanged(object _, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = TxtFilter.Text?.Trim() ?? string.Empty;
            RepoList.ItemsSource = filter.Length == 0
                ? _repos
                : _repos.Where(r => r.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void OnRepoSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            BtnSelect.IsEnabled = RepoList.SelectedItem != null;
        }

        private void OnRepoDoubleTapped(object _, Avalonia.Input.TappedEventArgs e)
        {
            OnSelect(null, null);
        }

        private void OnSelect(object _, RoutedEventArgs e)
        {
            if (RepoList.SelectedItem is not Models.GitProviderRepo repo)
                return;

            Result = repo;
            Close();
        }

        private void OnCancel(object _, RoutedEventArgs e)
        {
            Close();
        }

        private List<Models.GitProviderRepo> _repos = [];
        private CancellationTokenSource _cts = null;
    }
}
