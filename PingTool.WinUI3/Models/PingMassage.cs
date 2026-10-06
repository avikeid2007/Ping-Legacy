using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace PingTool.Models;

public partial class PingMassage : ObservableObject
{
    [ObservableProperty]
    [PrimaryKey, AutoIncrement]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial Guid PingId { get; set; }

    [ObservableProperty]
    public partial string IpAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Size { get; set; }

    [ObservableProperty]
    public partial long Time { get; set; }

    [ObservableProperty]
    public partial int Ttl { get; set; }

    [ObservableProperty]
    public partial string Response { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset Date { get; set; }
}
