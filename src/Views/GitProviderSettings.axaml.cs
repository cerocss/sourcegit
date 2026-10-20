using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class GitProviderSettings : UserControl
    {
        public GitProviderSettings()
        {
            DataContext = ViewModels.GitProviderAccounts.Instance;
            InitializeComponent();
        }

        private void OnProviderChanged(object _, SelectionChangedEventArgs e)
        {
            if (HostPanel != null)
                HostPanel.IsVisible = CmbProvider.SelectedIndex == (int)Models.GitProviderType.GitLabSelfHosted;
        }

        private void OnRemoveAccount(object _, RoutedEventArgs e)
        {
            if (AccountList.SelectedItem is Models.GitProviderAccount account)
                ViewModels.GitProviderAccounts.Instance.Remove(account);

            e.Handled = true;
        }

        private async void OnAddAccount(object _, RoutedEventArgs e)
        {
            e.Handled = true;

            var type = (Models.GitProviderType)CmbProvider.SelectedIndex;
            var host = Models.GitProviderAccount.NormalizeHost(type, TxtHost.Text);
            var token = TxtToken.Text?.Trim();

            if (!System.Uri.TryCreate(host, System.UriKind.Absolute, out System.Uri _))
            {
                TxtStatus.Text = "Please enter a valid server URL.";
                return;
            }

            if (string.IsNullOrEmpty(token))
            {
                TxtStatus.Text = "Please enter an access token.";
                return;
            }

            var account = new Models.GitProviderAccount() { Type = type, Host = host, Token = token };
            TxtStatus.Text = "Signing in...";

            try
            {
                account.Username = await Models.GitProviderClient.GetUsernameAsync(account);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Sign in failed: {ex.Message}";
                return;
            }

            var accounts = ViewModels.GitProviderAccounts.Instance;
            var existing = accounts.Accounts.FirstOrDefault(a => a.Host == host && a.Username == account.Username);
            if (existing != null)
                accounts.Remove(existing);

            accounts.Add(account);
            TxtToken.Text = string.Empty;
            TxtStatus.Text = $"Signed in as {account.Username}.";
        }
    }
}
