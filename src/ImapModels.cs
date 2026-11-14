using System;
using System.Collections.Generic;

namespace ImapSyncWin
{
    public enum SecurityType
    {
        SslTls,      // Port 993 (implicit TLS)
        StartTls,    // Port 143 (explicit STARTTLS)
        Plain        // Port 143 (unencrypted)
    }

    public enum TimePeriodOption
    {
        AllMessages,
        Last6Months,
        LastYear,
        CustomDates
    }

    public enum QueueConcurrencyOption
    {
        OneAtATime = 1,
        ThreeAtATime = 3,
        SyncAll = 0
    }

    public enum AccountSyncStatus
    {
        Idle,
        Pending,
        Connecting,
        Scanning,
        Syncing,
        Retrying,
        Completed,
        Failed,
        Stopped
    }

    public class EmailProviderPreset
    {
        public string Name { get; set; }
        public string DefaultHost { get; set; }
        public int DefaultPort { get; set; }
        public SecurityType Security { get; set; }
        public string HelpText { get; set; }

        public static List<EmailProviderPreset> GetPresets()
        {
            return new List<EmailProviderPreset>
            {
                new EmailProviderPreset
                {
                    Name = "Gmail",
                    DefaultHost = "imap.gmail.com",
                    DefaultPort = 993,
                    Security = SecurityType.SslTls,
                    HelpText = "Gmail requires an App Password if 2-Step Verification is on. Create one at myaccount.google.com/apppasswords"
                },
                new EmailProviderPreset
                {
                    Name = "Microsoft 365 / Outlook.com",
                    DefaultHost = "outlook.office365.com",
                    DefaultPort = 993,
                    Security = SecurityType.SslTls,
                    HelpText = "For Microsoft 365, ensure IMAP access is enabled in the Exchange Admin Center."
                },
                new EmailProviderPreset
                {
                    Name = "Yahoo Mail",
                    DefaultHost = "imap.mail.yahoo.com",
                    DefaultPort = 993,
                    Security = SecurityType.SslTls,
                    HelpText = "Yahoo requires an App Password from your Yahoo account security settings."
                },
                new EmailProviderPreset
                {
                    Name = "iCloud Mail",
                    DefaultHost = "imap.mail.me.com",
                    DefaultPort = 993,
                    Security = SecurityType.SslTls,
                    HelpText = "iCloud requires an App-Specific Password generated from appleid.apple.com."
                },
                new EmailProviderPreset
                {
                    Name = "Other IMAP server",
                    DefaultHost = "mail.example.com",
                    DefaultPort = 993,
                    Security = SecurityType.SslTls,
                    HelpText = "Enter your custom mail server host, port, and security settings."
                }
            };
        }
    }

    public class ImapFolder
    {
        public string Name { get; set; }
        public string RawName { get; set; }
        public string Delimiter { get; set; }
        public List<string> Attributes { get; set; }
        public int MessageCount { get; set; }
        public bool IsSelectable { get; set; }

        public ImapFolder()
        {
            Attributes = new List<string>();
            IsSelectable = true;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class FolderMapping
    {
        public string SourceFolder { get; set; }
        public string DestFolder { get; set; }
        public bool Selected { get; set; }
        public int TotalMessages { get; set; }
        public int Moved { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public string Status { get; set; }

        public FolderMapping()
        {
            Selected = true;
            Status = "Queued";
        }
    }

    public class AccountPair
    {
        public string Id { get; set; }

        // Source Mailbox
        public string SourceProvider { get; set; }
        public string SourceHost { get; set; }
        public int SourcePort { get; set; }
        public SecurityType SourceSecurity { get; set; }
        public string SourceUser { get; set; }
        public string SourcePassword { get; set; }

        // Destination Mailbox
        public string DestProvider { get; set; }
        public string DestHost { get; set; }
        public int DestPort { get; set; }
        public SecurityType DestSecurity { get; set; }
        public string DestUser { get; set; }
        public string DestPassword { get; set; }

        // Mappings & Progress
        public List<FolderMapping> FolderMappings { get; set; }
        public AccountSyncStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public int TotalMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public int MovedMessages { get; set; }
        public int SkippedMessages { get; set; }
        public int ErrorMessages { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public AccountPair()
        {
            Id = Guid.NewGuid().ToString("N");
            SourceProvider = "Other IMAP server";
            SourcePort = 993;
            SourceSecurity = SecurityType.SslTls;
            DestProvider = "Other IMAP server";
            DestPort = 993;
            DestSecurity = SecurityType.SslTls;
            FolderMappings = new List<FolderMapping>();
            Status = AccountSyncStatus.Idle;
            StatusMessage = "Ready";
        }
    }

    public class AppSettings
    {
        public string DefaultSourceHost { get; set; }
        public int DefaultSourcePort { get; set; }
        public SecurityType DefaultSourceSecurity { get; set; }

        public string DefaultDestHost { get; set; }
        public int DefaultDestPort { get; set; }
        public SecurityType DefaultDestSecurity { get; set; }

        public TimePeriodOption TimePeriod { get; set; }
        public DateTime? CustomStartDate { get; set; }
        public DateTime? CustomEndDate { get; set; }

        public QueueConcurrencyOption QueueConcurrency { get; set; }
        public int MaxRetries { get; set; }
        public int RetryDelaySeconds { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool DirectStreamingOnly { get; set; }
        public bool DeduplicateMessageId { get; set; }
        public string DefaultFoldersToMigrate { get; set; }
        public bool SkipLargerThanEnabled { get; set; }
        public int MaxEmailSizeMB { get; set; }

        public AppSettings()
        {
            DefaultSourceHost = "imap.example.com";
            DefaultSourcePort = 993;
            DefaultSourceSecurity = SecurityType.SslTls;

            DefaultDestHost = "imap.example.com";
            DefaultDestPort = 993;
            DefaultDestSecurity = SecurityType.SslTls;

            TimePeriod = TimePeriodOption.AllMessages;
            CustomStartDate = DateTime.Today.AddMonths(-1);
            CustomEndDate = DateTime.Today;

            QueueConcurrency = QueueConcurrencyOption.OneAtATime;
            MaxRetries = 10;
            RetryDelaySeconds = 3;
            TimeoutSeconds = 45;
            DirectStreamingOnly = true;
            DeduplicateMessageId = true;
            DefaultFoldersToMigrate = "INBOX, Sent, Archive";
            SkipLargerThanEnabled = false;
            MaxEmailSizeMB = 25;
        }
    }
}
