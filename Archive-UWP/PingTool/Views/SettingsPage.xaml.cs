using PingTool.Core.Helpers;
using PingTool.Core.Services;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace PingTool.Views
{
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private UserDataService UserDataService => Singleton<UserDataService>.Instance;
        private IdentityService IdentityService => Singleton<IdentityService>.Instance;
        private ElementTheme _elementTheme = ThemeSelectorService.Theme;
        private bool _isLoggedIn;
        private bool _isBusy;
        private UserData _user;
        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }
            set { Set(ref _elementTheme, value); }
        }
        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }
            set { Set(ref _versionDescription, value); }
        }
        public bool IsLoggedIn
        {
            get { return _isLoggedIn; }
            set { Set(ref _isLoggedIn, value); }
        }
        public bool IsBusy
        {
            get { return _isBusy; }
            set { Set(ref _isBusy, value); }
        }
        public UserData User
        {
            get { return _user; }
            set { Set(ref _user, value); }
        }
        public SettingsPage()
        {
            InitializeComponent();
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeAsync();
            AutoStartPing.IsOn = await ApplicationData.Current.LocalSettings.ReadAsync<bool>("IsPingAutoStart");
        }
        private async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            IdentityService.LoggedIn += OnLoggedIn;
            IdentityService.LoggedOut += OnLoggedOut;
            UserDataService.UserDataUpdated += OnUserDataUpdated;
            IsLoggedIn = IdentityService.IsLoggedIn();
            User = await UserDataService.GetUserAsync();
        }

        private string GetVersionDescription()
        {
            var appName = "AppDisplayName".GetLocalized();
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{appName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private async void ThemeChanged_CheckedAsync(object sender, RoutedEventArgs e)
        {
            var param = (sender as RadioButton)?.CommandParameter;
            if (param != null)
            {
                await ThemeSelectorService.SetThemeAsync((ElementTheme)param);
            }
        }

        private void OnUserDataUpdated(object sender, UserData userData)
        {
            User = userData;
        }

        private async void OnLogIn(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            var loginResult = await IdentityService.LoginAsync();
            if (loginResult != LoginResultType.Success)
            {
                await AuthenticationHelper.ShowLoginErrorAsync(loginResult);
                IsBusy = false;
            }
        }

        private async void OnLogOut(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            await IdentityService.LogoutAsync();
        }

        private void OnLoggedIn(object sender, EventArgs e)
        {
            IsLoggedIn = true;
            IsBusy = false;
        }

        private void OnLoggedOut(object sender, EventArgs e)
        {
            User = null;
            IsLoggedIn = false;
            IsBusy = false;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            IdentityService.LoggedIn -= OnLoggedIn;
            IdentityService.LoggedOut -= OnLoggedOut;
            UserDataService.UserDataUpdated -= OnUserDataUpdated;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }
            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private async void ToggleSwitch_ToggledAsync(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                await ApplicationData.Current.LocalSettings.SaveAsync("IsPingAutoStart", toggleSwitch.IsOn);
            }
        }
    }
}
