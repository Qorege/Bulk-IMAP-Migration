using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ImapSyncWin
{
    public class JournalEntry
    {
        public string AccountKey { get; set; }
        public string SourceFolder { get; set; }
        public string DestFolder { get; set; }
        public uint SourceUid { get; set; }
        public string MessageId { get; set; }
        public long Size { get; set; }
        public DateTime TransferredAt { get; set; }
    }

    public class MigrationJournal
    {
        private static readonly object _syncLock = new object();
        private static MigrationJournal _instance;
        public static MigrationJournal Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MigrationJournal();
                            _instance.Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly string _filePath;
        private readonly HashSet<string> _uidKeys;
        private readonly HashSet<string> _msgIdKeys;
        private readonly List<JournalEntry> _entries;

        public MigrationJournal(string customPath)
        {
            _uidKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _msgIdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _entries = new List<JournalEntry>();
            _filePath = customPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migration_journal.tsv");
        }

        public MigrationJournal() : this(null) { }

        public int TotalRecorded
        {
            get { return _entries.Count; }
        }

        public void Load()
        {
            lock (_syncLock)
            {
                _uidKeys.Clear();
                _msgIdKeys.Clear();
                _entries.Clear();

                if (!File.Exists(_filePath)) return;

                try
                {
                    using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                            var parts = line.Split('\t');
                            if (parts.Length >= 5)
                            {
                                string account = parts[0];
                                string srcFolder = parts[1];
                                string dstFolder = parts[2];
                                uint uid = 0;
                                uint.TryParse(parts[3], out uid);
                                string msgId = parts[4];
                                long size = parts.Length > 5 ? long.Parse(parts[5]) : 0;
                                DateTime time = parts.Length > 6 ? DateTime.Parse(parts[6]) : DateTime.UtcNow;

                                var entry = new JournalEntry
                                {
                                    AccountKey = account,
                                    SourceFolder = srcFolder,
                                    DestFolder = dstFolder,
                                    SourceUid = uid,
                                    MessageId = msgId,
                                    Size = size,
                                    TransferredAt = time
                                };

                                _entries.Add(entry);

                                if (uid > 0)
                                {
                                    _uidKeys.Add(MakeUidKey(account, srcFolder, dstFolder, uid));
                                }

                                if (!string.IsNullOrEmpty(msgId))
                                {
                                    _msgIdKeys.Add(MakeMsgIdKey(account, dstFolder, msgId));
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public bool IsAlreadyTransferred(string accountKey, string srcFolder, string dstFolder, uint uid, string messageId)
        {
            lock (_syncLock)
            {
                if (uid > 0 && _uidKeys.Contains(MakeUidKey(accountKey, srcFolder, dstFolder, uid)))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(messageId) && _msgIdKeys.Contains(MakeMsgIdKey(accountKey, dstFolder, messageId)))
                {
                    return true;
                }

                return false;
            }
        }

        public void RecordTransfer(string accountKey, string srcFolder, string dstFolder, uint uid, string messageId, long size)
        {
            lock (_syncLock)
            {
                var entry = new JournalEntry
                {
                    AccountKey = accountKey,
                    SourceFolder = srcFolder,
                    DestFolder = dstFolder,
                    SourceUid = uid,
                    MessageId = messageId ?? "",
                    Size = size,
                    TransferredAt = DateTime.UtcNow
                };

                _entries.Add(entry);

                if (uid > 0)
                {
                    _uidKeys.Add(MakeUidKey(accountKey, srcFolder, dstFolder, uid));
                }

                if (!string.IsNullOrEmpty(messageId))
                {
                    _msgIdKeys.Add(MakeMsgIdKey(accountKey, dstFolder, messageId));
                }

                try
                {
                    string line = string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6:o}\r\n",
                        EscapeTsv(accountKey),
                        EscapeTsv(srcFolder),
                        EscapeTsv(dstFolder),
                        uid,
                        EscapeTsv(messageId ?? ""),
                        size,
                        entry.TransferredAt);

                    File.AppendAllText(_filePath, line, Encoding.UTF8);
                }
                catch { }
            }
        }

        public void Clear()
        {
            lock (_syncLock)
            {
                _entries.Clear();
                _uidKeys.Clear();
                _msgIdKeys.Clear();
                try
                {
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                }
                catch { }
            }
        }

        private string MakeUidKey(string account, string srcFolder, string dstFolder, uint uid)
        {
            return string.Format("{0}|{1}|{2}|{3}", account, srcFolder, dstFolder, uid);
        }

        private string MakeMsgIdKey(string account, string dstFolder, string msgId)
        {
            return string.Format("{0}|{1}|{2}", account, dstFolder, msgId.Trim().ToLowerInvariant());
        }

        private string EscapeTsv(string s)
        {
            if (s == null) return "";
            return s.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
