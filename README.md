# IMAP to IMAP Email Migration Tool (Portable Windows App)

[![GitHub](https://img.shields.io/badge/GitHub-Qorege%2FBulk--IMAP--Migration-blue?logo=github)](https://github.com/Qorege/Bulk-IMAP-Migration)
[![Platform](https://img.shields.io/badge/Platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-0078D6?logo=windows)](https://github.com/Qorege/Bulk-IMAP-Migration)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.0%20--%204.8-512BD4)](https://github.com/Qorege/Bulk-IMAP-Migration)

A high-performance, resilient, modern, and portable Windows desktop application (`IMAPMigrator.exe`) designed for migrating single or multiple IMAP email accounts with direct IMAP-to-IMAP streaming, true deduplication, size filtering, and zero local disk bloating.

**Repository URL**: [https://github.com/Qorege/Bulk-IMAP-Migration](https://github.com/Qorege/Bulk-IMAP-Migration)

---

## 🚀 Key Advantages & Architecture

1. **Direct IMAP-to-IMAP Streaming (Zero Disk Bloat)**:
   - Does **not** store gigabytes of emails on local disk.
   - Messages stream directly between servers through memory buffers using RFC 3501 `FETCH (BODY.PEEK[])` and `APPEND` literals.
   - Preserves message flags (`\Seen`, `\Flagged`, `\Draft`, etc.), original headers, attachments, and `INTERNALDATE` with byte-for-byte fidelity.

2. **Source Mailbox Strictly Read-Only**:
   - For maximum security, the source mailbox is **never modified**.
   - Mailboxes are accessed exclusively using IMAP `EXAMINE` (Read-Only) mode.
   - Message payloads are retrieved with `BODY.PEEK[]` so the `\Seen` flag on the source mailbox remains untouched.

3. **Skip Emails Larger Than [ N ] MB**:
   - Easily set a size limit in **Settings** (checkbox: *"Skip emails larger than: [ N ] MB"*, customizable in megabytes).
   - Before downloading email bodies, the engine checks `RFC822.SIZE` via IMAP metadata. Oversized messages are skipped instantly without wasting bandwidth, incrementing the skipped counter and logging the skip reason.

4. **Folder Mapping via Dropdown Menu**:
   - Easily map any source folder to any destination folder using the dropdown menu (e.g., map `inbox2` ➔ `Archive`, or `Old_Sent` ➔ `Sent`).
   - Automatically populates with discovered folders from both mailboxes, standard system folders, plus an **+ Add new target folder** option for custom folder creation.

5. **True Deduplication & Crash-Proof Journal**:
   - Tracks both **Message-ID** and **UID** in a local migration journal (`migration_journal.tsv`).
   - Automatically pre-scans destination mailbox headers.
   - Safe to pause, stop, or re-run anytime: skips previously transferred emails instantly without duplicates.

6. **Modern & Elegant UI Styling**:
   - Clean, elevated card layouts with rounded corners (`12px` radius) and subtle borders (`#e2e8f0`).
   - Real-time `PillBadge` status indicators (`○ Not connected`, `Connecting...`, `✔ Connected`, `Error`).
   - Micro-typography with bold uppercase form labels for clean visual hierarchy.
   - Distinct iconography across all action buttons (`⚡ Test Connection`, `🔗 Connect`, `➕ Add to Queue`, `🚀 Start Migration`, `📂 Import CSV`, `📄 CSV Template`, `✏️ Edit Account`, `❌ Remove`, `▶ Start Queue`, `⏸ Pause`, `⏹ Stop`, `🧹 Clear Finished`).
   - High-contrast developer console activity log (`#0f172a` slate background, `#e2e8f0` text, Consolas font).
   - Embedded multi-resolution application icon (`app.ico` and `icon.png`) for Windows taskbar, window title, and header banner.

7. **Edit Queued / CSV Accounts with Full GUI Dialog**:
   - Select any account in the Bulk Migration Queue and click **✏️ Edit Account** (or double-click the row).
   - Opens an interactive editing dialog matching the Single Mailbox Migration tab, allowing you to edit credentials, test/connect, and customize folder mappings with dropdowns.

8. **Smart CSV Fallback (Auto-Defaults)**:
   - Any missing values in uploaded CSV files (hosts, ports, SSL, or folders) are automatically populated from your **Settings** defaults.
   - CSV files can be as simple as just `SourceUser,SourcePassword,DestUser,DestPassword`.

9. **Default Folders to Migrate in Settings**:
   - Configure default folders (e.g., `INBOX, Sent, Archive`) in the Settings dialog to automatically apply to new accounts and CSV imports when not specified.

10. **Automatic Recovery & Auto-Retry**:
    - Built-in resilience for socket drops, server rate limits, and network disconnects.
    - Automatically pauses and retries with progressive backoff up to **10+ times** until 100% completed.

11. **Queue Concurrency**:
    - **1 at a time** (Sequential, lowest server load)
    - **3 at a time** (Balanced throughput)
    - **Sync all accounts** (Full parallel migration)

12. **Multi-OS Compatibility**:
    - Runs out of the box on Windows 10 & 11 as well as older Windows (Windows 7, 8, 8.1) without installing third-party runtimes.
    - Full PerMonitorV2 high-DPI scaling support.

---

## 📁 Editions & File Structure

This repository provides **two distinct editions** powered by the same shared core engine:

### 1. Modern WPF MVVM Edition (`IMAPMigrator.WPF/`)
- **Target Runtime**: .NET 8 (Windows)
- **Architecture**: Strict MVVM pattern with `CommunityToolkit.Mvvm`
- **UI & Controls**: `MahApps.Metro`, `MaterialDesignThemes` (Material Design in XAML), and `WPF UI` (`Wpf.Ui`)
- **Charts & Telemetry**: `LiveCharts2` (`LiveChartsCore.SkiaSharpView.WPF`) with interactive Donut status breakdowns and smooth real-time Throughput line charts (Emails/sec)
- **Features**: Responsive layout, MahApps animated tabs, right-docked settings drawer Flyout, modal account editor, 1:1 side-by-side card views, dark developer terminal activity log.
- **Build / Run**: Double-click `build_wpf.bat` or run:
  ```powershell
  dotnet publish IMAPMigrator.WPF/IMAPMigrator.WPF.csproj -c Release -o bin_wpf
  ```

### 2. Standalone Portable WinForms Edition (`IMAPMigrator.exe`)
- **Target Runtime**: .NET Framework 4.0 - 4.8 (Runs out of the box on Windows 7, 8, 10, 11 with zero installations)
- **Binary**: Single standalone file `IMAPMigrator.exe`
- **Build / Run**: Double-click `build.bat`

---

## 📁 Source Tree

- **`IMAPMigrator.exe`**: Standalone, portable executable with embedded icon (`app.ico`).
- **`build_wpf.bat`**: One-click build and publish script for the WPF MVVM edition.
- **`build.bat`**: One-click build script for the standalone portable edition.
- **`sample_accounts.csv`**: Sample CSV template ready for bulk account import.
- **`settings.ini`**: Configuration file storing default servers, folders, concurrency, and size filters.
- **`IMAPMigrator.WPF/`**: Complete WPF MVVM source project:
  - `ViewModels/`: `MainViewModel`, `SingleMigrationViewModel`, `QueueViewModel`, `MonitorViewModel`, `SettingsViewModel`, `AccountEditViewModel`.
  - `Views/`: `SingleMigrationView`, `QueueView`, `MonitorView`, `AccountEditWindow`, `AboutWindow`.
  - `Models/`: `FolderMappingItem`, `FolderProgressModel`, `LogEntry`.
  - `Converters/`: `StatusColorConverter`, `InverseBooleanConverter`.
- **`src/`**: Shared core streaming engine (.NET Standard / .NET 4.0 - .NET 8 compatible):
  - `ImapClient.cs`: RFC 3501 IMAP client with SSL/TLS, STARTTLS, EXAMINE (Read-Only), SEARCH, PEEK, APPEND, and RFC822.SIZE query.
  - `MigrationEngine.cs`: Multi-threaded queue executor, auto-retry loop, progress calculations, and size skip logic.
  - `MigrationJournal.cs`: High-speed UID and Message-ID deduplication journal.
  - `CsvHandler.cs`: CSV parser with smart missing-value defaulting and template generator.
  - `MockImapServer.cs`: Built-in loopback IMAP server for offline self-testing and interactive demos.
  - `Settings.cs` & `SettingsDialog.cs`: Settings configuration for default hosts, default folders, retries, and size filters.
  - `AccountEditDialog.cs`: Full-featured account editor dialog matching the single mailbox layout.
  - `MainForm.cs`: Portable UI with folder dropdown mapping and live progress.
  - `Controls/CustomControls.cs`: Modern animated progress bars, rounded card panels, pill badges, and flat buttons.

---

## 🛠️ How to Use

### 1. Single Mailbox Migration
1. Double-click `IMAPMigrator.exe`.
2. Under **📤 Source Mailbox (Read-Only)**, select your email service and enter credentials.
3. Under **📥 Destination Mailbox**, enter the destination credentials.
4. Click **⚡ Test Connection** to verify server connectivity (`✓ Server responds`, `✓ SSL/TLS`, `✓ Sign-in`, `✓ Folders`).
5. Click **🔗 Connect** on both sides to discover folders.
6. In the folder mapping table, use the **Destination Folder (Dropdown)** column to choose where each folder's emails go (e.g. map `inbox2` to `Archive`).
7. Select your desired period filter (e.g., *All messages*, *Last 6 months*, *Choose dates...*).
8. Click **🚀 Start Migration** (or click **➕ Add to Queue** to queue it).

### 2. Bulk Migration via Queue / CSV
1. Switch to the **📋 Bulk Migration Queue** tab.
2. Click **📂 Import CSV** and select your `.csv` file. Any missing values will automatically be filled from Settings.
3. Double-click any row or click **✏️ Edit Account** to modify credentials or customize folder dropdown mappings.
4. Choose your concurrency option (*1 at a time*, *3 at a time*, or *Sync all*).
5. Click **▶ Start Queue**.
6. Switch to the **🚀 Migration Monitor** tab to watch animated progress, folder stats, and real-time logs.

### 3. Built-in Offline Demo & Self-Test
- From the application menu, select **Tools ➔ ▶ Run Built-in Live Demo** to automatically spin up two loopback IMAP test servers and watch direct streaming in action without entering any credentials.
- Alternatively, launch from terminal with `IMAPMigrator.exe --test` to run the comprehensive automated headless verification test suite.
