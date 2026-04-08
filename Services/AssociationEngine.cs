using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Tooling.Connector;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedyNtoNAssociatePlugin.Services
{
    public class AssociationEngine
    {
        // Events — nullable total means "still loading from source"
        public event Action<int, int, int, int?> ProgressUpdated;
        public event Action<string> LogMessage;

        // Constants
        private const int MinThreadPoolSize = 100;
        private const int MaxConnectionLimit = 65000;
        private const int BackoffBaseMs = 2000;
        private const int BackoffJitterMs = 3000;
        private const int MaxRetryDelayMs = 60000;
        private const int MaxThrottleBackoffMs = 30000;
        private const long ProgressUpdateIntervalMs = 100;
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

        /// <summary>
        /// Thread-safe counters shared across all workers.
        /// </summary>
        private class RunState
        {
            public int Completed;
            public int Duplicates;
            public int Errors;
            public int ThrottleBackoffMs;
            public long LastProgressUpdateMs;
            public int? TotalKnown;
            public int RoundRobin = -1;
        }

        public async Task RunAsync(
            IOrganizationService service,
            IEnumerable<AssociationPair> pairsSource,
            RelationshipInfo relationship,
            int degreeOfParallelism,
            ResumeTracker resumeTracker,
            bool bypassPlugins,
            bool verboseLogging,
            int maxRetries,
            int batchSize,
            bool fireAndForget,
            bool useDirectInsert,
            CancellationToken cancellationToken)
        {
            var completedSet = resumeTracker.GetCompletedSet();
            var resumedCount = completedSet.Count;

            LogMessage?.Invoke($"Already completed (from resume): {resumedCount:N0}");

            TuneThreadPool();

            var pool = InitializeClientPool(service, degreeOfParallelism);
            var clients = pool.Item1;
            var clientLocks = pool.Item2;
            int poolSize = pool.Item3;
            var primaryClient = service as CrmServiceClient;

            LogMessage?.Invoke($"Using {poolSize} pooled connections, parallelism: {degreeOfParallelism}, batch size: {batchSize}");

            var state = new RunState();
            var sw = Stopwatch.StartNew();

            // Bounded buffer: holds enough items for all parallel workers to have full batches plus headroom
            int bufferCapacity = Math.Max(batchSize * degreeOfParallelism * 2, 1000);
            using (var buffer = new BlockingCollection<AssociationPair>(bufferCapacity))
            {
                // Producer: enumerate source, filter completed, feed into buffer
                var producerTask = Task.Run(() =>
                {
                    int produced = 0;
                    try
                    {
                        foreach (var pair in pairsSource)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!completedSet.Contains(pair.NormalizedKey()))
                            {
                                buffer.Add(pair, cancellationToken);
                                produced++;
                            }
                        }
                    }
                    finally
                    {
                        buffer.CompleteAdding();
                        state.TotalKnown = produced;
                        LogMessage?.Invoke($"Source enumeration complete. {produced:N0} pairs to process (after filtering {resumedCount:N0} already completed).");
                    }
                }, cancellationToken);

                // Consumer logic depends on batch size
                if (batchSize <= 1)
                {
                    await RunSingleMode(buffer, clients, clientLocks, poolSize, primaryClient,
                        relationship, bypassPlugins, verboseLogging, maxRetries, useDirectInsert,
                        resumeTracker, state, sw, degreeOfParallelism, cancellationToken);
                }
                else
                {
                    await RunBatchMode(buffer, clients, clientLocks, poolSize, primaryClient,
                        relationship, bypassPlugins, verboseLogging, maxRetries, batchSize,
                        fireAndForget, useDirectInsert, resumeTracker,
                        state, sw, degreeOfParallelism, cancellationToken);
                }

                try { await producerTask; } catch (OperationCanceledException) { }
            }

            resumeTracker.FlushBatch();

            ProgressUpdated?.Invoke(state.Completed, state.Duplicates, state.Errors, state.TotalKnown);
            DisposeClonedClients(clients);

            var elapsed = sw.Elapsed;
            var successCount = state.Completed - state.Duplicates - state.Errors;
            var throughput = elapsed.TotalSeconds > 0 ? state.Completed / elapsed.TotalSeconds : 0;
            LogMessage?.Invoke($"Complete. {successCount:N0} associated, {state.Duplicates:N0} duplicates skipped, {state.Errors:N0} errors. Elapsed: {elapsed:mm\\:ss}. Throughput: {throughput:F1} pairs/sec");
        }

        #region Single-Request Mode (batchSize == 1)

        private async Task RunSingleMode(
            BlockingCollection<AssociationPair> buffer,
            IOrganizationService[] clients, object[] clientLocks, int poolSize,
            CrmServiceClient primaryClient, RelationshipInfo relationship,
            bool bypassPlugins, bool verboseLogging, int maxRetries,
            bool useDirectInsert, ResumeTracker resumeTracker, RunState state,
            Stopwatch sw, int degreeOfParallelism, CancellationToken cancellationToken)
        {
            using (var semaphore = new SemaphoreSlim(degreeOfParallelism))
            {
            var tasks = new List<Task>();
            int pruneThreshold = degreeOfParallelism * 10;

            foreach (var pair in buffer.GetConsumingEnumerable(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Prune completed tasks to avoid unbounded memory growth at 1M+ scale
                if (tasks.Count > pruneThreshold)
                    tasks.RemoveAll(t => t.IsCompleted);

                await semaphore.WaitAsync(cancellationToken);

                var capturedPair = pair;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var currentBackoff = Volatile.Read(ref state.ThrottleBackoffMs);
                        if (currentBackoff > 0)
                            await Task.Delay(currentBackoff, cancellationToken);

                        var clientIndex = (int)((uint)Interlocked.Increment(ref state.RoundRobin) % poolSize);
                        var client = clients[clientIndex];
                        var request = useDirectInsert
                            ? (OrganizationRequest)BuildCreateIntersectRequest(capturedPair, relationship, bypassPlugins)
                            : BuildAssociateRequest(capturedPair, relationship, bypassPlugins);

                        for (int attempt = 0; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                client.Execute(request);
                                Interlocked.Increment(ref state.Completed);
                                resumeTracker.TrackCompleted(capturedPair);
                                DecayThrottleBackoff(ref state.ThrottleBackoffMs);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"OK: {capturedPair.Guid1} <-> {capturedPair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (IsDuplicateKeyError(ex))
                            {
                                Interlocked.Increment(ref state.Completed);
                                Interlocked.Increment(ref state.Duplicates);
                                resumeTracker.TrackCompleted(capturedPair);
                                if (verboseLogging)
                                    LogMessage?.Invoke($"DUPLICATE: {capturedPair.Guid1} <-> {capturedPair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
                            {
                                var delay = Math.Min(
                                    (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                                    MaxRetryDelayMs);
                                Interlocked.Exchange(ref state.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                                LogMessage?.Invoke($"Retry {attempt + 1}/{maxRetries} in {delay / 1000}s: {capturedPair.Guid1} <-> {capturedPair.Guid2}: {GetExceptionSummary(ex)}");
                                await Task.Delay(delay, cancellationToken);

                                client = TryReconnectClient(primaryClient, clients, clientLocks, clientIndex, client, ex);
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref state.Completed);
                                Interlocked.Increment(ref state.Errors);
                                LogMessage?.Invoke($"FAILED after {attempt + 1} attempts: {capturedPair.Guid1} <-> {capturedPair.Guid2}: {GetExceptionSummary(ex)}");
                                break;
                            }
                        }

                        TryFireProgressUpdate(sw, ref state.LastProgressUpdateMs, state.Completed, state.Duplicates, state.Errors, state.TotalKnown);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            } // using semaphore
        }

        #endregion

        #region Batch Mode (ExecuteMultiple)

        private async Task RunBatchMode(
            BlockingCollection<AssociationPair> buffer,
            IOrganizationService[] clients, object[] clientLocks, int poolSize,
            CrmServiceClient primaryClient, RelationshipInfo relationship,
            bool bypassPlugins, bool verboseLogging, int maxRetries, int batchSize,
            bool fireAndForget, bool useDirectInsert, ResumeTracker resumeTracker,
            RunState state, Stopwatch sw,
            int degreeOfParallelism, CancellationToken cancellationToken)
        {
            using (var semaphore = new SemaphoreSlim(degreeOfParallelism))
            {
            var tasks = new List<Task>();
            int pruneThreshold = degreeOfParallelism * 10;

            var currentBatch = new List<AssociationPair>(batchSize);

            foreach (var pair in buffer.GetConsumingEnumerable(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentBatch.Add(pair);

                if (currentBatch.Count >= batchSize)
                {
                    var batchToProcess = currentBatch;
                    currentBatch = new List<AssociationPair>(batchSize);

                    // Prune completed tasks to avoid unbounded memory growth
                    if (tasks.Count > pruneThreshold)
                        tasks.RemoveAll(t => t.IsCompleted);

                    await semaphore.WaitAsync(cancellationToken);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessBatch(batchToProcess, clients, clientLocks, poolSize,
                                primaryClient, relationship, bypassPlugins, verboseLogging, maxRetries,
                                fireAndForget, useDirectInsert, resumeTracker, state, cancellationToken);
                            TryFireProgressUpdate(sw, ref state.LastProgressUpdateMs, state.Completed, state.Duplicates, state.Errors, state.TotalKnown);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }
            }

            // Process remaining items in the last partial batch
            if (currentBatch.Count > 0)
            {
                await semaphore.WaitAsync(cancellationToken);
                var finalBatch = currentBatch;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessBatch(finalBatch, clients, clientLocks, poolSize,
                            primaryClient, relationship, bypassPlugins, verboseLogging, maxRetries,
                            fireAndForget, useDirectInsert, resumeTracker, state, cancellationToken);
                        TryFireProgressUpdate(sw, ref state.LastProgressUpdateMs, state.Completed, state.Duplicates, state.Errors, state.TotalKnown);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            } // using semaphore
        }

        private async Task ProcessBatch(
            List<AssociationPair> batch,
            IOrganizationService[] clients, object[] clientLocks, int poolSize,
            CrmServiceClient primaryClient, RelationshipInfo relationship,
            bool bypassPlugins, bool verboseLogging, int maxRetries,
            bool fireAndForget, bool useDirectInsert,
            ResumeTracker resumeTracker, RunState state,
            CancellationToken cancellationToken)
        {
            var currentBackoff = Volatile.Read(ref state.ThrottleBackoffMs);
            if (currentBackoff > 0)
                await Task.Delay(currentBackoff, cancellationToken);

            var clientIndex = (int)((uint)Interlocked.Increment(ref state.RoundRobin) % poolSize);
            var client = clients[clientIndex];

            var pendingPairs = new List<AssociationPair>(batch);

            for (int attempt = 0; attempt <= maxRetries && pendingPairs.Count > 0; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var multiRequest = BuildExecuteMultipleBatch(pendingPairs, relationship, bypassPlugins, useDirectInsert, fireAndForget);

                try
                {
                    var response = (ExecuteMultipleResponse)client.Execute(multiRequest);

                    if (fireAndForget)
                    {
                        // No per-item responses — treat entire batch as succeeded
                        foreach (var pair in pendingPairs)
                        {
                            Interlocked.Increment(ref state.Completed);
                            resumeTracker.TrackCompleted(pair);
                        }
                        DecayThrottleBackoff(ref state.ThrottleBackoffMs);
                        break;
                    }

                    var retryList = new List<AssociationPair>();

                    // O(1) lookup instead of O(n) scan per item
                    var responseMap = new Dictionary<int, ExecuteMultipleResponseItem>(response.Responses.Count);
                    foreach (var r in response.Responses)
                        responseMap[r.RequestIndex] = r;

                    for (int i = 0; i < pendingPairs.Count; i++)
                    {
                        var pair = pendingPairs[i];
                        responseMap.TryGetValue(i, out var itemResponse);

                        if (itemResponse == null || itemResponse.Fault == null)
                        {
                            Interlocked.Increment(ref state.Completed);
                            resumeTracker.TrackCompleted(pair);
                            DecayThrottleBackoff(ref state.ThrottleBackoffMs);
                            if (verboseLogging)
                                LogMessage?.Invoke($"OK: {pair.Guid1} <-> {pair.Guid2}");
                        }
                        else if (IsDuplicateKeyFault(itemResponse.Fault))
                        {
                            Interlocked.Increment(ref state.Completed);
                            Interlocked.Increment(ref state.Duplicates);
                            resumeTracker.TrackCompleted(pair);
                            if (verboseLogging)
                                LogMessage?.Invoke($"DUPLICATE: {pair.Guid1} <-> {pair.Guid2}");
                        }
                        else if (attempt < maxRetries && IsTransientFault(itemResponse.Fault))
                        {
                            retryList.Add(pair);
                        }
                        else
                        {
                            Interlocked.Increment(ref state.Completed);
                            Interlocked.Increment(ref state.Errors);
                            var msg = itemResponse.Fault.Message ?? "Unknown error";
                            if (msg.Length > MaxExceptionMessageLength)
                                msg = msg.Substring(0, MaxExceptionMessageLength) + "...";
                            LogMessage?.Invoke($"FAILED: {pair.Guid1} <-> {pair.Guid2}: {msg}");
                        }
                    }

                    if (retryList.Count == 0)
                        break;

                    var delay = Math.Min(
                        (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                        MaxRetryDelayMs);
                    Interlocked.Exchange(ref state.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                    LogMessage?.Invoke($"Batch retry {attempt + 1}/{maxRetries}: {retryList.Count} transient failures, backoff {delay / 1000}s");
                    await Task.Delay(delay, cancellationToken);

                    pendingPairs = retryList;
                }
                catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
                {
                    var delay = Math.Min(
                        (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                        MaxRetryDelayMs);
                    Interlocked.Exchange(ref state.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                    LogMessage?.Invoke($"Batch call failed (transient), retry {attempt + 1}/{maxRetries} in {delay / 1000}s: {GetExceptionSummary(ex)}");
                    await Task.Delay(delay, cancellationToken);

                    client = TryReconnectClient(primaryClient, clients, clientLocks, clientIndex, client, ex);
                }
                catch (Exception ex)
                {
                    foreach (var pair in pendingPairs)
                    {
                        Interlocked.Increment(ref state.Completed);
                        Interlocked.Increment(ref state.Errors);
                    }
                    LogMessage?.Invoke($"Batch FAILED permanently ({pendingPairs.Count} pairs): {GetExceptionSummary(ex)}");
                    break;
                }
            }
        }

        private static ExecuteMultipleRequest BuildExecuteMultipleBatch(
            List<AssociationPair> pairs, RelationshipInfo relationship, bool bypassPlugins,
            bool useDirectInsert, bool fireAndForget)
        {
            var multiRequest = new ExecuteMultipleRequest
            {
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = !fireAndForget,
                    ReturnResponses = !fireAndForget
                },
                Requests = new OrganizationRequestCollection()
            };

            foreach (var pair in pairs)
            {
                multiRequest.Requests.Add(useDirectInsert
                    ? (OrganizationRequest)BuildCreateIntersectRequest(pair, relationship, bypassPlugins)
                    : BuildAssociateRequest(pair, relationship, bypassPlugins));
            }

            return multiRequest;
        }

        private static bool IsDuplicateKeyFault(OrganizationServiceFault fault)
        {
            if (fault == null) return false;
            var msg = fault.Message ?? "";
            return msg.IndexOf(DuplicateKeyError, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTransientFault(OrganizationServiceFault fault)
        {
            if (fault == null) return false;
            var msg = fault.Message ?? "";
            return TransientPatterns.Any(p => msg.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion

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

        private static CreateRequest BuildCreateIntersectRequest(
            AssociationPair pair, RelationshipInfo relationship, bool bypassPlugins)
        {
            var intersectRecord = new Entity(relationship.IntersectEntityName);
            intersectRecord[relationship.Entity1IntersectAttribute] = pair.Guid1;
            intersectRecord[relationship.Entity2IntersectAttribute] = pair.Guid2;

            var request = new CreateRequest { Target = intersectRecord };

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
            int completed, int duplicates, int errors, int? total)
        {
            var now = sw.ElapsedMilliseconds;
            if (now - Interlocked.Read(ref lastProgressUpdateMs) > ProgressUpdateIntervalMs || (total.HasValue && completed == total.Value))
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
