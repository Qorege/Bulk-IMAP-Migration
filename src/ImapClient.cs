using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ImapSyncWin
{
    public class FetchedMessage
    {
        public uint Uid { get; set; }
        public string Flags { get; set; }
        public string InternalDate { get; set; }
        public byte[] RawData { get; set; }
        public string MessageId { get; set; }
        public long Size { get; set; }
        public bool IsSizeExceeded { get; set; }
        public bool NotFound { get; set; }
        public bool IsUnavailable { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool ServerResponds { get; set; }
        public bool SslSuccess { get; set; }
        public bool SignInSuccess { get; set; }
        public bool FoldersSuccess { get; set; }
        public int FolderCount { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ImapClient : IDisposable
    {
        private TcpClient _tcp;
        private ImapStream _stream;
        private int _tagCounter;
        private int _timeoutMs;

        public string Host { get; private set; }
        public int Port { get; private set; }
        public SecurityType Security { get; private set; }

        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_tcp == null || !_tcp.Connected || _stream == null) return false;
                    if (_tcp.Client != null && _tcp.Client.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] b = new byte[1];
                        if (_tcp.Client.Receive(b, SocketFlags.Peek) == 0)
                        {
                            return false;
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Action<string> Logger { get; set; }

        public ImapClient(string host, int port, SecurityType security, int timeoutSeconds = 60)
        {
            Host = host;
            Port = port;
            Security = security;
            _timeoutMs = Math.Max(30, timeoutSeconds) * 1000;
        }

        private void Log(string msg)
        {
            try
            {
                var handler = Logger;
                if (handler != null) handler(msg);
            }
            catch { }
        }

        public void Connect()
        {
            Disconnect();
            _tcp = new TcpClient();
            _tcp.ReceiveTimeout = _timeoutMs;
            _tcp.SendTimeout = _timeoutMs;

            Log(string.Format("Connecting to {0}:{1} ({2})...", Host, Port, Security));
            _tcp.Connect(Host, Port);

            if (Security == SecurityType.SslTls)
            {
                var sslStream = new SslStream(_tcp.GetStream(), false, ValidateCertificate);
                // Support TLS 1.0, 1.1, 1.2, and 1.3 ((SslProtocols)12288)
                sslStream.AuthenticateAsClient(Host, null,
                    (SslProtocols)12288 | SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                _stream = new ImapStream(sslStream);
            }
            else
            {
                _stream = new ImapStream(_tcp.GetStream());
            }

            // Read greeting banner
            string greeting = ReadLine();
            if (string.IsNullOrEmpty(greeting) || (!greeting.StartsWith("* OK") && !greeting.StartsWith("* PREAUTH")))
            {
                throw new Exception("Unexpected IMAP greeting: " + greeting);
            }

            // Handle STARTTLS if configured
            if (Security == SecurityType.StartTls)
            {
                string tag = NextTag();
                SendCommand(tag + " STARTTLS");
                string resp = ReadUntilTag(tag);
                if (!resp.Contains(tag + " OK"))
                {
                    throw new Exception("STARTTLS negotiation failed: " + resp);
                }

                var sslStream = new SslStream(_tcp.GetStream(), false, ValidateCertificate);
                sslStream.AuthenticateAsClient(Host, null,
                    (SslProtocols)12288 | SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                _stream = new ImapStream(sslStream);
            }
        }

        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Allow self-signed certificates or domain name mismatches commonly seen on cPanel/private mail servers
            return true;
        }

        public void Login(string username, string password)
        {
            string tag = NextTag();
            string escapedUser = EscapeString(username);
            string escapedPass = EscapeString(password);

            SendCommand(string.Format("{0} LOGIN {1} {2}", tag, escapedUser, escapedPass));
            string response = ReadUntilTag(tag);

            if (!response.Contains(tag + " OK"))
            {
                throw new Exception("IMAP Login failed: " + response.Trim());
            }
        }

        public ConnectionTestResult TestConnection(string username, string password)
        {
            var res = new ConnectionTestResult();
            try
            {
                Connect();
                res.ServerResponds = true;
                res.SslSuccess = (Security != SecurityType.Plain);

                Login(username, password);
                res.SignInSuccess = true;

                var folders = ListFolders();
                res.FoldersSuccess = true;
                res.FolderCount = folders.Count;
            }
            catch (Exception ex)
            {
                res.ErrorMessage = ex.Message;
            }
            finally
            {
                Disconnect();
            }
            return res;
        }

        public List<ImapFolder> ListFolders()
        {
            string tag = NextTag();
            SendCommand(tag + " LIST \"\" \"*\"");

            var list = new List<ImapFolder>();
            string line;
            while ((line = ReadLine()) != null)
            {
                if (line.StartsWith(tag + " "))
                {
                    if (!line.Contains("OK"))
                    {
                        throw new Exception("LIST failed: " + line);
                    }
                    break;
                }

                if (line.StartsWith("*") && line.IndexOf("LIST", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string folderLiteral = null;
                    var litMatch = Regex.Match(line, @"\{(\d+)\+?\}\s*$");
                    if (litMatch.Success)
                    {
                        int litLen = int.Parse(litMatch.Groups[1].Value);
                        byte[] rawLit = _stream.ReadExactBytes(litLen);
                        folderLiteral = Encoding.UTF8.GetString(rawLit);
                    }

                    var folder = ParseListLine(line, folderLiteral);
                    if (folder != null)
                    {
                        list.Add(folder);
                    }
                }
            }

            return list;
        }

        private ImapFolder ParseListLine(string line, string folderLiteral = null)
        {
            try
            {
                int firstParen = line.IndexOf('(');
                int lastParen = line.IndexOf(')', firstParen + 1);
                if (firstParen < 0 || lastParen < 0) return null;

                string flagsStr = line.Substring(firstParen + 1, lastParen - firstParen - 1);
                string remainder = line.Substring(lastParen + 1).Trim();

                string delimiter = "/";
                string folderName = "";

                if (remainder.StartsWith("\""))
                {
                    int nextQuote = remainder.IndexOf('\"', 1);
                    if (nextQuote > 1)
                    {
                        delimiter = remainder.Substring(1, nextQuote - 1);
                        remainder = remainder.Substring(nextQuote + 1).Trim();
                    }
                }
                else
                {
                    int spaceIdx = remainder.IndexOf(' ');
                    if (spaceIdx > 0)
                    {
                        delimiter = remainder.Substring(0, spaceIdx);
                        remainder = remainder.Substring(spaceIdx + 1).Trim();
                    }
                }

                if (!string.IsNullOrEmpty(folderLiteral))
                {
                    folderName = folderLiteral;
                }
                else if (remainder.StartsWith("\"") && remainder.EndsWith("\"") && remainder.Length >= 2)
                {
                    folderName = remainder.Substring(1, remainder.Length - 2);
                }
                else
                {
                    folderName = remainder;
                }

                var folder = new ImapFolder
                {
                    Name = folderName,
                    RawName = folderName,
                    Delimiter = delimiter,
                    IsSelectable = !flagsStr.ToLower().Contains("\\noselect")
                };

                foreach (var f in flagsStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    folder.Attributes.Add(f);
                }

                return folder;
            }
            catch
            {
                return null;
            }
        }

        public int ExamineFolder(string folderName)
        {
            string tag = NextTag();
            SendCommand(string.Format("{0} EXAMINE {1}", tag, EscapeString(folderName)));

            int exists = 0;
            string line;
            while ((line = ReadLine()) != null)
            {
                if (line.StartsWith(tag + " "))
                {
                    if (!line.Contains("OK"))
                    {
                        throw new Exception("EXAMINE failed for " + folderName + ": " + line);
                    }
                    break;
                }

                if (line.StartsWith("*") && line.Contains("EXISTS"))
                {
                    var match = Regex.Match(line, @"\*\s+(\d+)\s+EXISTS");
                    if (match.Success)
                    {
                        int.TryParse(match.Groups[1].Value, out exists);
                    }
                }
            }

            return exists;
        }

        public int SelectFolder(string folderName, bool createIfMissing = true)
        {
            string tag = NextTag();
            SendCommand(string.Format("{0} SELECT {1}", tag, EscapeString(folderName)));

            int exists = 0;
            string line;
            bool ok = false;
            string lastStatusLine = "";

            while ((line = ReadLine()) != null)
            {
                if (line.StartsWith(tag + " "))
                {
                    ok = line.Contains("OK");
                    lastStatusLine = line;
                    break;
                }

                if (line.StartsWith("*") && line.Contains("EXISTS"))
                {
                    var match = Regex.Match(line, @"\*\s+(\d+)\s+EXISTS");
                    if (match.Success)
                    {
                        int.TryParse(match.Groups[1].Value, out exists);
                    }
                }
            }

            if (!ok)
            {
                if (createIfMissing)
                {
                    CreateFolder(folderName);
                    return SelectFolder(folderName, false);
                }
                throw new Exception(string.Format("Failed to select folder '{0}': {1}", folderName, lastStatusLine));
            }

            return exists;
        }

        public void CreateFolder(string folderName)
        {
            string tag = NextTag();
            SendCommand(string.Format("{0} CREATE {1}", tag, EscapeString(folderName)));
            string resp = ReadUntilTag(tag);
            if (!resp.Contains(tag + " OK") && !resp.Contains("ALREADYEXISTS"))
            {
                Log("Notice: CREATE folder '" + folderName + "' response: " + resp.Trim());
            }
        }

        public List<uint> SearchUids(TimePeriodOption period, DateTime? customStart, DateTime? customEnd)
        {
            string searchCriteria = "ALL";
            DateTime now = DateTime.Now;

            switch (period)
            {
                case TimePeriodOption.Last6Months:
                    DateTime sixMonthsAgo = now.AddMonths(-6);
                    searchCriteria = "SINCE " + FormatImapDate(sixMonthsAgo);
                    break;

                case TimePeriodOption.LastYear:
                    DateTime oneYearAgo = now.AddYears(-1);
                    searchCriteria = "SINCE " + FormatImapDate(oneYearAgo);
                    break;

                case TimePeriodOption.CustomDates:
                    if (customStart.HasValue && customEnd.HasValue)
                    {
                        searchCriteria = string.Format("SINCE {0} BEFORE {1}",
                            FormatImapDate(customStart.Value),
                            FormatImapDate(customEnd.Value.AddDays(1)));
                    }
                    else if (customStart.HasValue)
                    {
                        searchCriteria = "SINCE " + FormatImapDate(customStart.Value);
                    }
                    else if (customEnd.HasValue)
                    {
                        searchCriteria = "BEFORE " + FormatImapDate(customEnd.Value.AddDays(1));
                    }
                    break;

                case TimePeriodOption.AllMessages:
                default:
                    searchCriteria = "ALL";
                    break;
            }

            string tag = NextTag();
            SendCommand(string.Format("{0} UID SEARCH {1}", tag, searchCriteria));

            var uids = new List<uint>();
            string line;
            while ((line = ReadLine()) != null)
            {
                if (line.StartsWith(tag + " "))
                {
                    if (!line.Contains("OK"))
                    {
                        throw new Exception("UID SEARCH failed: " + line);
                    }
                    break;
                }

                if (line.StartsWith("* SEARCH"))
                {
                    string nums = line.Substring("* SEARCH".Length).Trim();
                    if (!string.IsNullOrEmpty(nums))
                    {
                        var parts = nums.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            uint val;
                            if (uint.TryParse(p, out val))
                            {
                                uids.Add(val);
                            }
                        }
                    }
                }
            }

            return uids;
        }

        public HashSet<string> FetchExistingMessageIds(string folderName)
        {
            var msgIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                int count = SelectFolder(folderName, true);
                if (count <= 0)
                {
                    return msgIds;
                }

                string tag = NextTag();
                SendCommand(string.Format("{0} FETCH 1:* (BODY.PEEK[HEADER.FIELDS (MESSAGE-ID)])", tag));

                while (true)
                {
                    string line = ReadLine();
                    if (line == null || line.StartsWith(tag + " ")) break;

                    if (line.StartsWith("*") && line.IndexOf("FETCH", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var litMatch = Regex.Match(line, @"\{(\d+)\+?\}\s*$");
                        if (litMatch.Success)
                        {
                            int litLen = int.Parse(litMatch.Groups[1].Value);
                            byte[] hdrBytes = _stream.ReadExactBytes(litLen);
                            string hdrText = Encoding.ASCII.GetString(hdrBytes);
                            var idMatch = Regex.Match(hdrText, @"(?im)^Message-ID:\s*<([^>]+)>");
                            if (!idMatch.Success)
                            {
                                idMatch = Regex.Match(hdrText, @"(?im)^Message-ID:\s*([^\r\n]+)");
                            }
                            if (idMatch.Success)
                            {
                                string id = idMatch.Groups[1].Value.Trim().Trim('<', '>', ' ', '\t');
                                if (!string.IsNullOrEmpty(id))
                                {
                                    msgIds.Add(id);
                                }
                            }
                            ReadLine(); // Read remainder (e.g. ")")
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Notice: Message-ID pre-scan skipped or not supported: " + ex.Message);
            }
            return msgIds;
        }

        public long FetchMessageSize(uint uid)
        {
            string tag = NextTag();
            SendCommand(string.Format("{0} UID FETCH {1} (RFC822.SIZE)", tag, uid));

            long size = 0;
            while (true)
            {
                string line = ReadLine();
                if (line == null || line.StartsWith(tag + " ")) break;

                if (line.StartsWith("*") && line.IndexOf("FETCH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var match = Regex.Match(line, @"RFC822\.SIZE\s+(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        long.TryParse(match.Groups[1].Value, out size);
                    }
                }
            }
            return size;
        }

        public FetchedMessage FetchMessage(uint uid)
        {
            // List of progressive fetch queries to try before giving up
            string[] queries = new[]
            {
                "(FLAGS INTERNALDATE BODY.PEEK[])",
                "(FLAGS INTERNALDATE RFC822.PEEK)",
                "(FLAGS INTERNALDATE RFC822)",
                "(FLAGS INTERNALDATE BODY[])",
                "(BODY.PEEK[])",
                "(RFC822)"
            };

            FetchedMessage lastResult = null;

            for (int i = 0; i < queries.Length; i++)
            {
                string query = queries[i];
                lastResult = TryFetchWithArgs(uid, query);

                // If message body was retrieved or message was expunged, return immediately
                if (lastResult.RawData != null || lastResult.NotFound)
                {
                    return lastResult;
                }

                // If error is NOT a server-side availability issue (e.g. connection dropped), stop trying queries
                if (!lastResult.IsUnavailable && string.IsNullOrEmpty(lastResult.ErrorMessage))
                {
                    break;
                }
            }

            return lastResult;
        }

        private FetchedMessage TryFetchWithArgs(uint uid, string fetchArgs)
        {
            string tag = NextTag();
            SendCommand(string.Format("{0} UID FETCH {1} {2}", tag, uid, fetchArgs));

            var result = new FetchedMessage { Uid = uid };
            bool foundMessage = false;

            while (true)
            {
                string line = ReadLine();
                if (line == null)
                {
                    throw new EndOfStreamException(string.Format("Connection closed by server while fetching UID {0}", uid));
                }

                if (line.StartsWith(tag + " "))
                {
                    if (!line.Contains("OK"))
                    {
                        result.ErrorMessage = line;
                        if (line.IndexOf("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf("NONEXISTENT", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.IsUnavailable = true;
                        }
                    }
                    break;
                }

                if (line.StartsWith("*") && line.IndexOf("FETCH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foundMessage = true;
                    ParseFetchAttributes(line, result);

                    var litMatch = Regex.Match(line, @"\{(\d+)\+?\}\s*$");
                    if (litMatch.Success)
                    {
                        int byteLength = int.Parse(litMatch.Groups[1].Value);
                        result.RawData = _stream.ReadExactBytes(byteLength);
                        result.Size = byteLength;

                        // After reading literal bytes, read the remainder of the FETCH line (e.g. ")\r\n" or " FLAGS (...) UID ...)\r\n")
                        string remainder = ReadLine();
                        if (!string.IsNullOrEmpty(remainder))
                        {
                            ParseFetchAttributes(remainder, result);
                        }
                    }
                }
            }

            if (!foundMessage && string.IsNullOrEmpty(result.ErrorMessage))
            {
                result.NotFound = true;
                return result;
            }

            if (result.RawData != null)
            {
                result.MessageId = ExtractMessageId(result.RawData);
            }

            return result;
        }

        private void ParseFetchAttributes(string text, FetchedMessage msg)
        {
            if (string.IsNullOrEmpty(text) || msg == null) return;

            var flagsMatch = Regex.Match(text, @"FLAGS\s*\(([^\)]*)\)", RegexOptions.IgnoreCase);
            if (flagsMatch.Success && string.IsNullOrEmpty(msg.Flags))
            {
                msg.Flags = flagsMatch.Groups[1].Value.Trim();
            }

            var dateMatch = Regex.Match(text, @"INTERNALDATE\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (dateMatch.Success && string.IsNullOrEmpty(msg.InternalDate))
            {
                msg.InternalDate = dateMatch.Groups[1].Value;
            }
        }

        public static string CleanFlagsForAppend(string flags)
        {
            if (string.IsNullOrEmpty(flags)) return null;

            var validFlags = new List<string>();
            var parts = flags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                string trimmed = p.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                // RFC 3501 Section 6.3.11 explicitly forbids \Recent in APPEND
                if (trimmed.Equals(@"\Recent", StringComparison.OrdinalIgnoreCase)) continue;
                validFlags.Add(trimmed);
            }
            return validFlags.Count > 0 ? string.Join(" ", validFlags.ToArray()) : null;
        }

        public void AppendMessage(string folderName, string flags, string internalDate, byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");

            string tag = NextTag();
            string cleanFlags = CleanFlagsForAppend(flags);
            string cleanDate = null;
            if (!string.IsNullOrEmpty(internalDate))
            {
                cleanDate = internalDate.Trim('\"', ' ');
            }

            var sb = new StringBuilder();
            sb.Append(tag);
            sb.Append(" APPEND ");
            sb.Append(EscapeString(folderName));

            if (!string.IsNullOrEmpty(cleanFlags))
            {
                sb.Append(" (");
                sb.Append(cleanFlags);
                sb.Append(")");
            }

            if (!string.IsNullOrEmpty(cleanDate))
            {
                if (string.IsNullOrEmpty(cleanFlags))
                {
                    sb.Append(" ()");
                }
                sb.Append(" \"");
                sb.Append(cleanDate);
                sb.Append("\"");
            }

            sb.Append(string.Format(" {{{0}}}", data.Length));

            _stream.WriteLine(sb.ToString());

            string prompt = _stream.ReadLine();
            while (prompt != null && !prompt.StartsWith("+") && !prompt.StartsWith(tag + " "))
            {
                prompt = _stream.ReadLine();
            }

            if (string.IsNullOrEmpty(prompt) || !prompt.StartsWith("+"))
            {
                throw new Exception(string.Format("Destination server rejected APPEND to '{0}': {1}",
                    folderName, prompt ?? "(no response)"));
            }

            _stream.Write(data, 0, data.Length);
            _stream.WriteLine(""); // CRLF to finish the command

            string response = ReadUntilTag(tag);
            if (!response.Contains(tag + " OK"))
            {
                throw new Exception(string.Format("APPEND failed on destination folder '{0}': {1}",
                    folderName, response.Trim()));
            }
        }

        private string ExtractMessageId(byte[] data)
        {
            try
            {
                int headerLen = Math.Min(data.Length, 8192);
                string text = Encoding.ASCII.GetString(data, 0, headerLen);
                var match = Regex.Match(text, @"(?im)^Message-ID:\s*<([^>]+)>");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                match = Regex.Match(text, @"(?im)^Message-ID:\s*([^\r\n]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim().Trim('<', '>');
                }
            }
            catch { }
            return null;
        }

        private string FormatImapDate(DateTime dt)
        {
            return dt.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        private string NextTag()
        {
            Interlocked.Increment(ref _tagCounter);
            return string.Format("a{0:D4}", _tagCounter);
        }

        private void SendCommand(string command)
        {
            if (_stream == null) throw new ObjectDisposedException("ImapClient");
            _stream.WriteLine(command);
        }

        private string ReadLine()
        {
            if (_stream == null) throw new ObjectDisposedException("ImapClient");
            return _stream.ReadLine();
        }

        private string ReadUntilTag(string tag)
        {
            var sb = new StringBuilder();
            string line;
            while ((line = ReadLine()) != null)
            {
                sb.AppendLine(line);
                if (line.StartsWith(tag + " "))
                {
                    break;
                }
            }
            return sb.ToString();
        }

        private string EscapeString(string str)
        {
            if (str == null) return "\"\"";
            return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public void Disconnect()
        {
            try
            {
                if (_tcp != null && _tcp.Connected && _stream != null)
                {
                    try
                    {
                        string tag = NextTag();
                        SendCommand(tag + " LOGOUT");
                    }
                    catch { }
                }

                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }

                if (_tcp != null)
                {
                    _tcp.Close();
                    _tcp = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
