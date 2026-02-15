using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PingTool.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel ViewModel;
        public MainPage()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.OnNavigatedToAsync(e);
            base.OnNavigatedTo(e);
        }
        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            await ViewModel.OnNavigatedFromAsync(e);
            base.OnNavigatedFrom(e);
        }

    }
}
