using PingTool.ViewModels;

using Windows.UI.Xaml.Controls;

namespace PingTool.Views
{
    public sealed partial class HistoryPage : Page
    {
        private HistoryViewModel ViewModel;
        public HistoryPage()
        {
            InitializeComponent();
            ViewModel = new HistoryViewModel();
            this.DataContext = ViewModel;
        }
    }
}
