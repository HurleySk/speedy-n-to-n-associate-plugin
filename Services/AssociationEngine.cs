using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Tooling.Connector;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class AssociationEngine
    {
        public event Action<int, int, int, int> ProgressUpdated; // completed, duplicates, errors, total
        public event Action<string> LogMessage;

        private static readonly Random Rng = new Random();

        public async Task RunAsync(
            IOrganizationService service,
            List<AssociationPair> pairs,
            RelationshipInfo relationship,
            int degreeOfParallelism,
            string resumeFilePath,
            bool bypassPlugins,
            bool verboseLogging,
            int maxRetries,
            CancellationToken cancellationToken)
        {
            // Load completed pairs for resume
            var completedSet = LoadCompletedPairs(resumeFilePath);
            var remaining = pairs.Where(p => !completedSet.Contains(p.NormalizedKey())).ToList();

            int total = remaining.Count;
            int completed = 0;
            int duplicates = 0;
            int errors = 0;
            int throttleBackoffMs = 0;

            if (remaining.Count == 0)
            {
                LogMessage?.Invoke("All pairs already completed. Nothing to do.");
                ProgressUpdated?.Invoke(0, 0, 0, 0);
                return;
            }

            LogMessage?.Invoke($"Total pairs: {pairs.Count}, Already completed: {completedSet.Count}, Remaining: {remaining.Count}");
            LogMessage?.Invoke($"Resume file: {resumeFilePath}");

            // Thread pool and connection tuning (from reference NtoN app)
            ThreadPool.SetMinThreads(100, 100);
#pragma warning disable SYSLIB0014
            ServicePointManager.DefaultConnectionLimit = 65000;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
#pragma warning restore SYSLIB0014

            // Pool CrmServiceClient instances via Clone()
            var primaryClient = service as CrmServiceClient;
            int poolSize;
            if (primaryClient != null)
            {
                poolSize = Math.Min(degreeOfParallelism, Math.Max(primaryClient.RecommendedDegreesOfParallelism, 1));
                primaryClient.EnableAffinityCookie = false;
            }
            else
            {
                poolSize = Math.Min(degreeOfParallelism, 8);
            }

            var clients = new IOrganizationService[poolSize];
            var clientLocks = new object[poolSize];
            clients[0] = service;
            clientLocks[0] = new object();
            for (int i = 1; i < poolSize; i++)
            {
                clientLocks[i] = new object();
                if (primaryClient != null)
                {
                    var clone = primaryClient.Clone();
                    clone.EnableAffinityCookie = false;
                    clients[i] = clone;
                }
                else
                {
                    clients[i] = service;
                }
            }

            LogMessage?.Invoke($"Using {poolSize} pooled connections, parallelism: {degreeOfParallelism}");

            int roundRobin = -1;
            var writerLock = new object();
            var sw = Stopwatch.StartNew();

            // Ensure resume directory exists
            var resumeDir = Path.GetDirectoryName(resumeFilePath);
            if (!string.IsNullOrEmpty(resumeDir) && !Directory.Exists(resumeDir))
                Directory.CreateDirectory(resumeDir);

            using (var completedWriter = new StreamWriter(resumeFilePath, append: true))
            {
                var semaphore = new SemaphoreSlim(degreeOfParallelism);
                var tasks = remaining.Select(async pair =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Shared throttle backoff -- if any thread was throttled, all threads slow down
                        var currentBackoff = Volatile.Read(ref throttleBackoffMs);
                        if (currentBackoff > 0)
                            await Task.Delay(currentBackoff, cancellationToken);

                        var clientIndex = (int)((uint)Interlocked.Increment(ref roundRobin) % poolSize);
                        var client = clients[clientIndex];

                        // Determine entity names based on relationship direction
                        var targetEntityName = relationship.Entity1LogicalName;
                        var relatedEntityName = relationship.Entity2LogicalName;

                        var request = new AssociateRequest
                        {
                            Target = new EntityReference(targetEntityName, pair.Guid1),
                            RelatedEntities = new EntityReferenceCollection
                            {
                                new EntityReference(relatedEntityName, pair.Guid2)
                            },
                            Relationship = new Relationship(relationship.SchemaName)
                            {
                                PrimaryEntityRole = EntityRole.Referencing
                            }
                        };

                        if (bypassPlugins)
                        {
                            request.Parameters["BypassBusinessLogicExecution"] = "CustomSync,CustomAsync";
                            request.Parameters["SuppressCallbackRegistrationExpanderJob"] = true;
                        }

                        // Tenacious retry with exponential backoff
                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                client.Execute(request);
                                Interlocked.Increment(ref completed);
                                TrackCompleted(pair, completedWriter, writerLock);
                                // Decay throttle backoff on success (halve instead of zeroing)
                                var current = Volatile.Read(ref throttleBackoffMs);
                                if (current > 0)
                                    Interlocked.Exchange(ref throttleBackoffMs, current / 2);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"OK: {pair.Guid1} <-> {pair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (IsDuplicateKeyError(ex))
                            {
                                Interlocked.Increment(ref completed);
                                Interlocked.Increment(ref duplicates);
                                TrackCompleted(pair, completedWriter, writerLock);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"DUPLICATE: {pair.Guid1} <-> {pair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
                            {
                                int delay;
                                lock (Rng)
                                {
                                    delay = Math.Min((int)(Math.Pow(2, attempt) * 2000) + Rng.Next(3000), 60000);
                                }
                                Interlocked.Exchange(ref throttleBackoffMs, Math.Min(delay, 30000));
                                LogMessage?.Invoke($"Retry {attempt + 1}/{maxRetries} in {delay / 1000}s: {pair.Guid1} <-> {pair.Guid2}: {GetExceptionSummary(ex)}");
                                await Task.Delay(delay, cancellationToken);

                                // Re-create client on connection-level errors
                                if (IsConnectionError(ex) && primaryClient != null)
                                {
                                    try
                                    {
                                        lock (clientLocks[clientIndex])
                                        {
                                            if (clients[clientIndex] == client) // still the same broken one
                                            {
                                                var newClone = primaryClient.Clone();
                                                newClone.EnableAffinityCookie = false;
                                                (client as IDisposable)?.Dispose();
                                                clients[clientIndex] = newClone;
                                            }
                                            client = clients[clientIndex];
                                        }
                                        LogMessage?.Invoke($"Reconnected client {clientIndex}.");
                                    }
                                    catch { /* best effort */ }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Final failure -- do NOT track in resume so it retries next run
                                Interlocked.Increment(ref completed);
                                Interlocked.Increment(ref errors);
                                LogMessage?.Invoke($"FAILED after {attempt + 1} attempts: {pair.Guid1} <-> {pair.Guid2}: {GetExceptionSummary(ex)}");
                                break;
                            }
                        }

                        ProgressUpdated?.Invoke(completed, duplicates, errors, total);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            // Dispose cloned clients
            for (int i = 1; i < clients.Length; i++)
            {
                (clients[i] as IDisposable)?.Dispose();
            }

            var elapsed = sw.Elapsed;
            var successCount = completed - duplicates - errors;
            LogMessage?.Invoke($"Complete. {successCount:N0} associated, {duplicates:N0} duplicates skipped, {errors:N0} errors. Elapsed: {elapsed:mm\\:ss}");
        }

        public int GetRecommendedParallelism(IOrganizationService service)
        {
            var client = service as CrmServiceClient;
            if (client != null)
                return Math.Max(client.RecommendedDegreesOfParallelism, 1);
            return 4;
        }

        private static void TrackCompleted(AssociationPair pair, StreamWriter writer, object writerLock)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lock (writerLock)
            {
                writer.WriteLine($"{pair.Guid1},{pair.Guid2},{ts}");
                writer.Flush();
            }
        }

        private static HashSet<(Guid, Guid)> LoadCompletedPairs(string path)
        {
            var set = new HashSet<(Guid, Guid)>();
            if (!File.Exists(path)) return set;

            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 &&
                    Guid.TryParse(parts[0], out var g1) &&
                    Guid.TryParse(parts[1], out var g2))
                {
                    set.Add(g1.CompareTo(g2) <= 0 ? (g1, g2) : (g2, g1));
                }
            }

            return set;
        }

        private static string GetExceptionSummary(Exception ex)
        {
            var msg = ex.Message;
            if (msg.Length > 200) msg = msg.Substring(0, 200) + "...";
            return msg;
        }

        private static bool IsTransient(Exception ex)
        {
            return IsTransientMessage(ex.Message) ||
                   (ex.InnerException != null && IsTransientMessage(ex.InnerException.Message));
        }

        private static bool IsTransientMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;

            // HTTP status codes
            if (msg.IndexOf("429", StringComparison.Ordinal) >= 0) return true;
            if (msg.IndexOf("503", StringComparison.Ordinal) >= 0) return true;
            if (msg.IndexOf("502", StringComparison.Ordinal) >= 0) return true;
            if (msg.IndexOf("504", StringComparison.Ordinal) >= 0) return true;

            // Throttling
            if (msg.IndexOf("throttl", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("server busy", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("try again", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Timeouts
            if (msg.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("task was canceled", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Connection errors
            if (msg.IndexOf("connection was closed", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("connection reset", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("underlying connection", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("error occurred while sending", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("socket", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (msg.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private static bool IsConnectionError(Exception ex)
        {
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            return msg.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   msg.IndexOf("socket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   msg.IndexOf("underlying", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDuplicateKeyError(Exception ex)
        {
            return ex.Message.IndexOf("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
