
using Microsoft.AppCenter.Analytics;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PingTool.Views
{
    public sealed partial class SuggestionPage : Page
    {
        public SuggestionPage()
        {
            InitializeComponent();
        }
        private void Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TxtName.Text))
                {
                    TxtThanksYou.Text = "Please enter the name.";
                    TxtThanksYou.Foreground = new SolidColorBrush(Colors.Red);
                    TxtThanksYou.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    return;
                }
                if (string.IsNullOrEmpty(TxtSuggestion.Text))
                {
                    TxtThanksYou.Text = "Please write your suggestion.";
                    TxtThanksYou.Foreground = new SolidColorBrush(Colors.Red);
                    TxtThanksYou.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    return;
                }
                Analytics.TrackEvent("Suggestion request", new Dictionary<string, string> { { "name", TxtName.Text }, { "email", TxtEmail.Text }, { "country", TxtCountry.Text }, { "suggestion", TxtSuggestion.Text } });
                TxtThanksYou.Text = "Thanks for your suggestion.";
                TxtThanksYou.Foreground = new SolidColorBrush(Colors.Green);
                TxtThanksYou.Visibility = Windows.UI.Xaml.Visibility.Visible;
                ClearText();
            }
            catch
            {
                TxtThanksYou.Text = "Something went wrong.";
                TxtThanksYou.Foreground = new SolidColorBrush(Colors.Red);
                TxtThanksYou.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
        }
        private void ClearText()
        {
            TxtName.Text = string.Empty;
            TxtEmail.Text = string.Empty;
            TxtCountry.Text = string.Empty;
            TxtSuggestion.Text = string.Empty;
        }
    }
}
