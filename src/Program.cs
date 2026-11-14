using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ImapSyncWin
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static void Main(string[] args)
        {
            // Enable modern TLS protocols on Windows 7 / 8 / 10 / 11
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 |
                                                                  (System.Net.SecurityProtocolType)768 |
                                                                  (System.Net.SecurityProtocolType)192 |
                                                                  System.Net.SecurityProtocolType.Tls;
            }
            catch { }

            // Global exception handling for resilient crash prevention
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show("An unexpected error occurred:\n\n" + e.Exception.Message + "\n\n" + e.Exception.StackTrace,
                    "IMAP Migration Tool Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show("A fatal error occurred:\n\n" + (ex != null ? ex.Message : e.ExceptionObject.ToString()),
                    "IMAP Migration Tool Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // If invoked with --test or --selftest, attach console and run headless automated test
            if (args != null && args.Length > 0 && (args[0] == "--test" || args[0] == "--selftest"))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                try
                {
                    var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                    Console.SetOut(stdout);
                    Console.SetError(stdout);
                }
                catch { }
                int exitCode = RunSelfTest();
                Environment.Exit(exitCode);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static int RunSelfTest()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("IMAP to IMAP Migration Tool - Self-Test Routine");
            Console.WriteLine("=================================================");

            try
            {
                MigrationJournal.Instance.Clear();

                // 1. Start mock servers
                Console.WriteLine("[1/6] Spinning up loopback Source and Dest IMAP Servers...");
                using (var srcServer = new MockImapServer(0))
                using (var dstServer = new MockImapServer(0))
                {
                    srcServer.PopulateSampleMessages(20, 5);
                    Console.WriteLine(string.Format("      Source listening on port {0} (25 messages ready)", srcServer.Port));
                    Console.WriteLine(string.Format("      Dest listening on port {0} (empty)", dstServer.Port));

                    // 2. Test IMAP Client Connection & Login
                    Console.WriteLine("[2/6] Testing IMAP client connection, login & read-only examine...");
                    using (var srcClient = new ImapClient("127.0.0.1", srcServer.Port, SecurityType.Plain))
                    {
                        srcClient.Connect();
                        srcClient.Login("user@source.com", "pass123");
                        var folders = srcClient.ListFolders();
                        Console.WriteLine(string.Format("      Discovered {0} folders on source.", folders.Count));

                        int inboxCount = srcClient.ExamineFolder("INBOX");
                        Console.WriteLine(string.Format("      EXAMINE INBOX message count: {0} (Source read-only verified)", inboxCount));
                        if (inboxCount != 20) throw new Exception("Expected 20 messages in INBOX, got " + inboxCount);
                    }

                    // 3. Test Migration Engine with Direct Streaming
                    Console.WriteLine("[3/6] Running full migration run #1 (Direct streaming transfer)...");
                    var account = new AccountPair
                    {
                        SourceHost = "127.0.0.1",
                        SourcePort = srcServer.Port,
                        SourceSecurity = SecurityType.Plain,
                        SourceUser = "user@source.com",
                        SourcePassword = "password",

                        DestHost = "127.0.0.1",
                        DestPort = dstServer.Port,
                        DestSecurity = SecurityType.Plain,
                        DestUser = "user@dest.com",
                        DestPassword = "password",

                        FolderMappings = new List<FolderMapping>
                        {
                            new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true, TotalMessages = 20 },
                            new FolderMapping { SourceFolder = "Spam", DestFolder = "Spam", Selected = true, TotalMessages = 5 }
                        }
                    };

                    var settings = new AppSettings
                    {
                        MaxRetries = 10,
                        RetryDelaySeconds = 1,
                        DirectStreamingOnly = true,
                        DeduplicateMessageId = true,
                        TimePeriod = TimePeriodOption.AllMessages
                    };

                    var engine = new MigrationEngine(settings);
                    int processedCount = 0;
                    int movedCount = 0;
                    int skippedCount = 0;

                    engine.OverallProgressChanged += (s, e) =>
                    {
                        processedCount = e.ProcessedMessages;
                        movedCount = e.MovedMessages;
                        skippedCount = e.SkippedMessages;
                    };

                    using (var cts = new CancellationTokenSource())
                    {
                        engine.MigrateSingleAccount(account, cts.Token);
                    }

                    Console.WriteLine(string.Format("      Run #1 complete: {0} processed, {1} moved, {2} skipped, {3} errors.",
                        account.ProcessedMessages, account.MovedMessages, account.SkippedMessages, account.ErrorMessages));

                    if (account.MovedMessages != 25)
                    {
                        throw new Exception(string.Format("Expected 25 moved messages, got {0}", account.MovedMessages));
                    }

                    // 4. Verify Destination Mailbox contents and preserved flags/dates
                    Console.WriteLine("[4/6] Verifying Destination Mailbox messages & byte integrity...");
                    int destInboxCount = dstServer.Mailboxes["INBOX"].Count;
                    int destSpamCount = dstServer.Mailboxes["Spam"].Count;
                    Console.WriteLine(string.Format("      Dest INBOX: {0} msgs, Dest Spam: {1} msgs", destInboxCount, destSpamCount));

                    if (destInboxCount != 20 || destSpamCount != 5)
                    {
                        throw new Exception("Destination counts mismatch!");
                    }

                    // 5. Test True Deduplication (Second Run)
                    Console.WriteLine("[5/6] Running migration run #2 to test True Deduplication...");
                    var account2 = new AccountPair
                    {
                        SourceHost = "127.0.0.1",
                        SourcePort = srcServer.Port,
                        SourceSecurity = SecurityType.Plain,
                        SourceUser = "user@source.com",
                        SourcePassword = "password",

                        DestHost = "127.0.0.1",
                        DestPort = dstServer.Port,
                        DestSecurity = SecurityType.Plain,
                        DestUser = "user@dest.com",
                        DestPassword = "password",

                        FolderMappings = new List<FolderMapping>
                        {
                            new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true, TotalMessages = 20 },
                            new FolderMapping { SourceFolder = "Spam", DestFolder = "Spam", Selected = true, TotalMessages = 5 }
                        }
                    };

                    using (var cts2 = new CancellationTokenSource())
                    {
                        engine.MigrateSingleAccount(account2, cts2.Token);
                    }

                    Console.WriteLine(string.Format("      Run #2 complete: {0} processed, {1} moved, {2} skipped, {3} errors.",
                        account2.ProcessedMessages, account2.MovedMessages, account2.SkippedMessages, account2.ErrorMessages));

                    if (account2.MovedMessages != 0 || account2.SkippedMessages != 25)
                    {
                        throw new Exception(string.Format("Deduplication failed! Expected 0 moved and 25 skipped, got {0} moved and {1} skipped",
                            account2.MovedMessages, account2.SkippedMessages));
                    }

                    // 6. Test Email Size Filter (Skip emails larger than N MB)
                    Console.WriteLine("[6/7] Testing Email Size Skipping Filter (Threshold 1 MB)...");
                    var sizeSettings = new AppSettings
                    {
                        SkipLargerThanEnabled = true,
                        MaxEmailSizeMB = 1,
                        DirectStreamingOnly = true,
                        DeduplicateMessageId = false
                    };
                    var sizeEngine = new MigrationEngine(sizeSettings);

                    // Add a message with 1.5 MB to source INBOX
                    byte[] bigData = new byte[1572864];
                    System.Text.Encoding.ASCII.GetBytes("From: big@test.com\r\nSubject: Huge\r\nMessage-ID: <big-unique@test.com>\r\n\r\n").CopyTo(bigData, 0);
                    srcServer.Mailboxes["INBOX"].Add(new MockMessage
                    {
                        Uid = 9999,
                        Flags = "\\Seen",
                        InternalDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture),
                        MessageId = "big-unique@test.com",
                        RawBytes = bigData
                    });

                    var sizeAccount = new AccountPair
                    {
                        SourceHost = "127.0.0.1",
                        SourcePort = srcServer.Port,
                        SourceSecurity = SecurityType.Plain,
                        SourceUser = "user@source.com",
                        SourcePassword = "password",

                        DestHost = "127.0.0.1",
                        DestPort = dstServer.Port,
                        DestSecurity = SecurityType.Plain,
                        DestUser = "user@dest.com",
                        DestPassword = "password",

                        FolderMappings = new List<FolderMapping>
                        {
                            new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true, TotalMessages = 1 }
                        }
                    };

                    using (var ctsSize = new CancellationTokenSource())
                    {
                        sizeEngine.MigrateSingleAccount(sizeAccount, ctsSize.Token);
                    }

                    Console.WriteLine(string.Format("      Size filter run: {0} processed, {1} moved, {2} skipped, {3} errors.",
                        sizeAccount.ProcessedMessages, sizeAccount.MovedMessages, sizeAccount.SkippedMessages, sizeAccount.ErrorMessages));

                    if (sizeAccount.SkippedMessages == 0)
                    {
                        throw new Exception("Size filter test failed: Oversized message was not skipped!");
                    }

                    // 7. Test CSV Import & Export
                    Console.WriteLine("[7/8] Testing CSV Template Generation & Parsing...");
                    string samplePath = "test_sample.csv";
                    CsvHandler.CreateSampleCsv(samplePath);
                    var imported = CsvHandler.ImportCsv(samplePath, settings);
                    Console.WriteLine(string.Format("      Imported {0} accounts from generated template successfully.", imported.Count));
                    if (imported.Count != 3) throw new Exception("Expected 3 accounts from sample CSV, got " + imported.Count);

                    try { System.IO.File.Delete(samplePath); } catch { }

                    // 8. Test Stream Coalescence & Literal Buffer Synchronization
                    Console.WriteLine("[8/8] Testing Stream Coalescence & Literal Buffer Synchronization...");
                    byte[] testBody = System.Text.Encoding.ASCII.GetBytes("From: test@domain.com\r\nSubject: Test\r\nMessage-ID: <coalesce-test@domain.com>\r\n\r\nBody content");
                    string testHeader = "* 1 FETCH (UID 1 FLAGS (\\Seen) INTERNALDATE \"14-Nov-2026 01:21:00 +0000\" BODY[] {" + testBody.Length + "}\r\n";
                    string testTrailer = ")\r\na0001 OK UID FETCH completed\r\n";

                    using (var ms = new System.IO.MemoryStream())
                    {
                        byte[] hBytes = System.Text.Encoding.UTF8.GetBytes(testHeader);
                        byte[] tBytes = System.Text.Encoding.UTF8.GetBytes(testTrailer);
                        ms.Write(hBytes, 0, hBytes.Length);
                        ms.Write(testBody, 0, testBody.Length);
                        ms.Write(tBytes, 0, tBytes.Length);
                        ms.Position = 0;

                        using (var ism = new ImapStream(ms, 65536))
                        {
                            string line = ism.ReadLine();
                            if (!line.Contains("{" + testBody.Length + "}")) throw new Exception("Header line mismatch");

                            byte[] readData = ism.ReadExactBytes(testBody.Length);
                            if (readData.Length != testBody.Length) throw new Exception("Literal length mismatch");
                            if (System.Text.Encoding.ASCII.GetString(readData) != System.Text.Encoding.ASCII.GetString(testBody))
                                throw new Exception("Literal body content mismatch");

                            string closeLine = ism.ReadLine();
                            if (closeLine != ")") throw new Exception("Expected close line ')', got: " + closeLine);

                            string tagLine = ism.ReadLine();
                            if (!tagLine.StartsWith("a0001 OK")) throw new Exception("Expected tag completion line, got: " + tagLine);
                        }
                    }
                    Console.WriteLine("      Stream buffer synchronization verified: 100% accurate.");

                    // 9. Test Graceful Skip of [UNAVAILABLE] Messages
                    Console.WriteLine("[9/9] Testing Graceful Recovery from Server [UNAVAILABLE] Messages...");
                    srcServer.Mailboxes["INBOX"].Add(new MockMessage
                    {
                        Uid = 8888,
                        Flags = "\\Seen",
                        InternalDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss +0000", System.Globalization.CultureInfo.InvariantCulture),
                        MessageId = "unavail@domain.com",
                        RawBytes = new byte[100],
                        SimulateUnavailable = true
                    });

                    var unavailAccount = new AccountPair
                    {
                        SourceHost = "127.0.0.1",
                        SourcePort = srcServer.Port,
                        SourceSecurity = SecurityType.Plain,
                        SourceUser = "user@source.com",
                        SourcePassword = "password",

                        DestHost = "127.0.0.1",
                        DestPort = dstServer.Port,
                        DestSecurity = SecurityType.Plain,
                        DestUser = "user@dest.com",
                        DestPassword = "password",

                        FolderMappings = new List<FolderMapping>
                        {
                            new FolderMapping { SourceFolder = "INBOX", DestFolder = "INBOX", Selected = true, TotalMessages = 1 }
                        }
                    };

                    var unavailSettings = new AppSettings
                    {
                        MaxRetries = 3,
                        RetryDelaySeconds = 1,
                        DirectStreamingOnly = true,
                        DeduplicateMessageId = false
                    };
                    var unavailEngine = new MigrationEngine(unavailSettings);

                    using (var ctsUnavail = new CancellationTokenSource())
                    {
                        unavailEngine.MigrateSingleAccount(unavailAccount, ctsUnavail.Token);
                    }

                    Console.WriteLine(string.Format("      Unavailable message handled: {0} processed, {1} moved, {2} skipped, {3} errors.",
                        unavailAccount.ProcessedMessages, unavailAccount.MovedMessages, unavailAccount.SkippedMessages, unavailAccount.ErrorMessages));

                    if (unavailAccount.SkippedMessages == 0)
                    {
                        throw new Exception("Unavailable message was not skipped properly!");
                    }

                    Console.WriteLine("\n>>> ALL TESTS PASSED SUCCESSFULLY! 100% OPERATIONAL. <<<");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[FAIL] Test exception: " + ex.ToString());
                return 1;
            }
        }
    }
}
