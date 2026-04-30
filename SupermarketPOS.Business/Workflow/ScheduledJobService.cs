using Newtonsoft.Json;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4.5, Step 7: Background service that executes scheduled jobs.
    /// Polls ScheduledJobs table, evaluates CronExpressions, and runs due actions.
    /// </summary>
    public sealed class ScheduledJobService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly IGenericDataService _dataService;
        private CancellationTokenSource _cts;
        private Task _runningTask;

        public ScheduledJobService(
            Func<AppDbContext> dbFactory,
            IGenericDataService dataService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        public void Start()
        {
            if (_runningTask != null) return;
            _cts = new CancellationTokenSource();
            _runningTask = RunLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _runningTask = null;
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueJobsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task ProcessDueJobsAsync(CancellationToken cancellationToken)
        {
            var jobs = LoadDueJobs();
            foreach (var job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteJobAsync(job).ConfigureAwait(false);
            }
        }

        private async Task ExecuteJobAsync(ScheduledJob job)
        {
            var startTime = DateTime.UtcNow;
            string status = "Success";
            string error = null;

            try
            {
                var parameters = !string.IsNullOrWhiteSpace(job.ParametersJson)
                    ? JsonConvert.DeserializeObject<Dictionary<string, object>>(job.ParametersJson)
                    : new Dictionary<string, object>();

                await _dataService.ExecuteActionAsync(
                    job.EntityName, job.ActionName, parameters)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                status = "Failed";
                error = ex.Message;
            }

            UpdateJobStatus(job.Id, startTime, status, error, job.CronExpression);
        }

        private List<ScheduledJob> LoadDueJobs()
        {
            var jobs = new List<ScheduledJob>();
            var now = DateTime.UtcNow;

            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Id, Name, CronExpression, ActionName, EntityName, 
                                   ParametersJson, LastRunAt, NextRunAt
                            FROM [ScheduledJobs]
                            WHERE IsActive = 1
                              AND (NextRunAt IS NULL OR NextRunAt <= @now)";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@now", now));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var job = new ScheduledJob
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    CronExpression = reader.GetString(reader.GetOrdinal("CronExpression")),
                                    ActionName = reader.GetString(reader.GetOrdinal("ActionName")),
                                    EntityName = reader.GetString(reader.GetOrdinal("EntityName")),
                                    ParametersJson = reader.IsDBNull(reader.GetOrdinal("ParametersJson"))
                                        ? null : reader.GetString(reader.GetOrdinal("ParametersJson"))
                                };

                                if (CronParser.IsDue(job.CronExpression, now))
                                    jobs.Add(job);
                            }
                        }
                    }
                }
            }
            catch (Exception) { }

            return jobs;
        }

        private void UpdateJobStatus(int jobId, DateTime ranAt, string status, string error, string cronExpression)
        {
            try
            {
                var nextRun = CronParser.GetNextRun(cronExpression, ranAt);

                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE [ScheduledJobs]
                            SET LastRunAt = @ranAt,
                                LastRunStatus = @status,
                                LastRunError = @error,
                                NextRunAt = @nextRun
                            WHERE Id = @id";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@ranAt", ranAt));
                        cmd.Parameters.Add(new SqlParameter("@status", status));
                        cmd.Parameters.Add(new SqlParameter("@error", (object)error ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@nextRun", (object)nextRun ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@id", jobId));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
