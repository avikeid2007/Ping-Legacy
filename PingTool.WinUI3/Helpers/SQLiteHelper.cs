using PingTool.Models;
using SQLite;

namespace PingTool.Helpers;

public static class SQLiteHelper
{
    private static string? _dbPath;
    // Serializes all access: concurrent SQLiteConnections from different threads to the
    // same file (e.g. a Save while DeleteOld is running) throw "database is locked" otherwise.
    private static readonly object DbLock = new();

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

    private static SQLiteConnection DbConnection
    {
        get
        {
            var db = new SQLiteConnection(new SQLiteConnectionString(DbPath));
            db.BusyTimeout = TimeSpan.FromSeconds(5);
            // PRAGMA journal_mode returns the resulting mode as a row, so Execute() (which
            // expects SQLITE_DONE) throws "not an error" - must use ExecuteScalar instead.
            db.ExecuteScalar<string>("PRAGMA journal_mode=WAL;");
            return db;
        }
    }

    public static void InitializeDatabase()
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            db.CreateTable<PingMassage>();
        }
    }

    public static void ClearAllPingMessages()
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            db.CreateTable<PingMassage>();
            db.DeleteAll<PingMassage>();
        }
    }

    public static void DeleteOld(int maxCount)
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            var oldHistory = GetAllDistinctCore(db).OrderByDescending(x => x.Date).Skip(maxCount).ToList();
            foreach (var item in oldHistory)
            {
                Delete(db, item.PingId);
            }
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
        lock (DbLock)
        {
            using var db = DbConnection;
            var query = db.Table<PingMassage>();
            if (pingId != null)
            {
                query = query.Where(x => x.PingId == pingId).OrderBy(x => x.Id);
            }
            return query.ToList();
        }
    }

    public static PingMassage? Get(int id)
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            return db.Table<PingMassage>().FirstOrDefault(x => x.Id == id);
        }
    }

    public static void Save(PingMassage ping)
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            db.Insert(ping);
        }
    }

    public static IEnumerable<PingMassage> GetAllDistinct()
    {
        lock (DbLock)
        {
            using var db = DbConnection;
            return GetAllDistinctCore(db);
        }
    }

    private static List<PingMassage> GetAllDistinctCore(SQLiteConnection db) =>
        db.Table<PingMassage>()
            .ToList()
            .GroupBy(x => x.PingId)
            .Select(x => x.First())
            .OrderByDescending(x => x.Date)
            .ToList();
}
