using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImapSyncWin
{
    public class OverallProgressEventArgs : EventArgs
    {
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public int MovedMessages { get; set; }
        public int SkippedMessages { get; set; }
        public int ErrorMessages { get; set; }

        public double Percentage
        {
            get { return TotalMessages > 0 ? (ProcessedMessages * 100.0 / TotalMessages) : 0; }
        }

        public double MessagesPerSecond { get; set; }
        public string TimeRemainingText { get; set; }
        public string CurrentAction { get; set; }
    }

    public class FolderProgressEventArgs : EventArgs
    {
        public string FolderName { get; set; }
        public string DestFolder { get; set; }
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Moved { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public string Status { get; set; }

        public double Percentage
        {
            get { return Total > 0 ? (Processed * 100.0 / Total) : 0; }
        }
    }

    public class LogEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string Level { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MigrationEngine
    {
        public event EventHandler<OverallProgressEventArgs> OverallProgressChanged;
        public event EventHandler<FolderProgressEventArgs> FolderProgressChanged;
        public event EventHandler<LogEventArgs> LogEmitted;
        public event EventHandler<AccountPair> AccountStarted;
        public event EventHandler<AccountPair> AccountCompleted;
        public event EventHandler<AccountPair> AccountFailed;
        public event EventHandler QueueCompleted;

        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private readonly MigrationJournal _journal;
        private readonly List<string> _logHistory;
        private readonly object _logLock;

        public bool IsRunning { get; private set; }

        public bool IsPaused
        {
            get { return !_pauseEvent.IsSet; }
        }

        public AppSettings Settings { get; set; }

        public MigrationEngine(AppSettings settings)
        {
            Settings = settings ?? new AppSettings();
            _journal = MigrationJournal.Instance;
            _logHistory = new List<string>();
            _logLock = new object();
            _pauseEvent = new ManualResetEventSlim(true);
        }

        public MigrationEngine() : this(null) { }

        public void Pause()
        {
            _pauseEvent.Reset();
            Log("Migration paused by user.", "INFO");
        }

        public void Resume()
        {
            _pauseEvent.Set();
            Log("Migration resumed by user.", "INFO");
        }

        public void Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _pauseEvent.Set();
                Log("Stop requested by user. Cleaning up safely...", "WARN");
            }
        }

        public void Log(string message, string level)
        {
            var now = DateTime.Now;
            string formatted = string.Format("[{0:HH:mm:ss}] [{1}] {2}", now, level, message);

            lock (_logLock)
            {
                _logHistory.Add(formatted);
                if (_logHistory.Count > 5000)
                {
                    _logHistory.RemoveRange(0, 1000);
                }
            }

            try
            {
                var handler = LogEmitted;
                if (handler != null)
                {
                    handler(this, new LogEventArgs
                    {
                        Message = message,
                        Level = level,
                        Timestamp = now
                    });
                }
            }
            catch { }
        }

        public void Log(string message)
        {
            Log(message, "INFO");
        }

        public List<string> GetLogHistory()
        {
            lock (_logLock)
            {
                return new List<string>(_logHistory);
            }
        }

        public void RunQueueAsync(List<AccountPair> accounts)
        {
            if (IsRunning) return;
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _pauseEvent.Set();

            Task.Factory.StartNew(() =>
            {
                try
                {
                    int concurrency = (int)Settings.QueueConcurrency;
                    if (concurrency <= 0 || concurrency > accounts.Count)
                    {
                        concurrency = Math.Max(1, accounts.Count);
                    }

                    Log(string.Format("Starting migration queue: {0} account(s), concurrency: {1}", accounts.Count, concurrency), "INFO");

                    using (var semaphore = new SemaphoreSlim(concurrency, concurrency))
                    {
                        var tasks = new List<Task>();

                        foreach (var acc in accounts)
                        {
                            if (_cts.IsCancellationRequested) break;

                            var account = acc;
                            semaphore.Wait(_cts.Token);

                            var t = Task.Factory.StartNew(() =>
                            {
                                try
                                {
                                    if (!_cts.IsCancellationRequested)
                                    {
                                        MigrateSingleAccount(account, _cts.Token);
                                    }
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }, _cts.Token);

                            tasks.Add(t);
                        }

                        Task.WaitAll(tasks.ToArray());
                    }

                    Log("All queued accounts processed.", "INFO");
                }
                catch (OperationCanceledException)
                {
                    Log("Queue execution was stopped.", "WARN");
                }
                catch (Exception ex)
                {
                    Log("Queue error: " + ex.Message, "ERROR");
                }
                finally
                {
                    IsRunning = false;
                    try
                    {
                        var handler = QueueCompleted;
                        if (handler != null) handler(this, EventArgs.Empty);
                    }
                    catch { }
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void MigrateSingleAccount(AccountPair account, CancellationToken token)
        {
            account.Status = AccountSyncStatus.Syncing;
            account.StartTime = DateTime.Now;

            var startHandler = AccountStarted;
            if (startHandler != null) startHandler(this, account);

            string accountKey = string.Format("{0}->{1}", account.SourceUser, account.DestUser);
            Log(string.Format("Beginning sync for {0} -> {1}", account.SourceUser, account.DestUser), "INFO");

            ImapClient srcClient = null;
            ImapClient dstClient = null;

            int overallTotal = account.FolderMappings.Where(f => f.Selected).Sum(f => f.TotalMessages);
            int overallProcessed = 0;
            int overallMoved = 0;
            int overallSkipped = 0;
            int overallErrors = 0;

            var stopwatch = Stopwatch.StartNew();

            Action initClients = () =>
            {
                if (srcClient != null) srcClient.Dispose();
                if (dstClient != null) dstClient.Dispose();

                srcClient = new ImapClient(account.SourceHost, account.SourcePort, account.SourceSecurity, Settings.TimeoutSeconds);
                srcClient.Logger = msg => Log("[Source] " + msg, "DEBUG");
                srcClient.Connect();
                srcClient.Login(account.SourceUser, account.SourcePassword);

                dstClient = new ImapClient(account.DestHost, account.DestPort, account.DestSecurity, Settings.TimeoutSeconds);
                dstClient.Logger = msg => Log("[Dest] " + msg, "DEBUG");
                dstClient.Connect();
                dstClient.Login(account.DestUser, account.DestPassword);
            };

            try
            {
                ExecuteWithRetry("Initial connection to mailboxes", initClients, token);

                foreach (var folderMap in account.FolderMappings.Where(f => f.Selected))
                {
                    if (token.IsCancellationRequested) break;

                    folderMap.Status = "Scanning";
                    Log(string.Format("Processing folder: '{0}' -> '{1}'", folderMap.SourceFolder, folderMap.DestFolder), "INFO");

                    int srcCount = 0;
                    ExecuteWithRetry("Examine source folder " + folderMap.SourceFolder, () =>
                    {
                        srcCount = srcClient.ExamineFolder(folderMap.SourceFolder);
                    }, token);

                    ExecuteWithRetry("Select/create destination folder " + folderMap.DestFolder, () =>
                    {
                        dstClient.SelectFolder(folderMap.DestFolder);
                    }, token);

                    HashSet<string> destExistingMsgIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (Settings.DeduplicateMessageId)
                    {
                        try
                        {
                            destExistingMsgIds = dstClient.FetchExistingMessageIds(folderMap.DestFolder);
                            Log(string.Format("Cached {0} existing Message-IDs on destination folder '{1}'", destExistingMsgIds.Count, folderMap.DestFolder), "INFO");
                        }
                        catch (Exception ex)
                        {
                            Log("Destination Message-ID cache notice: " + ex.Message, "DEBUG");
                        }
                    }

                    List<uint> uids = new List<uint>();
                    ExecuteWithRetry("Search messages in " + folderMap.SourceFolder, () =>
                    {
                        uids = srcClient.SearchUids(Settings.TimePeriod, Settings.CustomStartDate, Settings.CustomEndDate);
                    }, token);

                    folderMap.TotalMessages = uids.Count;
                    folderMap.Status = "Syncing";
                    Log(string.Format("Found {0} message(s) to process in '{1}'", uids.Count, folderMap.SourceFolder), "INFO");

                    int folderProcessed = 0;

                    for (int i = 0; i < uids.Count; i++)
                    {
                        _pauseEvent.Wait(token);
                        if (token.IsCancellationRequested) break;

                        uint uid = uids[i];
                        bool transferSuccess = false;
                        int retryCount = 0;

                        while (!transferSuccess && retryCount < Math.Max(10, Settings.MaxRetries) && !token.IsCancellationRequested)
                        {
                            try
                            {
                                // Ensure clients are connected before processing message
                                if (srcClient == null || !srcClient.IsConnected || dstClient == null || !dstClient.IsConnected)
                                {
                                    initClients();
                                    srcClient.ExamineFolder(folderMap.SourceFolder);
                                    dstClient.SelectFolder(folderMap.DestFolder);
                                }

                                if (_journal.IsAlreadyTransferred(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, null))
                                {
                                    folderMap.Skipped++;
                                    overallSkipped++;
                                    folderProcessed++;
                                    overallProcessed++;
                                    transferSuccess = true;
                                    break;
                                }

                                // Skip emails larger than user-defined size limit in MB
                                if (Settings.SkipLargerThanEnabled && Settings.MaxEmailSizeMB > 0)
                                {
                                    long size = srcClient.FetchMessageSize(uid);
                                    long maxBytes = (long)Settings.MaxEmailSizeMB * 1024 * 1024;
                                    if (size > maxBytes)
                                    {
                                        Log(string.Format("Skipped UID {0}: Size {1:F2} MB exceeds limit of {2} MB",
                                            uid, size / (1024.0 * 1024.0), Settings.MaxEmailSizeMB), "INFO");
                                        folderMap.Skipped++;
                                        overallSkipped++;
                                        folderProcessed++;
                                        overallProcessed++;
                                        transferSuccess = true;
                                        break;
                                    }
                                }

                                FetchedMessage msg = srcClient.FetchMessage(uid);

                                if (msg != null && msg.NotFound)
                                {
                                    Log(string.Format("Notice: Message UID {0} was not found on source (deleted/expunged), skipping.", uid), "INFO");
                                    folderMap.Skipped++;
                                    overallSkipped++;
                                    folderProcessed++;
                                    overallProcessed++;
                                    transferSuccess = true;
                                    break;
                                }

                                if (msg != null && (msg.IsUnavailable || (msg.RawData == null && !string.IsNullOrEmpty(msg.ErrorMessage))))
                                {
                                    Log(string.Format("Notice: Message UID {0} is permanently UNAVAILABLE on source mail server ({1}). Skipping unreadable message to continue queue.",
                                        uid, msg.ErrorMessage ?? "Server returned no data"), "WARN");
                                    folderMap.Skipped++;
                                    overallSkipped++;
                                    folderProcessed++;
                                    overallProcessed++;
                                    _journal.RecordTransfer(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, "UNAVAILABLE-" + uid, 0);
                                    transferSuccess = true;
                                    break;
                                }

                                if (msg == null || msg.RawData == null)
                                {
                                    throw new Exception(string.Format("Failed to fetch message UID {0}", uid));
                                }

                                if (Settings.DeduplicateMessageId && !string.IsNullOrEmpty(msg.MessageId))
                                {
                                    if (destExistingMsgIds.Contains(msg.MessageId) ||
                                        _journal.IsAlreadyTransferred(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, msg.MessageId))
                                    {
                                        folderMap.Skipped++;
                                        overallSkipped++;
                                        folderProcessed++;
                                        overallProcessed++;
                                        _journal.RecordTransfer(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, msg.MessageId, msg.RawData.Length);
                                        transferSuccess = true;
                                        break;
                                    }
                                }

                                dstClient.AppendMessage(folderMap.DestFolder, msg.Flags, msg.InternalDate, msg.RawData);

                                _journal.RecordTransfer(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, msg.MessageId, msg.RawData.Length);
                                if (!string.IsNullOrEmpty(msg.MessageId))
                                {
                                    destExistingMsgIds.Add(msg.MessageId);
                                }

                                folderMap.Moved++;
                                overallMoved++;
                                folderProcessed++;
                                overallProcessed++;
                                transferSuccess = true;
                            }
                            catch (Exception ex)
                            {
                                if (ex.Message.IndexOf("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    ex.Message.IndexOf("NONEXISTENT", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    Log(string.Format("Notice: Message UID {0} reported as UNAVAILABLE by source server ({1}). Skipping unreadable message to continue queue.",
                                        uid, ex.Message), "WARN");
                                    folderMap.Skipped++;
                                    overallSkipped++;
                                    folderProcessed++;
                                    overallProcessed++;
                                    _journal.RecordTransfer(accountKey, folderMap.SourceFolder, folderMap.DestFolder, uid, "UNAVAILABLE-" + uid, 0);
                                    transferSuccess = true;
                                    break;
                                }

                                retryCount++;
                                if (retryCount >= Math.Max(10, Settings.MaxRetries))
                                {
                                    folderMap.Errors++;
                                    overallErrors++;
                                    folderProcessed++;
                                    overallProcessed++;
                                    Log(string.Format("Permanent error transferring UID {0}: {1}", uid, ex.Message), "ERROR");
                                    break;
                                }

                                int backoffSeconds = Math.Min(30, (int)Math.Pow(1.5, retryCount) * Settings.RetryDelaySeconds);
                                Log(string.Format("Connection drop/error on UID {0} ({1}). Retrying in {2}s (Attempt {3}/{4})...",
                                    uid, ex.Message, backoffSeconds, retryCount, Settings.MaxRetries), "WARN");

                                Thread.Sleep(backoffSeconds * 1000);

                                try
                                {
                                    initClients();
                                    srcClient.ExamineFolder(folderMap.SourceFolder);
                                    dstClient.SelectFolder(folderMap.DestFolder);
                                }
                                catch (Exception reconnectEx)
                                {
                                    Log("Reconnect attempt error: " + reconnectEx.Message, "WARN");
                                    try { if (srcClient != null) srcClient.Dispose(); } catch { }
                                    try { if (dstClient != null) dstClient.Dispose(); } catch { }
                                    srcClient = null;
                                    dstClient = null;
                                }
                            }
                        }

                        double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        double speed = elapsedSec > 0 ? (overallProcessed / elapsedSec) : 0;
                        string timeLeft = "Estimating...";
                        if (speed > 0 && overallTotal > overallProcessed)
                        {
                            int secLeft = (int)((overallTotal - overallProcessed) / speed);
                            if (secLeft < 60) timeLeft = "Less than a minute left";
                            else if (secLeft < 3600) timeLeft = string.Format("About {0} min left", secLeft / 60);
                            else timeLeft = string.Format("About {0}h {1}m left", secLeft / 3600, (secLeft % 3600) / 60);
                        }
                        else if (overallProcessed >= overallTotal)
                        {
                            timeLeft = "Complete";
                        }

                        var fpHandler = FolderProgressChanged;
                        if (fpHandler != null)
                        {
                            fpHandler(this, new FolderProgressEventArgs
                            {
                                FolderName = folderMap.SourceFolder,
                                DestFolder = folderMap.DestFolder,
                                Total = folderMap.TotalMessages,
                                Processed = folderProcessed,
                                Moved = folderMap.Moved,
                                Skipped = folderMap.Skipped,
                                Errors = folderMap.Errors,
                                Status = string.Format("Running, {0} of {1}", folderProcessed, folderMap.TotalMessages)
                            });
                        }

                        var opHandler = OverallProgressChanged;
                        if (opHandler != null)
                        {
                            opHandler(this, new OverallProgressEventArgs
                            {
                                TotalMessages = overallTotal,
                                ProcessedMessages = overallProcessed,
                                MovedMessages = overallMoved,
                                SkippedMessages = overallSkipped,
                                ErrorMessages = overallErrors,
                                MessagesPerSecond = speed,
                                TimeRemainingText = timeLeft,
                                CurrentAction = string.Format("{0} -> {1}", folderMap.SourceFolder, folderMap.DestFolder)
                            });
                        }
                    }

                    folderMap.Status = folderMap.Errors > 0 ? "Completed with errors" : "Completed";
                }

                account.Status = AccountSyncStatus.Completed;
                account.StatusMessage = string.Format("Done: {0} moved, {1} skipped, {2} errors", overallMoved, overallSkipped, overallErrors);
                account.MovedMessages = overallMoved;
                account.SkippedMessages = overallSkipped;
                account.ErrorMessages = overallErrors;
                account.ProcessedMessages = overallProcessed;
                account.TotalMessages = overallTotal;
                account.EndTime = DateTime.Now;

                Log(string.Format("Finished sync for {0} -> {1}. Moved: {2}, Skipped: {3}, Errors: {4}",
                    account.SourceUser, account.DestUser, overallMoved, overallSkipped, overallErrors), "INFO");

                var compHandler = AccountCompleted;
                if (compHandler != null) compHandler(this, account);
            }
            catch (OperationCanceledException)
            {
                account.Status = AccountSyncStatus.Stopped;
                account.StatusMessage = "Stopped by user";
                Log("Sync stopped by user for " + accountKey, "WARN");
                var failHandler = AccountFailed;
                if (failHandler != null) failHandler(this, account);
            }
            catch (Exception ex)
            {
                account.Status = AccountSyncStatus.Failed;
                account.StatusMessage = "Failed: " + ex.Message;
                Log(string.Format("Fatal error migrating {0}: {1}", accountKey, ex.Message), "ERROR");
                var failHandler = AccountFailed;
                if (failHandler != null) failHandler(this, account);
            }
            finally
            {
                if (srcClient != null) srcClient.Dispose();
                if (dstClient != null) dstClient.Dispose();
            }
        }

        private void ExecuteWithRetry(string actionName, Action action, CancellationToken token)
        {
            int attempts = 0;
            int maxAttempts = Math.Max(10, Settings.MaxRetries);

            while (attempts < maxAttempts)
            {
                token.ThrowIfCancellationRequested();
                _pauseEvent.Wait(token);

                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    attempts++;
                    if (attempts >= maxAttempts)
                    {
                        throw new Exception(string.Format("{0} failed after {1} attempts: {2}", actionName, maxAttempts, ex.Message));
                    }

                    int delay = Math.Min(30, (int)Math.Pow(1.5, attempts) * Settings.RetryDelaySeconds);
                    Log(string.Format("{0} dropped: {1}. Auto-retrying in {2}s (Attempt {3}/{4})...",
                        actionName, ex.Message, delay, attempts, maxAttempts), "WARN");

                    Thread.Sleep(delay * 1000);
                }
            }
        }
    }
}
