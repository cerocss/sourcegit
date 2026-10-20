using System;
using System.Threading;

using Avalonia.Collections;
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

            RepoTree.ItemsSource = _roots;
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

            // Keep the current tree on screen (dimmed) until the new roots are ready to avoid flicker.
            RepoTree.IsEnabled = false;
            PbLoading.IsVisible = true;

            try
            {
                var roots = await Models.GitProviderClient.ListRootsAsync(account, token);
                if (token.IsCancellationRequested)
                    return;

                _account = account;
                _token = token;
                _allNode = null;
                if (ChkAll.IsChecked == true)
                {
                    _allNode = Models.GitProviderClient.CreateAllProjectsNode(account, token);
                    roots.Add(_allNode);
                }

                _roots = new AvaloniaList<Models.GitProviderNode>(roots);
                RepoTree.ItemsSource = _roots; // Single swap, no intermediate empty state.
                TxtStatus.Text = string.Empty;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _roots = [];
                    RepoTree.ItemsSource = _roots;
                    TxtStatus.Text = $"Failed to load groups: {ex.Message}";
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    PbLoading.IsVisible = false;
                    RepoTree.IsEnabled = true;
                }
            }
        }

        private void OnShowAllChanged(object _, RoutedEventArgs e)
        {
            if (_account == null)
                return;

            if (ChkAll.IsChecked == true)
            {
                if (_allNode == null)
                {
                    _allNode = Models.GitProviderClient.CreateAllProjectsNode(_account, _token);
                    _roots.Add(_allNode);
                }
            }
            else if (_allNode != null)
            {
                _roots.Remove(_allNode);
                _allNode = null;
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

        private AvaloniaList<Models.GitProviderNode> _roots = [];
        private Models.GitProviderAccount _account = null;
        private CancellationToken _token = CancellationToken.None;
        private Models.GitProviderNode _allNode = null;
        private CancellationTokenSource _cts = null;
    }
}
