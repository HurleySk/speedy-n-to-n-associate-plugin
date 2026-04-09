using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Tooling.Connector;
using SpeedyNtoNAssociatePlugin.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

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
        private const string BypassLogicParam = "BypassBusinessLogicExecution";
        private const string BypassLogicValue = "CustomSync,CustomAsync";
        private const string SuppressCallbackParam = "SuppressCallbackRegistrationExpanderJob";

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

        /// <summary>
        /// Bundles runtime state passed between internal methods.
        /// </summary>
        private class RunContext
        {
            public IOrganizationService[] Clients;
            public object[] ClientLocks;
            public int PoolSize;
            public CrmServiceClient PrimaryClient;
            public ResumeTracker ResumeTracker;
            public RunState State;
            public Stopwatch Stopwatch;
            public CancellationToken CancellationToken;
        }

        public async Task RunAsync(
            IOrganizationService service,
            IEnumerable<AssociationPair> pairsSource,
            AssociationRunOptions options,
            ResumeTracker resumeTracker,
            CancellationToken cancellationToken)
        {
            var completedSet = resumeTracker.GetCompletedSet();
            var resumedCount = completedSet.Count;

            LogMessage?.Invoke($"Already completed (from resume): {resumedCount:N0}");

            TuneThreadPool();

            var pool = InitializeClientPool(service, options.DegreeOfParallelism);

            var ctx = new RunContext
            {
                Clients = pool.Item1,
                ClientLocks = pool.Item2,
                PoolSize = pool.Item3,
                PrimaryClient = service as CrmServiceClient,
                ResumeTracker = resumeTracker,
                State = new RunState(),
                Stopwatch = Stopwatch.StartNew(),
                CancellationToken = cancellationToken
            };

            LogMessage?.Invoke($"Using {ctx.PoolSize} pooled connections, parallelism: {options.DegreeOfParallelism}, batch size: {options.BatchSize}");

            // Bounded buffer: holds enough items for all parallel workers to have full batches plus headroom
            int bufferCapacity = Math.Max(options.BatchSize * options.DegreeOfParallelism * 2, 1000);
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
                        ctx.State.TotalKnown = produced;
                        LogMessage?.Invoke($"Source enumeration complete. {produced:N0} pairs to process (after filtering {resumedCount:N0} already completed).");
                    }
                }, cancellationToken);

                // Consumer logic depends on batch size
                if (options.BatchSize <= 1)
                {
                    await RunSingleMode(buffer, options, ctx);
                }
                else
                {
                    await RunBatchMode(buffer, options, ctx);
                }

                try { await producerTask; } catch (OperationCanceledException) { }
            }

            resumeTracker.FlushBatch();

            ProgressUpdated?.Invoke(ctx.State.Completed, ctx.State.Duplicates, ctx.State.Errors, ctx.State.TotalKnown);
            DisposeClonedClients(ctx.Clients);

            var elapsed = ctx.Stopwatch.Elapsed;
            var successCount = ctx.State.Completed - ctx.State.Duplicates - ctx.State.Errors;
            var throughput = elapsed.TotalSeconds > 0 ? ctx.State.Completed / elapsed.TotalSeconds : 0;
            LogMessage?.Invoke($"Complete. {successCount:N0} associated, {ctx.State.Duplicates:N0} duplicates skipped, {ctx.State.Errors:N0} errors. Elapsed: {elapsed:mm\\:ss}. Throughput: {throughput:F1} pairs/sec");
        }

        #region Single-Request Mode (batchSize == 1)

        private async Task RunSingleMode(
            BlockingCollection<AssociationPair> buffer,
            AssociationRunOptions options, RunContext ctx)
        {
            using (var semaphore = new SemaphoreSlim(options.DegreeOfParallelism))
            {
            var tasks = new List<Task>();
            int pruneThreshold = options.DegreeOfParallelism * 10;

            foreach (var pair in buffer.GetConsumingEnumerable(ctx.CancellationToken))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                // Prune completed tasks to avoid unbounded memory growth at 1M+ scale
                if (tasks.Count > pruneThreshold)
                    tasks.RemoveAll(t => t.IsCompleted);

                await semaphore.WaitAsync(ctx.CancellationToken);

                var capturedPair = pair;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var currentBackoff = Volatile.Read(ref ctx.State.ThrottleBackoffMs);
                        if (currentBackoff > 0)
                            await Task.Delay(currentBackoff, ctx.CancellationToken);

                        var clientIndex = (int)((uint)Interlocked.Increment(ref ctx.State.RoundRobin) % ctx.PoolSize);
                        var client = ctx.Clients[clientIndex];
                        var request = BuildAssociateRequest(capturedPair, options.Relationship, options.BypassPlugins);

                        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
                        {
                            try
                            {
                                client.Execute(request);
                                Interlocked.Increment(ref ctx.State.Completed);
                                ctx.ResumeTracker.TrackCompleted(capturedPair);
                                DecayThrottleBackoff(ref ctx.State.ThrottleBackoffMs);
                                if (options.VerboseLogging)
                                    LogMessage?.Invoke($"OK: {capturedPair.Guid1} <-> {capturedPair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (ErrorClassifier.IsDuplicateKey(ex))
                            {
                                Interlocked.Increment(ref ctx.State.Completed);
                                Interlocked.Increment(ref ctx.State.Duplicates);
                                ctx.ResumeTracker.TrackCompleted(capturedPair);
                                if (options.VerboseLogging)
                                    LogMessage?.Invoke($"DUPLICATE: {capturedPair.Guid1} <-> {capturedPair.Guid2}");
                                break;
                            }
                            catch (Exception ex) when (attempt < options.MaxRetries && ErrorClassifier.IsTransient(ex))
                            {
                                var delay = Math.Min(
                                    (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                                    MaxRetryDelayMs);
                                Interlocked.Exchange(ref ctx.State.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                                LogMessage?.Invoke($"Retry {attempt + 1}/{options.MaxRetries} in {delay / 1000}s: {capturedPair.Guid1} <-> {capturedPair.Guid2}: {ErrorClassifier.GetExceptionSummary(ex)}");
                                await Task.Delay(delay, ctx.CancellationToken);

                                client = TryReconnectClient(ctx, clientIndex, client, ex);
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref ctx.State.Completed);
                                Interlocked.Increment(ref ctx.State.Errors);
                                LogMessage?.Invoke($"FAILED after {attempt + 1} attempts: {capturedPair.Guid1} <-> {capturedPair.Guid2}: {ErrorClassifier.GetExceptionSummary(ex)}");
                                break;
                            }
                        }

                        TryFireProgressUpdate(ctx);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ctx.CancellationToken));
            }

            await Task.WhenAll(tasks);
            } // using semaphore
        }

        #endregion

        #region Batch Mode (ExecuteMultiple)

        private async Task RunBatchMode(
            BlockingCollection<AssociationPair> buffer,
            AssociationRunOptions options, RunContext ctx)
        {
            using (var semaphore = new SemaphoreSlim(options.DegreeOfParallelism))
            {
            var tasks = new List<Task>();
            int pruneThreshold = options.DegreeOfParallelism * 10;

            var currentBatch = new List<AssociationPair>(options.BatchSize);

            foreach (var pair in buffer.GetConsumingEnumerable(ctx.CancellationToken))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                currentBatch.Add(pair);

                if (currentBatch.Count >= options.BatchSize)
                {
                    var batchToProcess = currentBatch;
                    currentBatch = new List<AssociationPair>(options.BatchSize);

                    // Prune completed tasks to avoid unbounded memory growth
                    if (tasks.Count > pruneThreshold)
                        tasks.RemoveAll(t => t.IsCompleted);

                    await semaphore.WaitAsync(ctx.CancellationToken);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessBatch(batchToProcess, options, ctx);
                            TryFireProgressUpdate(ctx);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, ctx.CancellationToken));
                }
            }

            // Process remaining items in the last partial batch
            if (currentBatch.Count > 0)
            {
                await semaphore.WaitAsync(ctx.CancellationToken);
                var finalBatch = currentBatch;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessBatch(finalBatch, options, ctx);
                        TryFireProgressUpdate(ctx);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ctx.CancellationToken));
            }

            await Task.WhenAll(tasks);
            } // using semaphore
        }

        private async Task ProcessBatch(
            List<AssociationPair> batch,
            AssociationRunOptions options, RunContext ctx)
        {
            var currentBackoff = Volatile.Read(ref ctx.State.ThrottleBackoffMs);
            if (currentBackoff > 0)
                await Task.Delay(currentBackoff, ctx.CancellationToken);

            var clientIndex = (int)((uint)Interlocked.Increment(ref ctx.State.RoundRobin) % ctx.PoolSize);
            var client = ctx.Clients[clientIndex];

            var pendingPairs = new List<AssociationPair>(batch);

            for (int attempt = 0; attempt <= options.MaxRetries && pendingPairs.Count > 0; attempt++)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var multiRequest = BuildExecuteMultipleBatch(pendingPairs, options.Relationship, options.BypassPlugins, options.FireAndForget);

                try
                {
                    var response = (ExecuteMultipleResponse)client.Execute(multiRequest);

                    if (options.FireAndForget)
                    {
                        // No per-item responses — treat entire batch as succeeded
                        foreach (var pair in pendingPairs)
                        {
                            Interlocked.Increment(ref ctx.State.Completed);
                            ctx.ResumeTracker.TrackCompleted(pair);
                        }
                        DecayThrottleBackoff(ref ctx.State.ThrottleBackoffMs);
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
                            Interlocked.Increment(ref ctx.State.Completed);
                            ctx.ResumeTracker.TrackCompleted(pair);
                            DecayThrottleBackoff(ref ctx.State.ThrottleBackoffMs);
                            if (options.VerboseLogging)
                                LogMessage?.Invoke($"OK: {pair.Guid1} <-> {pair.Guid2}");
                        }
                        else if (ErrorClassifier.IsDuplicateKeyFault(itemResponse.Fault))
                        {
                            Interlocked.Increment(ref ctx.State.Completed);
                            Interlocked.Increment(ref ctx.State.Duplicates);
                            ctx.ResumeTracker.TrackCompleted(pair);
                            if (options.VerboseLogging)
                                LogMessage?.Invoke($"DUPLICATE: {pair.Guid1} <-> {pair.Guid2}");
                        }
                        else if (attempt < options.MaxRetries && ErrorClassifier.IsTransientFault(itemResponse.Fault))
                        {
                            retryList.Add(pair);
                        }
                        else
                        {
                            Interlocked.Increment(ref ctx.State.Completed);
                            Interlocked.Increment(ref ctx.State.Errors);
                            var msg = itemResponse.Fault.Message ?? "Unknown error";
                            if (msg.Length > 200)
                                msg = msg.Substring(0, 200) + "...";
                            LogMessage?.Invoke($"FAILED: {pair.Guid1} <-> {pair.Guid2}: {msg}");
                        }
                    }

                    if (retryList.Count == 0)
                        break;

                    var delay = Math.Min(
                        (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                        MaxRetryDelayMs);
                    Interlocked.Exchange(ref ctx.State.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                    LogMessage?.Invoke($"Batch retry {attempt + 1}/{options.MaxRetries}: {retryList.Count} transient failures, backoff {delay / 1000}s");
                    await Task.Delay(delay, ctx.CancellationToken);

                    pendingPairs = retryList;
                }
                catch (Exception ex) when (attempt < options.MaxRetries && ErrorClassifier.IsTransient(ex))
                {
                    var delay = Math.Min(
                        (int)(Math.Pow(2, attempt) * BackoffBaseMs) + Rng.Value.Next(BackoffJitterMs),
                        MaxRetryDelayMs);
                    Interlocked.Exchange(ref ctx.State.ThrottleBackoffMs, Math.Min(delay, MaxThrottleBackoffMs));
                    LogMessage?.Invoke($"Batch call failed (transient), retry {attempt + 1}/{options.MaxRetries} in {delay / 1000}s: {ErrorClassifier.GetExceptionSummary(ex)}");
                    await Task.Delay(delay, ctx.CancellationToken);

                    client = TryReconnectClient(ctx, clientIndex, client, ex);
                }
                catch (Exception ex)
                {
                    foreach (var pair in pendingPairs)
                    {
                        Interlocked.Increment(ref ctx.State.Completed);
                        Interlocked.Increment(ref ctx.State.Errors);
                    }
                    LogMessage?.Invoke($"Batch FAILED permanently ({pendingPairs.Count} pairs): {ErrorClassifier.GetExceptionSummary(ex)}");
                    break;
                }
            }
        }

        private static ExecuteMultipleRequest BuildExecuteMultipleBatch(
            List<AssociationPair> pairs, RelationshipInfo relationship, bool bypassPlugins,
            bool fireAndForget)
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
                multiRequest.Requests.Add(BuildAssociateRequest(pair, relationship, bypassPlugins));
            }

            return multiRequest;
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

        // NOTE: Direct CreateRequest on intersect entities is NOT supported by Dataverse.
        // The platform does not register the Create message on intersect entity types.
        // AssociateRequest is the only supported way to create N:N relationships.

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
            RunContext ctx, int clientIndex, IOrganizationService currentClient, Exception ex)
        {
            if (!ErrorClassifier.IsConnectionError(ex) || ctx.PrimaryClient == null || clientIndex == 0)
                return currentClient;

            try
            {
                lock (ctx.ClientLocks[clientIndex])
                {
                    if (ctx.Clients[clientIndex] == currentClient)
                    {
                        var newClone = ctx.PrimaryClient.Clone();
                        newClone.EnableAffinityCookie = false;
                        (currentClient as IDisposable)?.Dispose();
                        ctx.Clients[clientIndex] = newClone;
                    }
                    LogMessage?.Invoke($"Reconnected client {clientIndex}.");
                    return ctx.Clients[clientIndex];
                }
            }
            catch
            {
                return currentClient;
            }
        }

        private void TryFireProgressUpdate(RunContext ctx)
        {
            var now = ctx.Stopwatch.ElapsedMilliseconds;
            if (now - Interlocked.Read(ref ctx.State.LastProgressUpdateMs) > ProgressUpdateIntervalMs || (ctx.State.TotalKnown.HasValue && ctx.State.Completed == ctx.State.TotalKnown.Value))
            {
                Interlocked.Exchange(ref ctx.State.LastProgressUpdateMs, now);
                ProgressUpdated?.Invoke(ctx.State.Completed, ctx.State.Duplicates, ctx.State.Errors, ctx.State.TotalKnown);
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
    }
}
