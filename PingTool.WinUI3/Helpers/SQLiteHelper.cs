using PingTool.Models;
using SQLite;

namespace PingTool.Helpers;

public static class SQLiteHelper
{
    private static string? _dbPath;

    private static string DbPath
    {
        get
        {
            if (string.IsNullOrEmpty(_dbPath))
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PingLegacy");
                Directory.CreateDirectory(folder);
                _dbPath = Path.Combine(folder, "pingdb.sqlite");
            }
            return _dbPath;
        }
    }

    private static SQLiteConnection DbConnection => new(new SQLiteConnectionString(DbPath));

    public static void InitializeDatabase()
    {
        using var db = DbConnection;
        db.CreateTable<PingMassage>();
    }

    public static void ClearAllPingMessages()
    {
        using var db = DbConnection;
        db.CreateTable<PingMassage>();
        db.DeleteAll<PingMassage>();
    }

    public static void DeleteOld(int maxCount)
    {
        using var db = DbConnection;
        var oldHistory = GetAllDistinct().OrderByDescending(x => x.Date).Skip(maxCount).ToList();
        foreach (var item in oldHistory)
        {
            Delete(db, item.PingId);
        }
    }

    private static void Delete(SQLiteConnection db, Guid pingId)
    {
        var pings = db.Table<PingMassage>().Where(x => x.PingId == pingId).ToList();
        foreach (var ping in pings)
        {
            db.Delete(ping);
        }
    }

    public static IEnumerable<PingMassage> GetAll(Guid? pingId = null)
    {
        using var db = DbConnection;
        var query = db.Table<PingMassage>();
        if (pingId != null)
        {
            query = query.Where(x => x.PingId == pingId).OrderBy(x => x.Id);
        }
        return query.ToList();
    }

    public static PingMassage? Get(int id)
    {
        using var db = DbConnection;
        return db.Table<PingMassage>().FirstOrDefault(x => x.Id == id);
    }

    public static void Save(PingMassage ping)
    {
        using var db = DbConnection;
        db.Insert(ping);
    }

    public static IEnumerable<PingMassage> GetAllDistinct()
    {
        using var db = DbConnection;
        return db.Table<PingMassage>()
            .ToList()
            .GroupBy(x => x.PingId)
            .Select(x => x.First())
            .OrderByDescending(x => x.Date)
            .ToList();
    }
}
