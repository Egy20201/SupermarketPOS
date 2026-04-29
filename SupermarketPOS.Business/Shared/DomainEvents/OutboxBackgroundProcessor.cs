using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business
{
    public class OutboxBackgroundProcessor : IDisposable
    {
        private const int MaxRetries = 5;
        private const int BatchSize = 20;
        private const int BackpressureQueueThreshold = 500;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan BackpressurePollingInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MetricsLogInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

        private readonly Func<AppDbContext> _dbFactory;
        private readonly IEnumerable<IDomainEventHandler<DeliveryPostedEvent>> _deliveryHandlers;
        private readonly IEnumerable<IDomainEventHandler<GoodsReceiptPostedEvent>> _goodsReceiptHandlers;
        private readonly IEnumerable<IDomainEventHandler<CreditNotePostedEvent>> _creditNoteHandlers;
        private readonly IEnumerable<IDomainEventHandler<DebitNotePostedEvent>> _debitNoteHandlers;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly OutboxProcessorMetrics _metrics = new OutboxProcessorMetrics();

        private CancellationTokenSource _cts;
        private Task _executingTask;
        private int _started;
        private bool _disposed;

        public OutboxBackgroundProcessor(
            Func<AppDbContext> dbFactory,
            IEnumerable<IDomainEventHandler<DeliveryPostedEvent>> deliveryHandlers,
            IEnumerable<IDomainEventHandler<GoodsReceiptPostedEvent>> goodsReceiptHandlers,
            IEnumerable<IDomainEventHandler<CreditNotePostedEvent>> creditNoteHandlers,
            IEnumerable<IDomainEventHandler<DebitNotePostedEvent>> debitNoteHandlers)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _deliveryHandlers = deliveryHandlers ?? throw new ArgumentNullException(nameof(deliveryHandlers));
            _goodsReceiptHandlers = goodsReceiptHandlers ?? throw new ArgumentNullException(nameof(goodsReceiptHandlers));
            _creditNoteHandlers = creditNoteHandlers ?? throw new ArgumentNullException(nameof(creditNoteHandlers));
            _debitNoteHandlers = debitNoteHandlers ?? throw new ArgumentNullException(nameof(debitNoteHandlers));
        }

        public OutboxProcessorMetrics Metrics => _metrics;

        public bool IsRunning => Interlocked.CompareExchange(ref _started, 0, 0) == 1;

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                return;

            _cts = new CancellationTokenSource();
            _executingTask = Task.Run(() => ExecuteAsync(_cts.Token));
            Logger.Info("OutboxBackgroundProcessor started.");
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _started, 0, 1) != 1)
                return;

            try
            {
                _cts?.Cancel();
                _executingTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _executingTask = null;
            }

            Logger.Info("OutboxBackgroundProcessor stopped.");
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(InitialDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var lastMetricsLog = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingEventsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "OutboxBackgroundProcessor cycle failed");
                }

                if (lastMetricsLog.Elapsed >= MetricsLogInterval)
                {
                    Logger.Info("OutboxMetrics: " + _metrics.ToString());
                    lastMetricsLog.Restart();
                }

                var delay = _metrics.LastQueueSize >= BackpressureQueueThreshold
                    ? BackpressurePollingInterval
                    : PollingInterval;

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessPendingEventsAsync(CancellationToken cancellationToken)
        {
            if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
                return;

            try
            {
                List<int> eventIds;

                using (var db = _dbFactory())
                {
                    var retryBefore = DateTime.UtcNow.Add(-StaleThreshold);
                    eventIds = db.OutboxEvents
                        .Where(e => e.RetryCount < MaxRetries &&
                                    (e.Status == OutboxStatus.Pending ||
                                     e.Status == OutboxStatus.Failed ||
                                     (e.Status == OutboxStatus.Processing && (!e.LastAttemptAt.HasValue || e.LastAttemptAt.Value < retryBefore))))
                        .OrderBy(e => e.CreatedAt)
                        .Take(BatchSize)
                        .Select(e => e.Id)
                        .ToList();
                }

                _metrics.RecordQueueSize(eventIds.Count);

                foreach (var id in eventIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProcessEvent(id);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        private void ProcessEvent(int outboxEventId)
        {
            var sw = Stopwatch.StartNew();

            using (var db = _dbFactory())
            {
                var outboxEvent = db.OutboxEvents.Find(outboxEventId);
                if (outboxEvent == null || outboxEvent.Status == OutboxStatus.Processed || outboxEvent.RetryCount >= MaxRetries) return;

                outboxEvent.Status = OutboxStatus.Processing;
                outboxEvent.RetryCount++;
                outboxEvent.LastAttemptAt = DateTime.UtcNow;
                db.SaveChanges();
            }

            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                var outboxEvent = db.OutboxEvents.Find(outboxEventId);
                if (outboxEvent == null || outboxEvent.Status == OutboxStatus.Processed)
                {
                    tx.Commit();
                    return;
                }

                try
                {
                    Dispatch(db, outboxEvent);
                    outboxEvent.Status = OutboxStatus.Processed;
                    outboxEvent.ProcessedAt = DateTime.UtcNow;
                    outboxEvent.LastError = null;
                    db.SaveChanges();
                    tx.Commit();

                    sw.Stop();
                    _metrics.RecordProcessed(sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    sw.Stop();
                    _metrics.RecordFailed(sw.ElapsedMilliseconds);
                    MarkFailed(outboxEventId, ex);
                }
            }
        }

        private void Dispatch(AppDbContext db, OutboxEvent outboxEvent)
        {
            switch (outboxEvent.EventType)
            {
                case nameof(DeliveryPostedEvent):
                    var deliveryPayload = JsonConvert.DeserializeObject<DeliveryPostedPayload>(outboxEvent.Payload);
                    var delivery = db.Deliveries.Include(d => d.Items).FirstOrDefault(d => d.Id == deliveryPayload.DeliveryId);
                    foreach (var handler in _deliveryHandlers) handler.Handle(new DeliveryPostedEvent(db, delivery));
                    break;

                case nameof(GoodsReceiptPostedEvent):
                    var receiptPayload = JsonConvert.DeserializeObject<GoodsReceiptPostedPayload>(outboxEvent.Payload);
                    var receipt = db.GoodsReceipts.Include(g => g.Items).FirstOrDefault(g => g.Id == receiptPayload.ReceiptId);
                    foreach (var handler in _goodsReceiptHandlers) handler.Handle(new GoodsReceiptPostedEvent(db, receipt));
                    break;

                case nameof(CreditNotePostedEvent):
                    var creditPayload = JsonConvert.DeserializeObject<CreditNotePostedPayload>(outboxEvent.Payload);
                    var creditNote = db.CreditNotes.Include(c => c.Items).FirstOrDefault(c => c.Id == creditPayload.CreditNoteId);
                    foreach (var handler in _creditNoteHandlers) handler.Handle(new CreditNotePostedEvent(db, creditNote, creditPayload.WarehouseId));
                    break;

                case nameof(DebitNotePostedEvent):
                    var debitPayload = JsonConvert.DeserializeObject<DebitNotePostedPayload>(outboxEvent.Payload);
                    var debitNote = db.DebitNotes.Include(d => d.Items).FirstOrDefault(d => d.Id == debitPayload.DebitNoteId);
                    foreach (var handler in _debitNoteHandlers) handler.Handle(new DebitNotePostedEvent(db, debitNote, debitPayload.WarehouseId));
                    break;

                default:
                    throw new InvalidOperationException("Unknown outbox event type: " + outboxEvent.EventType);
            }
        }

        private void MarkFailed(int outboxEventId, Exception ex)
        {
            using (var db = _dbFactory())
            {
                var outboxEvent = db.OutboxEvents.Find(outboxEventId);
                if (outboxEvent == null) return;

                var isDeadLetter = outboxEvent.RetryCount >= MaxRetries;
                outboxEvent.Status = isDeadLetter ? OutboxStatus.Failed : OutboxStatus.Pending;
                outboxEvent.LastError = ex.ToString();
                outboxEvent.LastAttemptAt = DateTime.UtcNow;
                db.SaveChanges();

                if (isDeadLetter)
                {
                    Logger.Fatal(ex, $"DEAD LETTER: Outbox event permanently failed after {MaxRetries} retries. Id={outboxEventId}, Type={outboxEvent.EventType}");
                }
                else
                {
                    Logger.Error(ex, "Outbox event processing failed. Id=" + outboxEventId);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _gate.Dispose();
        }
    }

    public class OutboxProcessorMetrics
    {
        private long _totalProcessed;
        private long _totalFailed;
        private long _totalLatencyMs;
        private int _lastQueueSize;

        public long TotalProcessed => Interlocked.Read(ref _totalProcessed);
        public long TotalFailed => Interlocked.Read(ref _totalFailed);
        public int LastQueueSize => Volatile.Read(ref _lastQueueSize);

        public double AverageLatencyMs
        {
            get
            {
                var total = Interlocked.Read(ref _totalProcessed) + Interlocked.Read(ref _totalFailed);
                return total == 0 ? 0 : (double)Interlocked.Read(ref _totalLatencyMs) / total;
            }
        }

        internal void RecordProcessed(long latencyMs)
        {
            Interlocked.Increment(ref _totalProcessed);
            Interlocked.Add(ref _totalLatencyMs, latencyMs);
        }

        internal void RecordFailed(long latencyMs)
        {
            Interlocked.Increment(ref _totalFailed);
            Interlocked.Add(ref _totalLatencyMs, latencyMs);
        }

        internal void RecordQueueSize(int size)
        {
            Volatile.Write(ref _lastQueueSize, size);
        }

        public override string ToString()
        {
            return $"Processed={TotalProcessed}, Failed={TotalFailed}, QueueSize={LastQueueSize}, AvgLatencyMs={AverageLatencyMs:F1}";
        }
    }
}
