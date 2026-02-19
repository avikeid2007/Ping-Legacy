using BasicMvvm;
using PingTool.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace PingTool.ViewModels
{
    class DataUsageViewModel : BindableBase
    {
        private ObservableCollection<DataUse> _networkUsageCollection;
        private ObservableCollection<NetworkDataUse> _totalUsageCollection;
        private ObservableCollection<NetworkDataUse> _todayUsageCollection;
        private NetworkDataUse _todayUpload;
        private NetworkDataUse _todayDownload;
        private NetworkDataUse _totalUpload;
        private NetworkDataUse _totalDownload;

        public NetworkDataUse TodayUpload
        {
            get { return _todayUpload; }
            set
            {
                _todayUpload = value;
                OnPropertyChanged();
            }
        }
        public NetworkDataUse TodayDownload
        {
            get { return _todayDownload; }
            set
            {
                _todayDownload = value;
                OnPropertyChanged();
            }
        }
        public NetworkDataUse TotalUpload
        {
            get { return _totalUpload; }
            set
            {
                _totalUpload = value;
                OnPropertyChanged();
            }
        }
        public NetworkDataUse TotalDownload
        {
            get { return _totalDownload; }
            set
            {
                _totalDownload = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<NetworkDataUse> TotalUsageCollection
        {
            get { return _totalUsageCollection; }
            set
            {
                _totalUsageCollection = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<NetworkDataUse> TodayUsageCollection
        {
            get { return _todayUsageCollection; }
            set
            {
                _todayUsageCollection = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<DataUse> NetworkUsageCollection
        {
            get { return _networkUsageCollection; }
            set
            {
                _networkUsageCollection = value;
                OnPropertyChanged();
            }
        }
        public DataUsageViewModel()
        {
            Task.WhenAll(GetDailyDataUsegesAsync(), GeTotalDataUsegesAsync(), GeTodayDataUsegesAsync());
        }
        private async Task GetDailyDataUsegesAsync()
        {
            NetworkUsageCollection = new ObservableCollection<DataUse>();
            for (int i = -10; i <= -1; i++)
            {
                var usages = await GetDataUsegesAsync(DateTime.Now.AddDays(i).Date, DateTime.Now.AddDays(i + 1).Date, NetworkInformation.GetInternetConnectionProfile());
                if (usages != null)
                {
                    NetworkUsageCollection.Add(new DataUse { Date = DateTime.Now.AddDays(i).Date, Download = GetSizeInByte(usages.BytesReceived), Upload = GetSizeInByte(usages.BytesSent), ConnectionDuration = usages.ConnectionDuration });
                }
            }
        }
        private async Task GeTotalDataUsegesAsync()
        {
            TotalUsageCollection = new ObservableCollection<NetworkDataUse>();
            var usages = await GetDataUsegesAsync(DateTime.Now.AddDays(-10).Date, DateTime.Now.Date, NetworkInformation.GetInternetConnectionProfile());
            if (usages != null)
            {
                TotalDownload = new NetworkDataUse { DataType = "Download", DataUse = GetSizeInByte(usages.BytesReceived) };
                TotalUpload = new NetworkDataUse { DataType = "Upload", DataUse = GetSizeInByte(usages.BytesSent) };
                TotalUsageCollection.Add(TotalDownload);
                TotalUsageCollection.Add(TotalUpload);
            }
        }
        private async Task GeTodayDataUsegesAsync()
        {
            TodayUsageCollection = new ObservableCollection<NetworkDataUse>();
            var usages = await GetDataUsegesAsync(DateTime.Now.AddDays(-1).Date, DateTime.Now.Date, NetworkInformation.GetInternetConnectionProfile());
            if (usages != null)
            {
                TodayDownload = new NetworkDataUse { DataType = "Download", DataUse = GetSizeInByte(usages.BytesReceived) };
                TodayUpload = new NetworkDataUse { DataType = "Upload", DataUse = GetSizeInByte(usages.BytesSent) };
                TodayUsageCollection.Add(TodayDownload);
                TodayUsageCollection.Add(TodayUpload);
            }
        }
        private async Task<NetworkUsage> GetDataUsegesAsync(DateTime startDate, DateTime endDate, ConnectionProfile internetConnectionProfile)
        {
            try
            {
                NetworkUsageStates NetworkUsageStates = new NetworkUsageStates();
                NetworkUsageStates.Roaming = TriStates.DoNotCare;
                NetworkUsageStates.Shared = TriStates.DoNotCare;
                return (await internetConnectionProfile.GetNetworkUsageAsync(startDate, endDate, DataUsageGranularity.Total, NetworkUsageStates)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        private ulong GetSizeInByte(ulong bytes)
        {
            return bytes / 1048576;
        }
    }
}
