using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImapSyncWin
{
    public static class CsvHandler
    {
        public static List<AccountPair> ImportCsv(string filePath, AppSettings defaultSettings)
        {
            var accounts = new List<AccountPair>();
            if (!File.Exists(filePath)) return accounts;

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return accounts;

            string headerLine = lines[0];
            var headers = ParseCsvRow(headerLine);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = ParseCsvRow(line);
                if (cols.Count == 0) continue;

                var acc = new AccountPair();
                acc.SourceHost = defaultSettings.DefaultSourceHost;
                acc.SourcePort = defaultSettings.DefaultSourcePort;
                acc.SourceSecurity = defaultSettings.DefaultSourceSecurity;
                acc.DestHost = defaultSettings.DefaultDestHost;
                acc.DestPort = defaultSettings.DefaultDestPort;
                acc.DestSecurity = defaultSettings.DefaultDestSecurity;

                // Source User / Email
                acc.SourceUser = GetColValue(cols, headerMap, "SourceUser", "SourceEmail", "source_email", "Source_User", "From", "FromEmail", "User1");
                acc.SourcePassword = GetColValue(cols, headerMap, "SourcePassword", "SourcePass", "source_pass", "FromPassword", "Pass1");

                // Dest User / Email
                acc.DestUser = GetColValue(cols, headerMap, "DestUser", "DestEmail", "dest_email", "Dest_User", "To", "ToEmail", "User2");
                acc.DestPassword = GetColValue(cols, headerMap, "DestPassword", "DestPass", "dest_pass", "ToPassword", "Pass2");

                // Optional custom hosts per line
                string sh = GetColValue(cols, headerMap, "SourceHost", "source_host", "FromHost", "SrcHost");
                if (!string.IsNullOrWhiteSpace(sh)) acc.SourceHost = sh.Trim();

                string sport = GetColValue(cols, headerMap, "SourcePort", "source_port", "FromPort", "SrcPort");
                int sp;
                if (int.TryParse(sport, out sp) && sp > 0) acc.SourcePort = sp;

                string ssec = GetColValue(cols, headerMap, "SourceSecurity", "SourceSSL", "source_ssl", "SrcSSL");
                SecurityType st;
                if (!string.IsNullOrWhiteSpace(ssec) && Enum.TryParse(ssec.Trim(), true, out st)) acc.SourceSecurity = st;

                string dh = GetColValue(cols, headerMap, "DestHost", "dest_host", "ToHost", "DstHost");
                if (!string.IsNullOrWhiteSpace(dh)) acc.DestHost = dh.Trim();

                string dport = GetColValue(cols, headerMap, "DestPort", "dest_port", "ToPort", "DstPort");
                int dp;
                if (int.TryParse(dport, out dp) && dp > 0) acc.DestPort = dp;

                string dsec = GetColValue(cols, headerMap, "DestSecurity", "DestSSL", "dest_ssl", "DstSSL");
                SecurityType dt;
                if (!string.IsNullOrWhiteSpace(dsec) && Enum.TryParse(dsec.Trim(), true, out dt)) acc.DestSecurity = dt;

                // Fallback for missing dest credentials
                if (string.IsNullOrEmpty(acc.DestUser) && !string.IsNullOrEmpty(acc.SourceUser))
                {
                    acc.DestUser = acc.SourceUser;
                }
                if (string.IsNullOrEmpty(acc.DestPassword) && !string.IsNullOrEmpty(acc.SourcePassword))
                {
                    acc.DestPassword = acc.SourcePassword;
                }

                // Folder list if specified (e.g. "INBOX;Sent;Archive"), otherwise default from settings
                string folderCol = GetColValue(cols, headerMap, "Folders", "folders", "FolderList", "Folder");
                if (string.IsNullOrWhiteSpace(folderCol))
                {
                    folderCol = defaultSettings.DefaultFoldersToMigrate;
                }

                if (!string.IsNullOrWhiteSpace(folderCol))
                {
                    var fParts = folderCol.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var f in fParts)
                    {
                        string trimF = f.Trim();
                        if (!string.IsNullOrEmpty(trimF))
                        {
                            acc.FolderMappings.Add(new FolderMapping
                            {
                                SourceFolder = trimF,
                                DestFolder = trimF,
                                Selected = true
                            });
                        }
                    }
                }

                if (acc.FolderMappings.Count == 0)
                {
                    acc.FolderMappings.Add(new FolderMapping
                    {
                        SourceFolder = "INBOX",
                        DestFolder = "INBOX",
                        Selected = true
                    });
                }

                if (!string.IsNullOrEmpty(acc.SourceUser) && !string.IsNullOrEmpty(acc.DestUser))
                {
                    accounts.Add(acc);
                }
            }

            return accounts;
        }

        public static void CreateSampleCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SourceHost,SourcePort,SourceSSL,SourceUser,SourcePassword,DestHost,DestPort,DestSSL,DestUser,DestPassword,Folders");
            sb.AppendLine("imap.old-server.com,993,SslTls,alice@old-domain.com,SecretPass1,imap.new-server.com,993,SslTls,alice@new-domain.com,NewSecretPass1,INBOX;Sent;Drafts;Archive");
            sb.AppendLine("imap.old-server.com,993,SslTls,bob@old-domain.com,SecretPass2,imap.new-server.com,993,SslTls,bob@new-domain.com,NewSecretPass2,INBOX;Sent;Spam");
            sb.AppendLine("imap.gmail.com,993,SslTls,charlie@gmail.com,abcd efgh ijkl mnop,outlook.office365.com,993,SslTls,charlie@company.com,WorkPass999,INBOX");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public static void ExportQueueToCsv(string filePath, List<AccountPair> accounts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SourceHost,SourcePort,SourceSSL,SourceUser,SourcePassword,DestHost,DestPort,DestSSL,DestUser,DestPassword,Status,Moved,Skipped,Errors");
            foreach (var acc in accounts)
            {
                sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                    CsvEscape(acc.SourceHost),
                    acc.SourcePort,
                    acc.SourceSecurity,
                    CsvEscape(acc.SourceUser),
                    CsvEscape(acc.SourcePassword),
                    CsvEscape(acc.DestHost),
                    acc.DestPort,
                    acc.DestSecurity,
                    CsvEscape(acc.DestUser),
                    CsvEscape(acc.DestPassword),
                    CsvEscape(acc.Status.ToString()),
                    acc.MovedMessages,
                    acc.SkippedMessages,
                    acc.ErrorMessages));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string GetColValue(List<string> cols, Dictionary<string, int> headerMap, params string[] names)
        {
            foreach (var n in names)
            {
                int idx;
                if (headerMap.TryGetValue(n, out idx) && idx < cols.Count)
                {
                    return cols[idx].Trim();
                }
            }
            return "";
        }

        private static List<string> ParseCsvRow(string line)
        {
            var result = new List<string>();
            var cur = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        cur.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            result.Add(cur.ToString());
            return result;
        }

        private static string CsvEscape(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            if (str.Contains(",") || str.Contains("\"") || str.Contains("\n") || str.Contains("\r"))
            {
                return "\"" + str.Replace("\"", "\"\"") + "\"";
            }
            return str;
        }
    }
}
