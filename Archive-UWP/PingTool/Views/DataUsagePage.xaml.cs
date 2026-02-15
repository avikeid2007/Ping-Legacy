using PingTool.ViewModels;

using Windows.UI.Xaml.Controls;

namespace PingTool.Views
{
    public sealed partial class DataUsagePage : Page
    {
        private DataUsageViewModel ViewModel;
        public DataUsagePage()
        {
            InitializeComponent();
            ViewModel = new DataUsageViewModel();
            this.DataContext = ViewModel;
        }
    }
}
