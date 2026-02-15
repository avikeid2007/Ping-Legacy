using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PingTool.Helpers;
using System.Text;
using Windows.ApplicationModel;
using Windows.System;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PingTool.Views;

public sealed partial class SuggestionPage : Page
{
    private const string GitHubRepoUrl = "https://github.com/avikeid2007/Ping-Legacy";
    private const string SupportEmail = "avnish@ymail.com"; // Update with your actual support email

    public SuggestionPage()
    {
        InitializeComponent();
    }

    private async void OpenGitHubIssue_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFeedback())
            return;

        var title = TitleText.Text.Trim();
        var description = DescriptionText.Text.Trim();

        // Build issue body
        var body = new StringBuilder();
        body.AppendLine("## Description");
        body.AppendLine(string.IsNullOrWhiteSpace(description) ? "_No description provided_" : description);
        body.AppendLine();

        if (IncludeSystemInfo.IsChecked == true)
        {
            body.AppendLine("## System Information");
            body.AppendLine($"- **App Version**: {GetAppVersion()}");
            body.AppendLine($"- **OS**: {Environment.OSVersion}");
            body.AppendLine($"- **.NET Version**: {Environment.Version}");
            body.AppendLine($"- **Machine**: {Environment.MachineName}");
            body.AppendLine();
        }

        // Determine label based on feedback type
        var labels = new List<string>();
        if (FeatureRadio.IsChecked == true) labels.Add("enhancement");
        else if (BugRadio.IsChecked == true) labels.Add("bug");
        else if (QuestionRadio.IsChecked == true) labels.Add("question");

        // Build GitHub new issue URL
        var issueUrl = $"{GitHubRepoUrl}/issues/new?" +
                      $"title={Uri.EscapeDataString(title)}" +
                      $"&body={Uri.EscapeDataString(body.ToString())}" +
                      (labels.Count > 0 ? $"&labels={string.Join(",", labels)}" : "");

        await Launcher.LaunchUriAsync(new Uri(issueUrl));

        // Clear form
        TitleText.Text = string.Empty;
        DescriptionText.Text = string.Empty;
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFeedback())
            return;

        var content = BuildFeedbackText();
        FileHelper.CopyText(content);

        _ = ShowSuccessDialog("Copied to Clipboard", "Feedback has been copied to your clipboard.");
    }

    private async void SendEmail_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFeedback())
            return;

        var title = TitleText.Text.Trim();
        var feedbackType = GetFeedbackType();
        var subject = $"[{feedbackType}] {title}";
        var body = BuildEmailBody();

        // Create mailto URL
        var mailtoUrl = $"mailto:{SupportEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";

        try
        {
            await Launcher.LaunchUriAsync(new Uri(mailtoUrl));
        }
        catch (Exception ex)
        {
            await ShowDialog("Error", $"Could not open email client: {ex.Message}");
        }
    }

    private async void ExportToFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFeedback())
            return;

        var savePicker = new FileSavePicker();

        // Get the window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("Text File", new List<string>() { ".txt" });
        savePicker.FileTypeChoices.Add("Markdown File", new List<string>() { ".md" });

        var feedbackType = GetFeedbackType();
        var sanitizedTitle = string.Join("_", TitleText.Text.Trim().Split(Path.GetInvalidFileNameChars()));
        savePicker.SuggestedFileName = $"{feedbackType}_{sanitizedTitle}_{DateTime.Now:yyyyMMdd}";

        StorageFile file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            try
            {
                var content = BuildFeedbackText();
                await FileIO.WriteTextAsync(file, content);

                await ShowSuccessDialog("Export Successful", $"Feedback has been saved to:\n{file.Path}");
            }
            catch (Exception ex)
            {
                await ShowDialog("Export Failed", $"Could not save file: {ex.Message}");
            }
        }
    }

    private bool ValidateFeedback()
    {
        var title = TitleText.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            _ = ShowDialog("Title Required", "Please enter a title for your feedback.");
            return false;
        }

        return true;
    }

    private string GetFeedbackType()
    {
        if (FeatureRadio.IsChecked == true) return "Feature Request";
        if (BugRadio.IsChecked == true) return "Bug Report";
        if (QuestionRadio.IsChecked == true) return "Question";
        return "Feedback";
    }

    private string BuildEmailBody()
    {
        var sb = new StringBuilder();

        sb.AppendLine("DESCRIPTION");
        sb.AppendLine("─────────────────────────────────────");
        sb.AppendLine(string.IsNullOrWhiteSpace(DescriptionText.Text) ? "No description provided" : DescriptionText.Text);
        sb.AppendLine();

        if (IncludeSystemInfo.IsChecked == true)
        {
            sb.AppendLine("SYSTEM INFORMATION");
            sb.AppendLine("─────────────────────────────────────");
            sb.AppendLine($"App Version: {GetAppVersion()}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($".NET Version: {Environment.Version}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine();
        }

        sb.AppendLine("─────────────────────────────────────");
        sb.AppendLine("This feedback was generated by Ping Legacy");

        return sb.ToString();
    }

    private string BuildFeedbackText()
    {
        var sb = new StringBuilder();
        
        var feedbackType = FeatureRadio.IsChecked == true ? "Feature Request" :
                          BugRadio.IsChecked == true ? "Bug Report" : "Question";
        
        sb.AppendLine($"# {feedbackType}: {TitleText.Text}");
        sb.AppendLine();
        sb.AppendLine("## Description");
        sb.AppendLine(DescriptionText.Text);
        sb.AppendLine();

        if (IncludeSystemInfo.IsChecked == true)
        {
            sb.AppendLine("## System Information");
            sb.AppendLine($"- App Version: {GetAppVersion()}");
            sb.AppendLine($"- OS: {Environment.OSVersion}");
            sb.AppendLine($"- .NET Version: {Environment.Version}");
        }

        return sb.ToString();
    }

    private static string GetAppVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task ShowDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowSuccessDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }
}
