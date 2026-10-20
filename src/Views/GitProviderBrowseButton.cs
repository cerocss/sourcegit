using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    /// <summary>
    ///     Button for the Clone dialog: pick a repository from the signed-in Git provider accounts.
    ///     Self-contained so that the Clone view-model and code-behind stay untouched.
    /// </summary>
    public class GitProviderBrowseButton : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        public GitProviderBrowseButton()
        {
            Classes.Add("flat");
            Height = 24;
            Margin = new Avalonia.Thickness(16, 0, 0, 0);
            Content = "Browse accounts...";
            ToolTip.SetTip(this, "Choose a repository from your GitHub / GitLab accounts");
        }

        protected override async void OnClick()
        {
            base.OnClick();

            if (DataContext is not ViewModels.Clone vm)
                return;

            if (ViewModels.GitProviderAccounts.Instance.Accounts.Count == 0)
            {
                Models.Notification.Send(null, "No Git accounts configured. Add one in Preferences > Git Accounts.", true);
                return;
            }

            var picker = new GitProviderRepoPicker();
            await this.ShowDialogAsync(picker);

            var repo = picker.Result;
            if (repo != null)
                vm.Remote = repo.HttpsUrl; // Use the protocol switcher for SSH.
        }
    }
}
