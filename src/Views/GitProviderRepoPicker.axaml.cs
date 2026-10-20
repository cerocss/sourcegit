using System;
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
            _cts?.Cancel(); // Also cancels every pending lazy load of the tree.
            base.OnClosed(e);
        }

        private async void OnAccountChanged(object _, SelectionChangedEventArgs e)
        {
            if (CmbAccount.SelectedItem is not Models.GitProviderAccount account)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            RepoTree.ItemsSource = null;
            TxtStatus.Text = "Loading groups...";
            PbLoading.IsVisible = true;

            try
            {
                var roots = await Models.GitProviderClient.ListRootsAsync(account, token);
                if (token.IsCancellationRequested)
                    return;

                TxtStatus.Text = string.Empty;
                PbLoading.IsVisible = false;
                RepoTree.ItemsSource = roots;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    PbLoading.IsVisible = false;
                    TxtStatus.Text = $"Failed to load groups: {ex.Message}";
                }
            }
        }

        private void OnTreeSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            BtnSelect.IsEnabled = RepoTree.SelectedItem is Models.GitProviderNode { Repo: not null };
        }

        private void OnTreeDoubleTapped(object _, Avalonia.Input.TappedEventArgs e)
        {
            if (RepoTree.SelectedItem is Models.GitProviderNode { Repo: not null })
                OnSelect(null, null);
        }

        private void OnSelect(object _, RoutedEventArgs e)
        {
            if (RepoTree.SelectedItem is not Models.GitProviderNode { Repo: not null } node)
                return;

            Result = node.Repo;
            Close();
        }

        private void OnCancel(object _, RoutedEventArgs e)
        {
            Close();
        }

        private CancellationTokenSource _cts = null;
    }
}
