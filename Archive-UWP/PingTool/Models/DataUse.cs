using BasicMvvm;
using System;

namespace PingTool.Models
{
    public class DataUse : BindableBase
    {
        private DateTime _date;
        private ulong _download;
        private ulong _upload;
        private TimeSpan _connectionDuration;
        public string DateString
        {
            get
            {
                return Date.ToString("MM/dd/yyyy");
            }
        }
        public DateTime Date
        {
            get { return _date; }
            set
            {
                _date = value;
                OnPropertyChanged();
            }
        }
        public ulong Download
        {
            get { return _download; }
            set
            {
                _download = value;
                OnPropertyChanged();
            }
        }
        public ulong Upload
        {
            get { return _upload; }
            set
            {
                _upload = value;
                OnPropertyChanged();
            }
        }
        public TimeSpan ConnectionDuration
        {
            get { return _connectionDuration; }
            set
            {
                _connectionDuration = value;
                OnPropertyChanged();
            }
        }

    }
}
