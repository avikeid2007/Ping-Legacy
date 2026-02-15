using BasicMvvm;
using BasicMvvm.Commands;
using PingTool.Helpers;
using PingTool.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PingTool.ViewModels
{
    public class HistoryViewModel : BindableBase
    {
        private PingMassage _selectedMassage;
        private ObservableCollection<PingMassage> _pingCollaction;
        private ObservableCollection<PingMassage> _historyCollaction;
        private bool _isReplyVisible;
        public bool IsReplyVisible
        {
            get { return _isReplyVisible; }
            set
            {
                _isReplyVisible = value;
                OnPropertyChanged();
            }
        }
        public ICommand CopyCommand => new DelegateCommand(OnCopyCommandExecuted);
        public ICommand ExportCommand => new AsyncCommand(OnExportCommandExecutedAsync);
        private void OnCopyCommandExecuted()
        {
            if (PingCollaction?.Count > 0)
            {
                var text = GetPingText();
                FileHelper.CopyText(text);
            }
        }
        private string GetPingText()
        {
            var text = string.Empty;
            PingCollaction.ToList().ForEach(x => text += x.Response + "\r");
            return text;
        }
        private async Task OnExportCommandExecutedAsync()
        {
            if (PingCollaction?.Count > 0)
            {
                var text = string.Empty;
                PingCollaction.ToList().ForEach(x => text += x.Response + "\r");
                await FileHelper.SaveFileAsync(text, "ping.txt");
            }
        }
        public HistoryViewModel()
        {
            var history = SQLiteHelper.GetAllDistinct();
            if (history?.Count() > 0)
            {
                HistoryCollaction = new ObservableCollection<PingMassage>(history);
            }
        }
        public PingMassage SelectedMassage
        {
            get { return _selectedMassage; }
            set
            {
                _selectedMassage = value;
                if (value != null)
                {
                    var historyList = SQLiteHelper.GetAll(value.PingId);
                    PingCollaction = new ObservableCollection<PingMassage>(historyList);
                    IsReplyVisible = true;
                }
                OnPropertyChanged();
            }
        }
        public ObservableCollection<PingMassage> PingCollaction
        {
            get { return _pingCollaction; }
            set
            {
                _pingCollaction = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<PingMassage> HistoryCollaction
        {
            get { return _historyCollaction; }
            set
            {
                _historyCollaction = value;
                OnPropertyChanged();
            }
        }

    }
}
