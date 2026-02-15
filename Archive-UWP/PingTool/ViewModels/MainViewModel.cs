using BasicMvvm;
using BasicMvvm.Commands;
using PingTool.Helpers;
using PingTool.Models;
using PingTool.Services;
using PingTool.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace PingTool
{
    public class MainViewModel : BindableBase
    {
        private string _ipAddress;
        private string _profileName;
        private bool _isWlan;
        private long _totalReceivedBytes;
        private long _totalSentBytes;
        private bool _isSupportIPV6;
        private int _wifiBars;
        private string _hostNameOrAddress;
        private string _ipType;
        private ObservableCollection<NetworkInterface> _localLANCollection;
        private NetworkInterface _selectedLocalLAN;
        private bool _hasInternetAccess;
        private bool? _isPingAutoStart;
        private readonly string _urlRegex = @"^(http|https|ftp|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
        private readonly string _ipRegex = @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}";
        public bool HasInternetAccess
        {
            get { return _hasInternetAccess; }
            set
            {
                _hasInternetAccess = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<NetworkInterface> LocalLANCollection
        {
            get { return _localLANCollection; }
            set
            {
                _localLANCollection = value;
                OnPropertyChanged();
            }
        }
        public NetworkInterface SelectedLocalLAN
        {
            get { return _selectedLocalLAN; }
            set
            {
                _selectedLocalLAN = value;
                OnPropertyChanged();
            }
        }
        public string IpType
        {
            get { return _ipType; }
            set
            {
                _ipType = value;
                OnPropertyChanged();
            }
        }
        public string HostNameOrAddress
        {
            get { return _hostNameOrAddress; }
            set
            {
                _hostNameOrAddress = value;
                if (IsValidHostNameOrAddress(value))
                {
                    _ = ApplicationData.Current.LocalSettings.SaveAsync(nameof(HostNameOrAddress), value);
                }
                OnPropertyChanged();
            }
        }
        public long TotalReceivedBytes
        {
            get { return _totalReceivedBytes; }
            set
            {
                _totalReceivedBytes = value;
                OnPropertyChanged();
            }
        }
        public long TotalSentBytes
        {
            get { return _totalSentBytes; }
            set
            {
                _totalSentBytes = value;
                OnPropertyChanged();
            }
        }
        public bool IsSupportIPV6
        {
            get { return _isSupportIPV6; }
            set
            {
                _isSupportIPV6 = value;
                OnPropertyChanged();
            }
        }
        public int WifiBars
        {
            get { return _wifiBars; }
            set
            {
                _wifiBars = value;
                OnPropertyChanged();
            }
        }
        public bool IsWlan
        {
            get { return _isWlan; }
            set
            {
                _isWlan = value;
                OnPropertyChanged();
            }
        }

        public string IpAddress
        {
            get { return _ipAddress; }
            set
            {
                _ipAddress = value;
                OnPropertyChanged();
            }
        }
        public string ProfileName
        {
            get { return _profileName; }
            set
            {
                _profileName = value;
                OnPropertyChanged();
            }
        }
        private bool _isPingStarted;
        public bool IsPingStarted
        {
            get { return _isPingStarted; }
            set
            {
                _isPingStarted = value;
                OnPropertyChanged();
            }
        }
        private bool _isCompactMode;
        public bool IsCompactMode
        {
            get { return _isCompactMode; }
            set
            {
                _isCompactMode = value;
                OnPropertyChanged();
            }
        }
        public ICommand StartCommand => new AsyncCommand(OnStartCommandExecutedAsync);
        public ICommand ClearCommand => new DelegateCommand(OnClearCommandExecuted);
        public ICommand CopyCommand => new DelegateCommand(OnCopyCommandExecuted);
        public ICommand ExportCommand => new AsyncCommand(OnExportCommandExecuteAsync);
        public ICommand KeyDownCommand => new AsyncCommand<KeyRoutedEventArgs>(OnKeyDownCommandExecuteAsync);
        public ICommand CompactCommand => new AsyncCommand(OnCompactCommandExecutedAsync);
        public async Task OnNavigatedToAsync(NavigationEventArgs _)
        {
            _isPingAutoStart = await ApplicationData.Current.LocalSettings.ReadAsync<bool?>("IsPingAutoStart");
            if (_isPingAutoStart == null)
            {
                _isPingAutoStart = true;
                await ApplicationData.Current.LocalSettings.SaveAsync("IsPingAutoStart", true);
            }
            var address = await ApplicationData.Current.LocalSettings.ReadAsync<string>(nameof(HostNameOrAddress));
            if (string.IsNullOrEmpty(address))
            {
                HostNameOrAddress = "172.217.166.238";
            }
            else
            {
                HostNameOrAddress = address;
            }
            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                App.AppServiceConnected += MainPage_AppServiceConnected;
                App.AppServiceDisconnected += MainPage_AppServiceDisconnected;
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
            await SetNetworkInfoAsync();
            NetworkInformation.NetworkStatusChanged -= NetworkInformation_NetworkStatusChangedAsync;
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChangedAsync;
            if (_isPingAutoStart != null && _isPingAutoStart.Value)
            {
                await Task.Delay(2000);
                await OnStartCommandExecutedAsync();
            }
        }
        private async Task NotifyUIAsync(Action action)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                               () => action());
        }
        private async Task SetNetworkInfoAsync()
        {
            try
            {
                var icp = NetworkInformation.GetInternetConnectionProfile();
                await NotifyUIAsync(() => LocalLANCollection = new ObservableCollection<NetworkInterface>(NetworkInterface.GetAllNetworkInterfaces()));
                if (icp?.NetworkAdapter == null)
                {
                    await NotifyUIAsync(() => ClearNetworkStatus());
                    return;
                }
                await NotifyUIAsync(() => HasInternetAccess = icp.GetNetworkConnectivityLevel() >= NetworkConnectivityLevel.InternetAccess);
                var hostname = NetworkInformation.GetHostNames()
                        .FirstOrDefault(
                            hn => hn.IPInformation?.NetworkAdapter != null
                            && hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId);
                var CurrentNetworkId = "{" + hostname.IPInformation.NetworkAdapter.NetworkAdapterId.ToString().ToUpper() + "}";
                await NotifyUIAsync(() => SelectedLocalLAN = LocalLANCollection.FirstOrDefault(x => x.Id.ToUpper() == CurrentNetworkId));
                await NotifyUIAsync(() => IpAddress = hostname?.CanonicalName);
                await NotifyUIAsync(() => IpType = hostname?.Type.ToString());
                await NotifyUIAsync(() => ProfileName = icp.ProfileName);
                await NotifyUIAsync(() => IsWlan = icp.IsWlanConnectionProfile);
                await NotifyUIAsync(() => WifiBars = Convert.ToInt32(icp.GetSignalBars()));
                var ipstats = SelectedLocalLAN.GetIPStatistics();
                await NotifyUIAsync(() => TotalReceivedBytes = GetSizeInByte(ipstats.BytesReceived));
                await NotifyUIAsync(() => TotalSentBytes = GetSizeInByte(ipstats.BytesSent));
                await NotifyUIAsync(() => IsSupportIPV6 = SelectedLocalLAN.Supports(NetworkInterfaceComponent.IPv6));
            }
            catch
            {
            }
        }
        private void ClearNetworkStatus()
        {
            IpAddress = string.Empty;
            IpType = string.Empty;
            ProfileName = string.Empty;
            HasInternetAccess = false;
        }
        private async void NetworkInformation_NetworkStatusChangedAsync(object sender)
        {
            await SetNetworkInfoAsync();
        }
        private long GetSizeInByte(long bytes)
        {
            return bytes / 1048576;
        }
        private void MainPage_AppServiceConnected(object sender, AppServiceTriggerDetails e)
        {
            App.Connection.RequestReceived += AppServiceConnection_RequestReceivedAsync;
        }
        private void MainPage_AppServiceDisconnected(object sender, EventArgs e)
        {
            //sdf
        }
        private async void AppServiceConnection_RequestReceivedAsync(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var reply = new PingMassage
            {
                PingId = _pingId,
                Date = _pingDate,
                IpAddress = Convert.ToString(args.Request.Message["host"]),
                Time = Convert.ToInt64(args.Request.Message["time"]),
                Size = Convert.ToInt32(args.Request.Message["size"]),
                Ttl = Convert.ToInt32(args.Request.Message["ttl"]),
                Response = Convert.ToString(args.Request.Message["response"]),
            };
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                () => (PingCollaction ?? (PingCollaction = new ObservableCollection<PingMassage>())).Add(reply));
            await NotifyUIAsync(() => SQLiteHelper.Save(reply));
        }
        private async Task OnStartCommandExecutedAsync()
        {
            ValueSet request;
            if (!IsPingStarted)
            {
                if (IsValidHostNameOrAddress(HostNameOrAddress))
                {
                    request = new ValueSet
                    {
                        { "host", HostNameOrAddress.Replace("http://", "").Replace("https://", "").TrimEnd('/') },
                        { "isStop", "false" }
                    };
                    _pingId = Guid.NewGuid();
                    _pingDate = DateTimeOffset.Now;
                    await App.Connection.SendMessageAsync(request);
                    IsPingStarted = true;
                    await DeleteOlderHistoryAsync();
                }
                else
                {
                    await new MessageDialog("Invalid Host Name or Address").ShowAsync();
                }
            }
            else
            {
                request = new ValueSet
                {
                    { "host", "" },
                    { "isStop", "false" }
                };
                await App.Connection.SendMessageAsync(request);
                IsPingStarted = false;
            }
        }

        private async Task DeleteOlderHistoryAsync()
        {
            try
            {
                await NotifyUIAsync(() => SQLiteHelper.DeleteOld(9));
            }
            catch
            { }
        }

        private bool IsValidHost(string url, string pattern)
        {
            var reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return reg.IsMatch(url);
        }
        private bool IsValidHostNameOrAddress(string hostNameOrAddress)
        {
            return !string.IsNullOrEmpty(hostNameOrAddress) && (IsValidHost(hostNameOrAddress, _ipRegex) || IsValidHost(hostNameOrAddress, _urlRegex));
        }
        private void OnClearCommandExecuted()
        {
            PingCollaction?.Clear();
        }
        private async Task OnKeyDownCommandExecuteAsync(KeyRoutedEventArgs obj)
        {
            if (obj.Key == VirtualKey.Enter)
            {
                await OnStartCommandExecutedAsync();
            }
        }
        private async Task OnExportCommandExecuteAsync()
        {
            if (PingCollaction?.Count > 0)
            {
                var text = string.Empty;
                PingCollaction.ToList().ForEach(x => text += x.Response + "\r");
                await FileHelper.SaveFileAsync(text, "ping.txt");
            }
        }
        private async Task OnCompactCommandExecutedAsync()
        {
            if (!IsCompactMode)
            {
                var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                preferences.CustomSize = new Windows.Foundation.Size(500, 500);
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, preferences);
                NavigationService.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                IsCompactMode = true;
                if (!IsPingStarted)
                {
                    await OnStartCommandExecutedAsync();
                }
            }
            else
            {
                var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.Default);
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default, preferences);
                NavigationService.GoBack();
                IsCompactMode = false;
            }


        }
        private void OnCopyCommandExecuted()
        {
            FileHelper.CopyText(IpAddress ?? "");
        }
        private ObservableCollection<PingMassage> _pingCollaction;
        private Guid _pingId;
        private DateTimeOffset _pingDate;

        public ObservableCollection<PingMassage> PingCollaction
        {
            get { return _pingCollaction; }
            set
            {
                _pingCollaction = value;
                OnPropertyChanged();
            }
        }
        public bool IsPingStop { get; set; }
        public async Task StartPingAsync()
        {
            ValueSet request = new ValueSet
            {
                { "host", "8.8.8.8" },
                { "isStop", "false" }
            };
            await App.Connection.SendMessageAsync(request);
        }

        internal async Task OnNavigatedFromAsync(NavigationEventArgs e)
        {
            if (IsPingStarted)
                await OnStartCommandExecutedAsync();
        }
    }
}
