using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using SpeedyNtoNAssociatePlugin.Models;
using SpeedyNtoNAssociatePlugin.Services;
using SpeedyNtoNAssociatePlugin.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

            // New tests
            TestAssociationPairNormalizedKey();
            TestExtractTaggedGuids();
            TestExtractPairsPositional();
            TestExtractPairsSelfReferencing();
            TestCsvEdgeCases();
            TestMetadataServiceGetAllMetadata();
            TestLoadFromFetchXmlPagination();
            TestAssociationEngineSuccessful();
            TestAssociationEngineDuplicateKey();
            TestAssociationEngineCancellation();
            TestGetRecommendedParallelism();
            TestAssociationEngineResume();

            // Performance feature tests
            TestFireAndForgetBatchMode();
            TestBatchModeResponseMap();

            // Test data generator
            TestGenerateCsvRoundTrip();

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
        #region Helpers

        static void SetSdkProperty(object obj, string propName, object value)
        {
            var prop = obj.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null)
                throw new InvalidOperationException(
                    $"Property '{propName}' not found on {obj.GetType().Name}");

            var setter = prop.GetSetMethod(true);
            if (setter == null)
                throw new InvalidOperationException(
                    $"Property '{propName}' on {obj.GetType().Name} has no setter");

            setter.Invoke(obj, new[] { value });
        }

        static void CleanupDb(string dbPath)
        {
            foreach (var ext in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(dbPath + ext); } catch { }
            }
        }

        #endregion

        #region Pure Logic Tests

        static void TestAssociationPairNormalizedKey()
        {
            Console.WriteLine("\nTest: AssociationPair.NormalizedKey");

            var guidA = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var guidB = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

            var pair1 = new AssociationPair { Guid1 = guidA, Guid2 = guidB };
            var pair2 = new AssociationPair { Guid1 = guidB, Guid2 = guidA };

            Assert(pair1.NormalizedKey() == pair2.NormalizedKey(),
                "(A,B) and (B,A) produce identical normalized key");

            var key = pair1.NormalizedKey();
            Assert(key.Item1.CompareTo(key.Item2) <= 0,
                "Smaller GUID is always Item1");

            var equalPair = new AssociationPair { Guid1 = guidA, Guid2 = guidA };
            var equalKey = equalPair.NormalizedKey();
            Assert(equalKey.Item1 == guidA && equalKey.Item2 == guidA,
                "Equal GUIDs produce (guid, guid)");

            var differentPair = new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() };
            Assert(pair1.NormalizedKey() != differentPair.NormalizedKey(),
                "Distinct pairs produce different keys");
        }

        static void TestExtractTaggedGuids()
        {
            Console.WriteLine("\nTest: ExtractTaggedGuids");

            var contactId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var oppId = Guid.NewGuid();

            var entity = new Entity("contact", contactId);
            // Direct Guid attribute
            entity["accountid"] = accountId;
            // AliasedValue
            entity["alias.oppid"] = new AliasedValue("opportunity", "opportunityid", oppId);
            // EntityReference
            var lookupGuid = Guid.NewGuid();
            entity["ownerid"] = new EntityReference("systemuser", lookupGuid);

            var guids = DataSourceService.ExtractTaggedGuids(entity);

            Assert(guids.Any(g => g.id == contactId && g.entityName == "contact"),
                "Entity.Id included with LogicalName");
            Assert(guids.Any(g => g.id == accountId && g.entityName == "contact"),
                "Direct Guid attribute extracted");
            Assert(guids.Any(g => g.id == oppId && g.entityName == "opportunity"),
                "AliasedValue extracted with source entity name");
            Assert(guids.Any(g => g.id == lookupGuid && g.entityName == "systemuser"),
                "EntityReference extracted with target entity name");

            // Guid.Empty filtered
            var emptyEntity = new Entity("test") { Id = Guid.Empty };
            emptyEntity["field"] = Guid.Empty;
            var emptyGuids = DataSourceService.ExtractTaggedGuids(emptyEntity);
            Assert(emptyGuids.Count == 0, "Guid.Empty filtered out");
        }

        static void TestExtractPairsPositional()
        {
            Console.WriteLine("\nTest: ExtractPairs (positional mode)");

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g3 = Guid.NewGuid();

            // Entity with 2 GUIDs
            var entity1 = new Entity("contact", g1);
            entity1["accountid"] = g2;

            // Entity with only 1 GUID (should be skipped)
            var entity2 = new Entity("contact", g3);

            var ec = new EntityCollection(new List<Entity> { entity1, entity2 });

            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            DataSourceService.ExtractPairs(ec, pairs, seen, null, null, ref skipped);

            Assert(pairs.Count == 1, "Entity with 2 GUIDs produces 1 pair");
            Assert(skipped == 1, "Entity with 1 GUID is skipped");

            // Dedup: add same pair again
            var ec2 = new EntityCollection(new List<Entity> { entity1 });
            DataSourceService.ExtractPairs(ec2, pairs, seen, null, null, ref skipped);
            Assert(pairs.Count == 1, "Duplicate pair deduped via seen set");
        }

        static void TestExtractPairsSelfReferencing()
        {
            Console.WriteLine("\nTest: ExtractPairs (self-referencing)");

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();

            // Entity with two contact GUIDs (self-referencing N:N)
            var entity = new Entity("contact", g1);
            entity["relatedcontactid"] = new AliasedValue("contact", "contactid", g2);

            var ec = new EntityCollection(new List<Entity> { entity });
            var pairs = new List<AssociationPair>();
            var seen = new HashSet<(Guid, Guid)>();
            int skipped = 0;

            DataSourceService.ExtractPairs(ec, pairs, seen, "contact", "contact", ref skipped);

            Assert(pairs.Count == 1, "Self-referencing: two contact GUIDs matched");
            Assert(pairs[0].Guid1 == g1 && pairs[0].Guid2 == g2,
                "Self-referencing: correct GUIDs assigned");
        }

        static void TestCsvEdgeCases()
        {
            Console.WriteLine("\nTest: CSV Edge Cases");
            var svc = new DataSourceService();

            // Empty GUIDs filtered
            var csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath,
                    "00000000-0000-0000-0000-000000000000,11111111-2222-3333-4444-555555555555\n");
                var pairs = svc.StreamFromCsv(csvPath).ToList();
                Assert(pairs.Count == 0, "Empty GUIDs (Guid.Empty) filtered");
            }
            finally { File.Delete(csvPath); }

            // Malformed lines skipped
            csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath, "not-a-guid,also-not\nhello\n");
                var pairs = svc.StreamFromCsv(csvPath).ToList();
                Assert(pairs.Count == 0, "Malformed lines skipped without exception");
            }
            finally { File.Delete(csvPath); }

            // Quoted GUIDs cleaned
            csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath,
                    "\"a1b2c3d4-e5f6-7890-abcd-ef1234567890\",\"11111111-2222-3333-4444-555555555555\"\n");
                var pairs = svc.StreamFromCsv(csvPath).ToList();
                Assert(pairs.Count == 1, "Quoted GUIDs parsed correctly");
            }
            finally { File.Delete(csvPath); }

            // Headerless CSV (first line is valid GUID pair)
            csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath,
                    "a1b2c3d4-e5f6-7890-abcd-ef1234567890,11111111-2222-3333-4444-555555555555\n" +
                    "a1b2c3d4-e5f6-7890-abcd-ef1234567891,11111111-2222-3333-4444-555555555556\n");
                var pairs = svc.StreamFromCsv(csvPath).ToList();
                Assert(pairs.Count == 2, "Headerless CSV includes first line");
            }
            finally { File.Delete(csvPath); }

            // Single column line
            csvPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(csvPath, "a1b2c3d4-e5f6-7890-abcd-ef1234567890\n");
                var pairs = svc.StreamFromCsv(csvPath).ToList();
                Assert(pairs.Count == 0, "Single column line skipped");
            }
            finally { File.Delete(csvPath); }
        }

        #endregion

        #region Mock-Based Service Tests

        static void TestMetadataServiceGetAllMetadata()
        {
            Console.WriteLine("\nTest: MetadataService.GetAllMetadata");

            // Build EntityMetadata via reflection (internal setters)
            var accountMeta = new EntityMetadata();
            SetSdkProperty(accountMeta, "LogicalName", "account");
            SetSdkProperty(accountMeta, "IsIntersect", (bool?)false);
            accountMeta.DisplayName = new Microsoft.Xrm.Sdk.Label("Account", 1033);

            var contactMeta = new EntityMetadata();
            SetSdkProperty(contactMeta, "LogicalName", "contact");
            SetSdkProperty(contactMeta, "IsIntersect", (bool?)false);
            // No DisplayName — tests fallback to LogicalName

            var intersectMeta = new EntityMetadata();
            SetSdkProperty(intersectMeta, "LogicalName", "accountcontact");
            SetSdkProperty(intersectMeta, "IsIntersect", (bool?)true);

            // Build ManyToManyRelationshipMetadata
            var rel1 = new ManyToManyRelationshipMetadata();
            SetSdkProperty(rel1, "SchemaName", "account_contact_nn");
            SetSdkProperty(rel1, "Entity1LogicalName", "account");
            SetSdkProperty(rel1, "Entity2LogicalName", "contact");
            SetSdkProperty(rel1, "IntersectEntityName", "accountcontact");
            SetSdkProperty(rel1, "Entity1IntersectAttribute", "accountid");
            SetSdkProperty(rel1, "Entity2IntersectAttribute", "contactid");

            // Same relationship on both entities (tests dedup)
            var rel2 = new ManyToManyRelationshipMetadata();
            SetSdkProperty(rel2, "SchemaName", "account_contact_nn");
            SetSdkProperty(rel2, "Entity1LogicalName", "account");
            SetSdkProperty(rel2, "Entity2LogicalName", "contact");
            SetSdkProperty(rel2, "IntersectEntityName", "accountcontact");
            SetSdkProperty(rel2, "Entity1IntersectAttribute", "accountid");
            SetSdkProperty(rel2, "Entity2IntersectAttribute", "contactid");

            SetSdkProperty(accountMeta, "ManyToManyRelationships", new ManyToManyRelationshipMetadata[] { rel1 });
            SetSdkProperty(contactMeta, "ManyToManyRelationships", new ManyToManyRelationshipMetadata[] { rel2 });

            var allMetadata = new EntityMetadata[] { accountMeta, contactMeta, intersectMeta };

            var mock = new MockOrganizationService();
            mock.ExecuteHandler = request =>
            {
                var response = new RetrieveAllEntitiesResponse();
                response.Results["EntityMetadata"] = allMetadata;
                return response;
            };

            var svc = new MetadataService();
            var result = svc.GetAllMetadata(mock);

            Assert(!result.Entities.Any(e => e.LogicalName == "accountcontact"),
                "Intersect entities filtered out");
            Assert(result.Entities.Any(e => e.LogicalName == "contact" && e.DisplayName == "contact"),
                "DisplayName falls back to LogicalName when null");
            Assert(result.Entities.Count == 2, "Two non-intersect entities returned");
            Assert(result.Relationships.Count == 1, "Relationships deduplicated by SchemaName");
            Assert(result.Relationships[0].Entity1LogicalName == "account" &&
                   result.Relationships[0].Entity2LogicalName == "contact",
                "Relationship fields correctly mapped");
        }

        static void TestLoadFromFetchXmlPagination()
        {
            Console.WriteLine("\nTest: LoadFromFetchXml Pagination");

            int callCount = 0;
            var mock = new MockOrganizationService();
            mock.RetrieveMultipleHandler = query =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Page 1: 2 entities, more records available
                    var e1 = new Entity("contact", Guid.NewGuid());
                    e1["accountid"] = Guid.NewGuid();
                    var e2 = new Entity("contact", Guid.NewGuid());
                    e2["accountid"] = Guid.NewGuid();

                    var ec = new EntityCollection(new List<Entity> { e1, e2 });
                    ec.MoreRecords = true;
                    ec.PagingCookie = "<cookie page=\"1\"/>";
                    return ec;
                }
                else
                {
                    // Page 2: 1 entity, no more records
                    var e3 = new Entity("contact", Guid.NewGuid());
                    e3["accountid"] = Guid.NewGuid();

                    var ec = new EntityCollection(new List<Entity> { e3 });
                    ec.MoreRecords = false;
                    return ec;
                }
            };

            var svc = new DataSourceService();
            var result = svc.LoadFromFetchXml(mock,
                "<fetch><entity name='contact'><attribute name='contactid'/></entity></fetch>",
                null, null);

            Assert(result.Item1.Count == 3, "3 pairs across 2 pages");
            Assert(result.Item2 == 0, "No rows skipped");
            Assert(callCount == 2, "Pagination: RetrieveMultiple called exactly twice");
        }

        #endregion

        #region AssociationEngine Tests

        static void TestAssociationEngineSuccessful()
        {
            Console.WriteLine("\nTest: AssociationEngine Successful");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_engine_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req => new AssociateResponse();

                var pairs = new List<AssociationPair>
                {
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                };

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact"
                };

                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var engine = new AssociationEngine();
                var logMessages = new List<string>();
                engine.LogMessage += msg => logMessages.Add(msg);

                engine.RunAsync(mock, pairs, relationship,
                    degreeOfParallelism: 1, tracker, bypassPlugins: false,
                    verboseLogging: false, maxRetries: 0, batchSize: 1,
                    fireAndForget: false,
                    CancellationToken.None).GetAwaiter().GetResult();

                tracker.FlushBatch();

                Assert(tracker.GetCompletedCount() == 3, "All 3 pairs completed");
                Assert(mock.ExecutedRequests.Count == 3 &&
                       mock.ExecutedRequests.ToArray().All(r => r is AssociateRequest),
                    "3 AssociateRequests sent");
                Assert(logMessages.Any(m => m.Contains("Complete")),
                    "Log contains 'Complete' message");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        static void TestAssociationEngineDuplicateKey()
        {
            Console.WriteLine("\nTest: AssociationEngine Duplicate Key");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_dup_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req =>
                    throw new Exception("Cannot insert duplicate key row in object");

                var pairs = new List<AssociationPair>
                {
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                };

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact"
                };

                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var engine = new AssociationEngine();
                int lastDuplicates = 0;
                engine.ProgressUpdated += (completed, duplicates, errors, total) =>
                {
                    lastDuplicates = duplicates;
                };

                engine.RunAsync(mock, pairs, relationship,
                    degreeOfParallelism: 1, tracker, bypassPlugins: false,
                    verboseLogging: false, maxRetries: 0, batchSize: 1,
                    fireAndForget: false,
                    CancellationToken.None).GetAwaiter().GetResult();

                tracker.FlushBatch();

                Assert(tracker.GetCompletedCount() == 2,
                    "Duplicate key pairs counted as completed");
                Assert(lastDuplicates == 2,
                    "Duplicates counter == pair count");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        static void TestAssociationEngineCancellation()
        {
            Console.WriteLine("\nTest: AssociationEngine Cancellation");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_cancel_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req =>
                {
                    Thread.Sleep(100); // 100ms per request; 1000 pairs = 100s total
                    return new AssociateResponse();
                };

                var pairs = new List<AssociationPair>();
                for (int i = 0; i < 1000; i++)
                    pairs.Add(new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() });

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact"
                };

                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var engine = new AssociationEngine();
                var cts = new CancellationTokenSource();
                cts.CancelAfter(500); // Cancel after 500ms — at most ~5 pairs can complete

                bool cancelled = false;
                try
                {
                    engine.RunAsync(mock, pairs, relationship,
                        degreeOfParallelism: 1, tracker, bypassPlugins: false,
                        verboseLogging: false, maxRetries: 0, batchSize: 1,
                        fireAndForget: false,
                        cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                tracker.FlushBatch();
                var completed = tracker.GetCompletedCount();
                Assert(cancelled && completed < 1000,
                    $"Cancellation stopped early: {completed}/1000 completed (cancelled={cancelled})");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        static void TestAssociationEngineResume()
        {
            Console.WriteLine("\nTest: AssociationEngine Resume (skip completed)");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_resume_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var pair1 = new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() };
                var pair2 = new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() };
                var pair3 = new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() };

                // Pre-populate tracker with pair1 as already completed
                var tracker = new ResumeTracker(dbPath);
                tracker.Open();
                tracker.TrackCompleted(pair1);
                tracker.FlushBatch();
                tracker.Dispose();

                // Reopen for the engine run
                tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req => new AssociateResponse();

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact"
                };

                var engine = new AssociationEngine();

                engine.RunAsync(mock, new[] { pair1, pair2, pair3 }, relationship,
                    degreeOfParallelism: 1, tracker, bypassPlugins: false,
                    verboseLogging: false, maxRetries: 0, batchSize: 1,
                    fireAndForget: false,
                    CancellationToken.None).GetAwaiter().GetResult();

                tracker.FlushBatch();

                Assert(mock.ExecutedRequests.Count == 2,
                    "Only 2 requests sent (pair1 skipped via resume)");
                Assert(tracker.GetCompletedCount() == 3,
                    "All 3 pairs in completed set after run");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        static void TestGetRecommendedParallelism()
        {
            Console.WriteLine("\nTest: GetRecommendedParallelism");

            var mock = new MockOrganizationService();
            var engine = new AssociationEngine();
            var result = engine.GetRecommendedParallelism(mock);

            Assert(result == 4, $"Non-CrmServiceClient returns default 4 (got {result})");
        }

        #endregion

        #region Performance Feature Tests

        static void TestFireAndForgetBatchMode()
        {
            Console.WriteLine("\nTest: Fire-and-Forget Batch Mode");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_ff_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req =>
                {
                    // ExecuteMultipleRequest in fire-and-forget returns empty responses
                    if (req is ExecuteMultipleRequest emr)
                    {
                        Assert(!emr.Settings.ReturnResponses,
                            "Fire-and-forget: ReturnResponses is false");
                        Assert(!emr.Settings.ContinueOnError,
                            "Fire-and-forget: ContinueOnError is false");
                        return new ExecuteMultipleResponse
                        {
                            Results = { ["Responses"] = new ExecuteMultipleResponseItemCollection() }
                        };
                    }
                    return new OrganizationResponse();
                };

                var pairs = new List<AssociationPair>();
                for (int i = 0; i < 5; i++)
                    pairs.Add(new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() });

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact",
                    IntersectEntityName = "test_account_contact",
                    Entity1IntersectAttribute = "accountid",
                    Entity2IntersectAttribute = "contactid"
                };

                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var engine = new AssociationEngine();
                engine.RunAsync(mock, pairs, relationship,
                    degreeOfParallelism: 1, tracker, bypassPlugins: false,
                    verboseLogging: false, maxRetries: 0, batchSize: 5,
                    fireAndForget: true,
                    CancellationToken.None).GetAwaiter().GetResult();

                tracker.FlushBatch();

                Assert(tracker.GetCompletedCount() == 5,
                    "Fire-and-forget: all 5 pairs marked completed");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        // NOTE: Direct intersect insert (CreateRequest on intersect entity) was tested here
        // but removed — Dataverse does not support Create on intersect entity types.
        // AssociateRequest is the only supported path for N:N relationships.

        static void TestBatchModeResponseMap()
        {
            Console.WriteLine("\nTest: Batch Mode Response Map (O(1) lookup)");

            var dbPath = Path.Combine(Path.GetTempPath(), "SpeedyNtoN_batch_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var mock = new MockOrganizationService();
                mock.ExecuteHandler = req =>
                {
                    if (req is ExecuteMultipleRequest emr)
                    {
                        // Return responses with one success and one duplicate
                        var responses = new ExecuteMultipleResponseItemCollection();
                        // Index 1 is a duplicate fault
                        responses.Add(new ExecuteMultipleResponseItem
                        {
                            RequestIndex = 1,
                            Fault = new OrganizationServiceFault
                            {
                                Message = "Cannot insert duplicate key row in object"
                            }
                        });
                        return new ExecuteMultipleResponse
                        {
                            Results = { ["Responses"] = responses }
                        };
                    }
                    return new OrganizationResponse();
                };

                var pairs = new List<AssociationPair>
                {
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                    new AssociationPair { Guid1 = Guid.NewGuid(), Guid2 = Guid.NewGuid() },
                };

                var relationship = new RelationshipInfo
                {
                    SchemaName = "test_nn",
                    Entity1LogicalName = "account",
                    Entity2LogicalName = "contact",
                    IntersectEntityName = "test_account_contact",
                    Entity1IntersectAttribute = "accountid",
                    Entity2IntersectAttribute = "contactid"
                };

                var tracker = new ResumeTracker(dbPath);
                tracker.Open();

                var engine = new AssociationEngine();
                int lastDuplicates = 0;
                engine.ProgressUpdated += (completed, duplicates, errors, total) =>
                {
                    lastDuplicates = duplicates;
                };

                engine.RunAsync(mock, pairs, relationship,
                    degreeOfParallelism: 1, tracker, bypassPlugins: false,
                    verboseLogging: false, maxRetries: 0, batchSize: 2,
                    fireAndForget: false,
                    CancellationToken.None).GetAwaiter().GetResult();

                tracker.FlushBatch();

                Assert(tracker.GetCompletedCount() == 2,
                    "Batch mode: both pairs tracked as completed");
                Assert(lastDuplicates == 1,
                    "Batch mode: 1 duplicate detected via response map");

                tracker.Dispose();
            }
            finally { CleanupDb(dbPath); }
        }

        #endregion

        #region Test Data Generator

        static void TestGenerateCsvRoundTrip()
        {
            Console.WriteLine("\nTest: CSV Generator Round-Trip");

            var csvPath = Path.GetTempFileName();
            try
            {
                int count = 500;
                GenerateTestCsv(csvPath, count);

                var svc = new DataSourceService();
                var pairs = svc.StreamFromCsv(csvPath).ToList();

                Assert(pairs.Count == count,
                    $"Generated CSV has {count} loadable pairs");
                Assert(pairs.All(p => p.Guid1 != Guid.Empty && p.Guid2 != Guid.Empty),
                    "All pairs have non-empty GUIDs");
                Assert(pairs.Select(p => p.NormalizedKey()).Distinct().Count() == count,
                    "All pairs are unique");

                // Verify file size is reasonable
                var fileInfo = new FileInfo(csvPath);
                Assert(fileInfo.Length > 0, $"CSV file is non-empty ({fileInfo.Length} bytes)");
            }
            finally { File.Delete(csvPath); }
        }

        /// <summary>
        /// Generates a CSV file with random GUID pairs for testing.
        /// Can be called from tests or used as a standalone utility.
        /// </summary>
        static void GenerateTestCsv(string outputPath, int count, bool includeHeader = true)
        {
            using (var writer = new StreamWriter(outputPath))
            {
                if (includeHeader)
                    writer.WriteLine("Guid1,Guid2");

                for (int i = 0; i < count; i++)
                    writer.WriteLine($"{Guid.NewGuid()},{Guid.NewGuid()}");
            }
        }

        #endregion
    }
}
