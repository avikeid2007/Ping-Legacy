using CommunityToolkit.Mvvm.ComponentModel;

namespace PingTool.Models;

public partial class DataUse : ObservableObject
{
    [ObservableProperty]
    public partial DateTime Date { get; set; }

    [ObservableProperty]
    public partial ulong Upload { get; set; }

    [ObservableProperty]
    public partial ulong Download { get; set; }

    [ObservableProperty]
    public partial TimeSpan ConnectionDuration { get; set; }
}

public class NetworkDataUse
{
    public string DataType { get; set; } = string.Empty;
    public ulong DataUse { get; set; }
}
