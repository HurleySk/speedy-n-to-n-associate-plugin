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
        // Events
        public event Action<int, int, int, int> ProgressUpdated;
        public event Action<string> LogMessage;

        // Constants
        private const int MinThreadPoolSize = 100;
        private const int MaxConnectionLimit = 65000;
        private const int BackoffBaseMs = 2000;
        private const int BackoffJitterMs = 3000;
        private const int MaxRetryDelayMs = 60000;
        private const int MaxThrottleBackoffMs = 30000;
        private const long ProgressUpdateIntervalMs = 100;
        private const int ResumeFlushInterval = 50;
        private const int MaxExceptionMessageLength = 200;

        private const string BypassLogicParam = "BypassBusinessLogicExecution";
        private const string BypassLogicValue = "CustomSync,CustomAsync";
        private const string SuppressCallbackParam = "SuppressCallbackRegistrationExpanderJob";
        private const string DuplicateKeyError = "Cannot insert duplicate key";

        private static readonly string[] TransientPatterns =
        {
            "429", "503", "502", "504",
            "throttl", "server busy", "try again",
            "timeout", "timed out", "task was canceled",
            "connection was closed", "connection reset", "underlying connection",
            "error occurred while sending", "socket", "network"
        };

        private static readonly ThreadLocal<Random> Rng =
            new ThreadLocal<Random>(() => new Random());

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
            var completedSet = LoadCompletedPairs(resumeFilePath);
            var remaining = pairs.Where(p => !completedSet.Contains(p.NormalizedKey())).ToList();

            int total = remaining.Count;
            int completed = 0;
            int duplicates = 0;
            int errors = 0;
            int throttleBackoffMs = 0;
            long lastProgressUpdateMs = 0;

            if (remaining.Count == 0)
            {
                LogMessage?.Invoke("All pairs already completed. Nothing to do.");
                ProgressUpdated?.Invoke(0, 0, 0, 0);
                return;
            }

            LogMessage?.Invoke($"Total pairs: {pairs.Count}, Already completed: {completedSet.Count}, Remaining: {remaining.Count}");
            LogMessage?.Invoke($"Resume file: {resumeFilePath}");

            TuneThreadPool();

            var pool = InitializeClientPool(service, degreeOfParallelism);
            var clients = pool.Item1;
            var clientLocks = pool.Item2;
            int poolSize = pool.Item3;
            var primaryClient = service as CrmServiceClient;

            LogMessage?.Invoke($"Using {poolSize} pooled connections, parallelism: {degreeOfParallelism}");

            int roundRobin = -1;
            var writerLock = new object();
            int writeCount = 0;
            var sw = Stopwatch.StartNew();

            var resumeDir = Path.GetDirectoryName(resumeFilePath);
            if (!string.IsNullOrEmpty(resumeDir) && !Directory.Exists(resumeDir))
                Directory.CreateDirectory(resumeDir);

            using (var completedWriter = new StreamWriter(resumeFilePath, append: true))
            {
                completedWriter.AutoFlush = false;

                var semaphore = new SemaphoreSlim(degreeOfParallelism);
                var tasks = remaining.Select(async pair =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var currentBackoff = Volatile.Read(ref throttleBackoffMs);
                        if (currentBackoff > 0)
                            await Task.Delay(currentBackoff, cancellationToken);

                        var clientIndex = (int)((uint)Interlocked.Increment(ref roundRobin) % poolSize);
                        var client = clients[clientIndex];
                        var request = BuildAssociateRequest(pair, relationship, bypassPlugins);

                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                client.Execute(request);
                                Interlocked.Increment(ref completed);
                                TrackCompleted(pair, completedWriter, writerLock, ref writeCount);
                                DecayThrottleBackoff(ref throttleBackoffMs);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"OK: {pair.Guid1} <-> {pair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (IsDuplicateKeyError(ex))
                            {
                                Interlocked.Increment(ref completed);
                                Interlocked.Increment(ref duplicates);
                                TrackCompleted(pair, completedWriter, writerLock, ref writeCount);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"DUPLICATE: {pair.Guid1} <-> {pair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
                            {
                                var delay = Math.Min(
                                    (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                                    MaxRetryDelayMs);
                                Interlocked.Exchange(ref throttleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                                LogMessage?.Invoke($"Retry {attempt + 1}/{maxRetries} in {delay / 1000}s: {pair.Guid1} <-> {pair.Guid2}: {GetExceptionSummary(ex)}");
                                await Task.Delay(delay, cancellationToken);

                                client = TryReconnectClient(primaryClient, clients, clientLocks, clientIndex, client, ex);
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref completed);
                                Interlocked.Increment(ref errors);
                                LogMessage?.Invoke($"FAILED after {attempt + 1} attempts: {pair.Guid1} <-> {pair.Guid2}: {GetExceptionSummary(ex)}");
                                break;
                            }
                        }

                        TryFireProgressUpdate(sw, ref lastProgressUpdateMs, completed, duplicates, errors, total);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                lock (writerLock) { completedWriter.Flush(); }
            }

            ProgressUpdated?.Invoke(completed, duplicates, errors, total);
            DisposeClonedClients(clients);

            var elapsed = sw.Elapsed;
            var successCount = completed - duplicates - errors;
            var throughput = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;
            LogMessage?.Invoke($"Complete. {successCount:N0} associated, {duplicates:N0} duplicates skipped, {errors:N0} errors. Elapsed: {elapsed:mm\\:ss}. Throughput: {throughput:F1} pairs/sec");
        }

        public int GetRecommendedParallelism(IOrganizationService service)
        {
            var client = service as CrmServiceClient;
            return client != null ? Math.Max(client.RecommendedDegreesOfParallelism, 1) : 4;
        }

        #region Extracted Methods

        private static void TuneThreadPool()
        {
            ThreadPool.SetMinThreads(MinThreadPoolSize, MinThreadPoolSize);
#pragma warning disable SYSLIB0014
            ServicePointManager.DefaultConnectionLimit = MaxConnectionLimit;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
#pragma warning restore SYSLIB0014
        }

        private static Tuple<IOrganizationService[], object[], int> InitializeClientPool(
            IOrganizationService service, int degreeOfParallelism)
        {
            var primaryClient = service as CrmServiceClient;
            int poolSize = degreeOfParallelism;

            if (primaryClient != null)
                primaryClient.EnableAffinityCookie = false;

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

            return Tuple.Create(clients, clientLocks, poolSize);
        }

        private static AssociateRequest BuildAssociateRequest(
            AssociationPair pair, RelationshipInfo relationship, bool bypassPlugins)
        {
            var request = new AssociateRequest
            {
                Target = new EntityReference(relationship.Entity1LogicalName, pair.Guid1),
                RelatedEntities = new EntityReferenceCollection
                {
                    new EntityReference(relationship.Entity2LogicalName, pair.Guid2)
                },
                Relationship = new Relationship(relationship.SchemaName)
                {
                    PrimaryEntityRole = EntityRole.Referencing
                }
            };

            if (bypassPlugins)
            {
                request.Parameters[BypassLogicParam] = BypassLogicValue;
                request.Parameters[SuppressCallbackParam] = true;
            }

            return request;
        }

        private static void DecayThrottleBackoff(ref int throttleBackoffMs)
        {
            int observed, desired;
            do
            {
                observed = Volatile.Read(ref throttleBackoffMs);
                if (observed <= 0) break;
                desired = observed / 2;
            } while (Interlocked.CompareExchange(ref throttleBackoffMs, desired, observed) != observed);
        }

        private IOrganizationService TryReconnectClient(
            CrmServiceClient primaryClient, IOrganizationService[] clients,
            object[] clientLocks, int clientIndex, IOrganizationService currentClient, Exception ex)
        {
            if (!IsConnectionError(ex) || primaryClient == null || clientIndex == 0)
                return currentClient;

            try
            {
                lock (clientLocks[clientIndex])
                {
                    if (clients[clientIndex] == currentClient)
                    {
                        var newClone = primaryClient.Clone();
                        newClone.EnableAffinityCookie = false;
                        (currentClient as IDisposable)?.Dispose();
                        clients[clientIndex] = newClone;
                    }
                    LogMessage?.Invoke($"Reconnected client {clientIndex}.");
                    return clients[clientIndex];
                }
            }
            catch
            {
                return currentClient;
            }
        }

        private void TryFireProgressUpdate(Stopwatch sw, ref long lastProgressUpdateMs,
            int completed, int duplicates, int errors, int total)
        {
            var now = sw.ElapsedMilliseconds;
            if (now - Interlocked.Read(ref lastProgressUpdateMs) > ProgressUpdateIntervalMs || completed == total)
            {
                Interlocked.Exchange(ref lastProgressUpdateMs, now);
                ProgressUpdated?.Invoke(completed, duplicates, errors, total);
            }
        }

        private static void DisposeClonedClients(IOrganizationService[] clients)
        {
            for (int i = 1; i < clients.Length; i++)
            {
                try { (clients[i] as IDisposable)?.Dispose(); }
                catch { /* best effort */ }
            }
        }

        #endregion

        #region Resume File

        private static void TrackCompleted(AssociationPair pair, StreamWriter writer, object writerLock, ref int writeCount)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lock (writerLock)
            {
                writer.WriteLine($"{pair.Guid1},{pair.Guid2},{ts}");
                writeCount++;
                if (writeCount % ResumeFlushInterval == 0)
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
                    set.Add(new AssociationPair { Guid1 = g1, Guid2 = g2 }.NormalizedKey());
                }
            }

            return set;
        }

        #endregion

        #region Error Classification

        private static string GetExceptionSummary(Exception ex)
        {
            var msg = ex.Message;
            return msg.Length > MaxExceptionMessageLength
                ? msg.Substring(0, MaxExceptionMessageLength) + "..."
                : msg;
        }

        private static bool IsTransient(Exception ex)
        {
            return IsTransientMessage(ex.Message) ||
                   (ex.InnerException != null && IsTransientMessage(ex.InnerException.Message));
        }

        private static bool IsTransientMessage(string msg)
        {
            return !string.IsNullOrEmpty(msg) &&
                   TransientPatterns.Any(p => msg.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
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
            return ex.Message.IndexOf(DuplicateKeyError, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
