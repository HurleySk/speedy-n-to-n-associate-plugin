using Microsoft.Data.Sqlite;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class ResumeTracker : IDisposable
    {
        private const int FlushThreshold = 100;

        private readonly string _dbPath;
        private SqliteConnection _connection;
        private readonly ConcurrentQueue<AssociationPair> _buffer = new ConcurrentQueue<AssociationPair>();
        private readonly object _flushLock = new object();
        private int _bufferedCount;
        private bool _disposed;

        public ResumeTracker(string dbPath)
        {
            _dbPath = dbPath;
        }

        public void Open()
        {
            SQLitePCL.Batteries.Init();

            var dir = System.IO.Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA synchronous=NORMAL;";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS completed (
                        guid1 TEXT NOT NULL,
                        guid2 TEXT NOT NULL,
                        completed_at TEXT NOT NULL,
                        PRIMARY KEY (guid1, guid2)
                    ) WITHOUT ROWID;";
                cmd.ExecuteNonQuery();
            }
        }

        public long GetCompletedCount()
        {
            if (_connection == null) return 0;

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM completed;";
                return (long)cmd.ExecuteScalar();
            }
        }

        public HashSet<(Guid, Guid)> GetCompletedSet()
        {
            var set = new HashSet<(Guid, Guid)>();
            if (_connection == null) return set;

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT guid1, guid2 FROM completed;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (Guid.TryParse(reader.GetString(0), out var g1) &&
                            Guid.TryParse(reader.GetString(1), out var g2))
                        {
                            set.Add((g1, g2));
                        }
                    }
                }
            }

            return set;
        }

        public void TrackCompleted(AssociationPair pair)
        {
            var key = pair.NormalizedKey();
            _buffer.Enqueue(new AssociationPair { Guid1 = key.Item1, Guid2 = key.Item2 });

            var count = System.Threading.Interlocked.Increment(ref _bufferedCount);
            if (count >= FlushThreshold)
                FlushBatch();
        }

        public void FlushBatch()
        {
            lock (_flushLock)
            {
                if (_buffer.IsEmpty || _connection == null) return;

                System.Threading.Interlocked.Exchange(ref _bufferedCount, 0);

                using (var transaction = _connection.BeginTransaction())
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = "INSERT OR IGNORE INTO completed (guid1, guid2, completed_at) VALUES ($g1, $g2, $ts);";
                        var p1 = cmd.Parameters.Add("$g1", SqliteType.Text);
                        var p2 = cmd.Parameters.Add("$g2", SqliteType.Text);
                        var pTs = cmd.Parameters.Add("$ts", SqliteType.Text);

                        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        while (_buffer.TryDequeue(out var pair))
                        {
                            p1.Value = pair.Guid1.ToString();
                            p2.Value = pair.Guid2.ToString();
                            pTs.Value = ts;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public void DeleteDatabase()
        {
            Dispose();

            if (System.IO.File.Exists(_dbPath))
                System.IO.File.Delete(_dbPath);

            // SQLite WAL/SHM files
            var walPath = _dbPath + "-wal";
            var shmPath = _dbPath + "-shm";
            if (System.IO.File.Exists(walPath))
                System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath))
                System.IO.File.Delete(shmPath);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            FlushBatch();
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
    }
}
