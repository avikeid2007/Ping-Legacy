using BasicMvvm;
using SQLite;
using System;

namespace PingTool.Models
{
    public class PingMassage : BindableBase
    {
        private int _id;
        private Guid _pingId;
        private string _ipAddress;
        private int _size;
        private long _time;
        private int _ttl;
        private string _response;
        private DateTimeOffset _date;

        [PrimaryKey, AutoIncrement]
        public int Id
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }
        public DateTimeOffset Date
        {
            get { return _date; }
            set
            {
                _date = value;
                OnPropertyChanged();
            }
        }
        public Guid PingId
        {
            get { return _pingId; }
            set
            {
                _pingId = value;
                OnPropertyChanged();
            }
        }
        public string Response
        {
            get { return _response; }
            set
            {
                _response = value;
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
        public int Size
        {
            get { return _size; }
            set
            {
                _size = value;
                OnPropertyChanged();
            }
        }
        public long Time
        {
            get { return _time; }
            set
            {
                _time = value;
                OnPropertyChanged();
            }
        }
        public int Ttl
        {
            get { return _ttl; }
            set
            {
                _ttl = value;
                OnPropertyChanged();
            }
        }
    }
}
