using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SpeedyNtoNAssociatePlugin.Models;
using SpeedyNtoNAssociatePlugin.Services;

namespace SpeedyNtoNAssociatePlugin.Tests
{
    class SmokeTest
    {
        static int _passed = 0;
        static int _failed = 0;

        static void Assert(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {name}");
                _passed++;
            }
            else
            {
                Console.WriteLine($"  [FAIL] {name}");
                _failed++;
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Speedy N:N Associate - Smoke Tests ===\n");

            TestCsvStreaming();
            TestCsvStreamingDedup();
            TestSqliteResumeTracker();
            TestResumeTrackerConcurrency();

            Console.WriteLine($"\n=== Results: {_passed} passed, {_failed} failed ===");
            Environment.Exit(_failed > 0 ? 1 : 0);
        }

        static void TestCsvStreaming()
        {
            Console.WriteLine("Test: CSV Streaming");

            var csvPath = Path.GetTempFileName();
            File.WriteAllText(csvPath,
                "guid1,guid2\n" +
                "a1b2c3d4-e5f6-7890-abcd-ef1234567890,11111111-2222-3333-4444-555555555555\n" +
                "a1b2c3d4-e5f6-7890-abcd-ef1234567891,11111111-2222-3333-4444-555555555556\n" +
                "a1b2c3d4-e5f6-7890-abcd-ef1234567892,11111111-2222-3333-4444-555555555557\n");

            var svc = new DataSourceService();
            var pairs = svc.StreamFromCsv(csvPath).ToList();

            Assert(pairs.Count == 3, "Streams 3 pairs from CSV with header");
            Assert(pairs[0].Guid1 == Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), "First pair Guid1 correct");
            Assert(pairs[0].Guid2 == Guid.Parse("11111111-2222-3333-4444-555555555555"), "First pair Guid2 correct");

            File.Delete(csvPath);
        }

        static void TestCsvStreamingDedup()
        {
            Console.WriteLine("\nTest: CSV Streaming Dedup");

            var csvPath = Path.GetTempFileName();
            // Include duplicates: exact dup and reversed-order dup
            File.WriteAllText(csvPath,
                "a1b2c3d4-e5f6-7890-abcd-ef1234567890,11111111-2222-3333-4444-555555555555\n" +
                "a1b2c3d4-e5f6-7890-abcd-ef1234567890,11111111-2222-3333-4444-555555555555\n" +
                "11111111-2222-3333-4444-555555555555,a1b2c3d4-e5f6-7890-abcd-ef1234567890\n" +
                "a1b2c3d4-e5f6-7890-abcd-ef1234567891,11111111-2222-3333-4444-555555555556\n");

            var svc = new DataSourceService();
            var pairs = svc.StreamFromCsv(csvPath).ToList();

            Assert(pairs.Count == 2, "Deduplicates exact and reversed duplicates (4 rows -> 2 pairs)");

            File.Delete(csvPath);
        }

        static void TestSqliteResumeTracker()
        {
            Console.WriteLine("\nTest: SQLite Resume Tracker");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_test_" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                Assert(tracker.GetCompletedCount() == 0, "Empty DB has 0 completed");

                // Track some pairs
                for (int i = 0; i < 150; i++)
                {
                    tracker.TrackCompleted(new AssociationPair
                    {
                        Guid1 = Guid.NewGuid(),
                        Guid2 = Guid.NewGuid()
                    });
                }

                // FlushBatch should have auto-triggered at 100, but call it to flush remaining
                tracker.FlushBatch();

                Assert(tracker.GetCompletedCount() == 150, "150 pairs tracked after flush");

                // Test GetCompletedSet
                var set = tracker.GetCompletedSet();
                Assert(set.Count == 150, "GetCompletedSet returns 150 entries");

                // Track a duplicate (normalized key)
                var g1 = Guid.NewGuid();
                var g2 = Guid.NewGuid();
                tracker.TrackCompleted(new AssociationPair { Guid1 = g1, Guid2 = g2 });
                tracker.TrackCompleted(new AssociationPair { Guid1 = g1, Guid2 = g2 }); // exact dup
                tracker.TrackCompleted(new AssociationPair { Guid1 = g2, Guid2 = g1 }); // reversed dup (same normalized key)
                tracker.FlushBatch();

                // Should only have 151 (150 + 1 unique new pair, dupes ignored via INSERT OR IGNORE)
                Assert(tracker.GetCompletedCount() == 151, "Duplicate pairs ignored (INSERT OR IGNORE)");

                tracker.Dispose();

                // Reopen and verify persistence
                var tracker2 = new ResumeTracker(dbPath);
                tracker2.Open();
                Assert(tracker2.GetCompletedCount() == 151, "Data persists after reopen");
                tracker2.Dispose();

                // Test DeleteDatabase
                var tracker3 = new ResumeTracker(dbPath);
                tracker3.Open();
                tracker3.DeleteDatabase();
                Assert(!File.Exists(dbPath), "DeleteDatabase removes the DB file");
            }
            finally
            {
                try { File.Delete(dbPath); } catch { }
                try { File.Delete(dbPath + "-wal"); } catch { }
                try { File.Delete(dbPath + "-shm"); } catch { }
            }
        }

        static void TestResumeTrackerConcurrency()
        {
            Console.WriteLine("\nTest: Resume Tracker Concurrency");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_conc_" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                // Simulate concurrent writes from multiple threads
                int threadCount = 10;
                int pairsPerThread = 200;
                var threads = new Thread[threadCount];

                for (int t = 0; t < threadCount; t++)
                {
                    threads[t] = new Thread(() =>
                    {
                        for (int i = 0; i < pairsPerThread; i++)
                        {
                            tracker.TrackCompleted(new AssociationPair
                            {
                                Guid1 = Guid.NewGuid(),
                                Guid2 = Guid.NewGuid()
                            });
                        }
                    });
                }

                foreach (var t in threads) t.Start();
                foreach (var t in threads) t.Join();

                tracker.FlushBatch();

                var count = tracker.GetCompletedCount();
                int expected = threadCount * pairsPerThread;
                Assert(count == expected, $"Concurrent writes: {count} == {expected} expected");

                tracker.Dispose();
            }
            finally
            {
                try { File.Delete(dbPath); } catch { }
                try { File.Delete(dbPath + "-wal"); } catch { }
                try { File.Delete(dbPath + "-shm"); } catch { }
            }
        }
    }
}
