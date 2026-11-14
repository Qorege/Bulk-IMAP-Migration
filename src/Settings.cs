using System;
using System.IO;
using System.Text;

namespace ImapSyncWin
{
    public static class SettingsStorage
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

        public static AppSettings Load()
        {
            var s = new AppSettings();
            if (!File.Exists(SettingsFile))
            {
                return s;
            }

            try
            {
                var lines = File.ReadAllLines(SettingsFile, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "defaultsourcehost":
                            s.DefaultSourceHost = val;
                            break;
                        case "defaultsourceport":
                            int sp;
                            if (int.TryParse(val, out sp)) s.DefaultSourcePort = sp;
                            break;
                        case "defaultsourcesecurity":
                            SecurityType ss;
                            if (Enum.TryParse(val, true, out ss)) s.DefaultSourceSecurity = ss;
                            break;

                        case "defaultdesthost":
                            s.DefaultDestHost = val;
                            break;
                        case "defaultdestport":
                            int dp;
                            if (int.TryParse(val, out dp)) s.DefaultDestPort = dp;
                            break;
                        case "defaultdestsecurity":
                            SecurityType ds;
                            if (Enum.TryParse(val, true, out ds)) s.DefaultDestSecurity = ds;
                            break;

                        case "timeperiod":
                            TimePeriodOption tp;
                            if (Enum.TryParse(val, true, out tp)) s.TimePeriod = tp;
                            break;
                        case "customstartdate":
                            DateTime dtStart;
                            if (DateTime.TryParse(val, out dtStart)) s.CustomStartDate = dtStart;
                            break;
                        case "customenddate":
                            DateTime dtEnd;
                            if (DateTime.TryParse(val, out dtEnd)) s.CustomEndDate = dtEnd;
                            break;

                        case "queueconcurrency":
                            QueueConcurrencyOption qc;
                            if (Enum.TryParse(val, true, out qc)) s.QueueConcurrency = qc;
                            break;
                        case "maxretries":
                            int mr;
                            if (int.TryParse(val, out mr)) s.MaxRetries = mr;
                            break;
                        case "retrydelayseconds":
                            int rd;
                            if (int.TryParse(val, out rd)) s.RetryDelaySeconds = rd;
                            break;
                        case "timeoutseconds":
                            int to;
                            if (int.TryParse(val, out to)) s.TimeoutSeconds = to;
                            break;
                        case "directstreamingonly":
                            bool dsOnly;
                            if (bool.TryParse(val, out dsOnly)) s.DirectStreamingOnly = dsOnly;
                            break;
                        case "deduplicatemessageid":
                            bool dedup;
                            if (bool.TryParse(val, out dedup)) s.DeduplicateMessageId = dedup;
                            break;
                        case "defaultfolderstomigrate":
                            if (!string.IsNullOrWhiteSpace(val)) s.DefaultFoldersToMigrate = val;
                            break;
                        case "skiplargerthanenabled":
                            bool skipLarge;
                            if (bool.TryParse(val, out skipLarge)) s.SkipLargerThanEnabled = skipLarge;
                            break;
                        case "maxemailsizemb":
                            int maxMb;
                            if (int.TryParse(val, out maxMb) && maxMb > 0) s.MaxEmailSizeMB = maxMb;
                            break;
                    }
                }
            }
            catch { }

            return s;
        }

        public static void Save(AppSettings s)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# IMAP Sync Win Settings");
                sb.AppendLine("DefaultSourceHost=" + s.DefaultSourceHost);
                sb.AppendLine("DefaultSourcePort=" + s.DefaultSourcePort);
                sb.AppendLine("DefaultSourceSecurity=" + s.DefaultSourceSecurity);
                sb.AppendLine("DefaultDestHost=" + s.DefaultDestHost);
                sb.AppendLine("DefaultDestPort=" + s.DefaultDestPort);
                sb.AppendLine("DefaultDestSecurity=" + s.DefaultDestSecurity);
                sb.AppendLine("TimePeriod=" + s.TimePeriod);
                sb.AppendLine("CustomStartDate=" + (s.CustomStartDate.HasValue ? s.CustomStartDate.Value.ToString("yyyy-MM-dd") : ""));
                sb.AppendLine("CustomEndDate=" + (s.CustomEndDate.HasValue ? s.CustomEndDate.Value.ToString("yyyy-MM-dd") : ""));
                sb.AppendLine("QueueConcurrency=" + s.QueueConcurrency);
                sb.AppendLine("MaxRetries=" + s.MaxRetries);
                sb.AppendLine("RetryDelaySeconds=" + s.RetryDelaySeconds);
                sb.AppendLine("TimeoutSeconds=" + s.TimeoutSeconds);
                sb.AppendLine("DirectStreamingOnly=" + s.DirectStreamingOnly);
                sb.AppendLine("DeduplicateMessageId=" + s.DeduplicateMessageId);
                sb.AppendLine("DefaultFoldersToMigrate=" + s.DefaultFoldersToMigrate);
                sb.AppendLine("SkipLargerThanEnabled=" + s.SkipLargerThanEnabled);
                sb.AppendLine("MaxEmailSizeMB=" + s.MaxEmailSizeMB);

                File.WriteAllText(SettingsFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}
