using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ImapSyncWin
{
    public class MockMessage
    {
        public uint Uid { get; set; }
        public string Flags { get; set; }
        public string InternalDate { get; set; }
        public string MessageId { get; set; }
        public byte[] RawBytes { get; set; }
        public bool SimulateUnavailable { get; set; }
    }

    public class MockImapServer : IDisposable
    {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _running;
        public int Port { get; private set; }
        public Dictionary<string, List<MockMessage>> Mailboxes { get; private set; }

        public MockImapServer(int port = 0)
        {
            Mailboxes = new Dictionary<string, List<MockMessage>>(StringComparer.OrdinalIgnoreCase);
            Mailboxes["INBOX"] = new List<MockMessage>();
            Mailboxes["Spam"] = new List<MockMessage>();

            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _running = true;
            _listenThread = new Thread(ListenLoop);
            _listenThread.IsBackground = true;
            _listenThread.Start();
        }

        public void PopulateSampleMessages(int inboxCount = 20, int spamCount = 5)
        {
            uint uidCounter = 100;
            for (int i = 1; i <= inboxCount; i++)
            {
                string msgId = string.Format("msg-{0:D4}-sample@company.org", i);
                string dateStr = DateTime.Now.AddDays(-i).ToString("dd-MMM-yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture);
                string rfc822 = string.Format(
                    "From: sender{0}@source-mailbox.com\r\n" +
                    "To: user@source-mailbox.com\r\n" +
                    "Subject: Sample Test Email #{0}\r\n" +
                    "Date: {1}\r\n" +
                    "Message-ID: <{2}>\r\n" +
                    "MIME-Version: 1.0\r\n" +
                    "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
                    "Hello,\r\n\r\nThis is sample email content #{0} transferred via direct IMAP streaming.\r\nPreserving flags, headers, and internal dates.\r\n",
                    i, DateTime.UtcNow.AddDays(-i).ToString("R"), msgId);

                Mailboxes["INBOX"].Add(new MockMessage
                {
                    Uid = uidCounter++,
                    Flags = (i % 2 == 0) ? @"\Seen" : @"\Seen \Flagged",
                    InternalDate = dateStr,
                    MessageId = msgId,
                    RawBytes = Encoding.UTF8.GetBytes(rfc822)
                });
            }

            for (int i = 1; i <= spamCount; i++)
            {
                string msgId = string.Format("spam-{0:D4}-sample@spam.org", i);
                string dateStr = DateTime.Now.AddDays(-i * 2).ToString("dd-MMM-yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture);
                string rfc822 = string.Format(
                    "From: promo{0}@spamdomain.net\r\n" +
                    "To: user@source-mailbox.com\r\n" +
                    "Subject: Special Promo Offer #{0}\r\n" +
                    "Date: {1}\r\n" +
                    "Message-ID: <{2}>\r\n\r\n" +
                    "Promotional spam content #{0}\r\n",
                    i, DateTime.UtcNow.AddDays(-i * 2).ToString("R"), msgId);

                Mailboxes["Spam"].Add(new MockMessage
                {
                    Uid = uidCounter++,
                    Flags = "",
                    InternalDate = dateStr,
                    MessageId = msgId,
                    RawBytes = Encoding.UTF8.GetBytes(rfc822)
                });
            }
        }

        private void ListenLoop()
        {
            try
            {
                while (_running)
                {
                    var client = _listener.AcceptTcpClient();
                    var th = new Thread(() => HandleClient(client));
                    th.IsBackground = true;
                    th.Start();
                }
            }
            catch { }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // Send greeting
                SendLine(stream, "* OK [CAPABILITY IMAP4rev1] Mock IMAP Server Ready");

                string selectedFolder = null;
                bool isExamine = false;

                string line;
                while (_running && (line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(new[] { ' ' }, 3);
                    if (parts.Length < 2) continue;

                    string tag = parts[0];
                    string cmd = parts[1].ToUpperInvariant();
                    string args = parts.Length > 2 ? parts[2] : "";

                    switch (cmd)
                    {
                        case "CAPABILITY":
                            SendLine(stream, "* CAPABILITY IMAP4rev1 STARTTLS");
                            SendLine(stream, tag + " OK CAPABILITY completed");
                            break;

                        case "STARTTLS":
                            SendLine(stream, tag + " BAD STARTTLS not enabled on plain mock");
                            break;

                        case "LOGIN":
                            SendLine(stream, tag + " OK [CAPABILITY IMAP4rev1] Logged in");
                            break;

                        case "LOGOUT":
                            SendLine(stream, "* BYE Logging out");
                            SendLine(stream, tag + " OK LOGOUT completed");
                            return;

                        case "LIST":
                            lock (Mailboxes)
                            {
                                foreach (var kv in Mailboxes.Keys)
                                {
                                    SendLine(stream, string.Format("* LIST (\\HasNoChildren) \"/\" \"{0}\"", kv));
                                }
                            }
                            SendLine(stream, tag + " OK LIST completed");
                            break;

                        case "CREATE":
                            string newFolder = args.Trim('\"', ' ');
                            lock (Mailboxes)
                            {
                                if (!Mailboxes.ContainsKey(newFolder))
                                {
                                    Mailboxes[newFolder] = new List<MockMessage>();
                                }
                            }
                            SendLine(stream, tag + " OK CREATE completed");
                            break;

                        case "EXAMINE":
                        case "SELECT":
                            isExamine = (cmd == "EXAMINE");
                            string folderToSelect = args.Trim('\"', ' ');
                            lock (Mailboxes)
                            {
                                if (!Mailboxes.ContainsKey(folderToSelect))
                                {
                                    if (!isExamine)
                                    {
                                        Mailboxes[folderToSelect] = new List<MockMessage>();
                                    }
                                    else
                                    {
                                        SendLine(stream, tag + " NO Mailbox does not exist");
                                        break;
                                    }
                                }
                                selectedFolder = folderToSelect;
                                var list = Mailboxes[selectedFolder];
                                SendLine(stream, string.Format("* {0} EXISTS", list.Count));
                                SendLine(stream, "* 0 RECENT");
                                SendLine(stream, "* OK [UIDVALIDITY 123456789] UIDs valid");
                                SendLine(stream, string.Format("* OK [{0}] {1} completed", isExamine ? "READ-ONLY" : "READ-WRITE", cmd));
                                SendLine(stream, tag + " OK [READ-" + (isExamine ? "ONLY" : "WRITE") + "] " + cmd + " completed");
                            }
                            break;

                        case "UID":
                            HandleUidCommand(stream, tag, args, selectedFolder);
                            break;

                        case "FETCH":
                            HandleFetchCommand(stream, tag, args, selectedFolder);
                            break;

                        case "APPEND":
                            HandleAppendCommand(stream, reader, tag, args);
                            break;

                        case "NOOP":
                            SendLine(stream, tag + " OK NOOP completed");
                            break;

                        default:
                            SendLine(stream, tag + " OK " + cmd + " completed");
                            break;
                    }
                }
            }
        }

        private void HandleUidCommand(Stream stream, string tag, string args, string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Mailboxes.ContainsKey(folder))
            {
                SendLine(stream, tag + " NO No folder selected");
                return;
            }

            string[] sub = args.Split(new[] { ' ' }, 2);
            string subCmd = sub[0].ToUpperInvariant();
            string subArgs = sub.Length > 1 ? sub[1] : "";

            if (subCmd == "SEARCH")
            {
                var sb = new StringBuilder("* SEARCH");
                lock (Mailboxes)
                {
                    foreach (var m in Mailboxes[folder])
                    {
                        sb.Append(" " + m.Uid);
                    }
                }
                SendLine(stream, sb.ToString());
                SendLine(stream, tag + " OK UID SEARCH completed");
            }
            else if (subCmd == "FETCH")
            {
                var match = Regex.Match(subArgs, @"^(\d+)");
                if (match.Success)
                {
                    uint targetUid = uint.Parse(match.Groups[1].Value);
                    MockMessage targetMsg = null;
                    lock (Mailboxes)
                    {
                        targetMsg = Mailboxes[folder].Find(m => m.Uid == targetUid);
                    }

                    if (targetMsg != null)
                    {
                        if (targetMsg.SimulateUnavailable)
                        {
                            SendLine(stream, tag + " NO [UNAVAILABLE] UID FETCH Completed. sc=mock_test_2026-11-14_");
                            return;
                        }

                        if (subArgs.IndexOf("BODY", StringComparison.OrdinalIgnoreCase) < 0 && subArgs.IndexOf("RFC822.SIZE", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string sizeLine = string.Format("* 1 FETCH (UID {0} RFC822.SIZE {1})", targetMsg.Uid, targetMsg.RawBytes.Length);
                            SendLine(stream, sizeLine);
                            SendLine(stream, tag + " OK UID FETCH completed");
                        }
                        else
                        {
                            string headerLine = string.Format("* 1 FETCH (UID {0} FLAGS ({1}) INTERNALDATE \"{2}\" RFC822.SIZE {3} BODY[] {{{4}}}",
                                targetMsg.Uid, targetMsg.Flags, targetMsg.InternalDate, targetMsg.RawBytes.Length, targetMsg.RawBytes.Length);
                            SendLine(stream, headerLine);
                            stream.Write(targetMsg.RawBytes, 0, targetMsg.RawBytes.Length);
                            SendLine(stream, ")");
                            SendLine(stream, tag + " OK UID FETCH completed");
                        }
                    }
                    else
                    {
                        SendLine(stream, tag + " NO UID not found");
                    }
                }
                else
                {
                    SendLine(stream, tag + " BAD Bad FETCH command");
                }
            }
        }

        private void HandleFetchCommand(Stream stream, string tag, string args, string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Mailboxes.ContainsKey(folder))
            {
                SendLine(stream, tag + " OK FETCH completed");
                return;
            }

            lock (Mailboxes)
            {
                var list = Mailboxes[folder];
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    string msgIdHeader = string.Format("Message-ID: <{0}>\r\n", m.MessageId);
                    byte[] hdrBytes = Encoding.UTF8.GetBytes(msgIdHeader);

                    SendLine(stream, string.Format("* {0} FETCH (BODY[HEADER.FIELDS (MESSAGE-ID)] {{{1}}}", i + 1, hdrBytes.Length));
                    stream.Write(hdrBytes, 0, hdrBytes.Length);
                    SendLine(stream, ")");
                }
            }
            SendLine(stream, tag + " OK FETCH completed");
        }

        private void HandleAppendCommand(Stream stream, StreamReader reader, string tag, string args)
        {
            // Parse: "DestFolder" (flags) "date" {bytes}
            var litMatch = Regex.Match(args, @"\{(\d+)\}\s*$");
            if (!litMatch.Success)
            {
                SendLine(stream, tag + " BAD Invalid APPEND syntax");
                return;
            }

            int byteLen = int.Parse(litMatch.Groups[1].Value);

            // Extract folder name
            string folder = "INBOX";
            var fMatch = Regex.Match(args, @"^""?([^""\s]+)""?");
            if (fMatch.Success)
            {
                folder = fMatch.Groups[1].Value;
            }

            // Extract flags
            string flags = "";
            var flMatch = Regex.Match(args, @"\(([^\)]*)\)");
            if (flMatch.Success)
            {
                flags = flMatch.Groups[1].Value;
            }

            // Extract date
            string date = "";
            var dMatch = Regex.Match(args, @"""([^""]{10,40})""");
            if (dMatch.Success)
            {
                date = dMatch.Groups[1].Value;
            }

            // Send continuation prompt '+'
            SendLine(stream, "+ Ready for literal data");

            // Read exact bytes
            byte[] buffer = new byte[byteLen];
            int total = 0;
            while (total < byteLen)
            {
                int r = stream.Read(buffer, total, byteLen - total);
                if (r <= 0) break;
                total += r;
            }

            // Read trailing CRLF
            reader.ReadLine();

            // Extract message-id
            string msgId = "";
            string txt = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 4096));
            var idMatch = Regex.Match(txt, @"Message-ID:\s*<([^>]+)>", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                msgId = idMatch.Groups[1].Value;
            }

            lock (Mailboxes)
            {
                if (!Mailboxes.ContainsKey(folder))
                {
                    Mailboxes[folder] = new List<MockMessage>();
                }

                uint newUid = (uint)(Mailboxes[folder].Count + 500);
                Mailboxes[folder].Add(new MockMessage
                {
                    Uid = newUid,
                    Flags = flags,
                    InternalDate = date,
                    MessageId = msgId,
                    RawBytes = buffer
                });
            }

            SendLine(stream, tag + " OK [APPENDUID 123456789 999] APPEND completed");
        }

        private void SendLine(Stream stream, string text)
        {
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(text + "\r\n");
                stream.Write(b, 0, b.Length);
                stream.Flush();
            }
            catch { }
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch { }
        }
    }
}
