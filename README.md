# MailDirector

**MailDirector** is a modern, fast, and customizable desktop email management client built for Windows using WPF, .NET 9, and the [WPF-UI](https://github.com/lepoco/wpfui) Fluent Design library. It integrates seamlessly with Microsoft 365 and Outlook via the Microsoft Graph API, offering powerful automated rule-based organization, custom folder hierarchies, flexible reading layouts, and offline/demo capabilities.

---

## Features

- **Modern Fluent UI**: Clean, responsive interface built with WPF-UI following Windows 11 Fluent Design principles with full Dark and Light theme support.
- **Microsoft Graph Integration**: Native authentication and email synchronization with Microsoft 365 / Outlook via Azure Entra ID interactive browser login and secure token caching.
- **YAML-Driven Rules Engine**: Define powerful, human-readable filter rules stored as individual `.yaml` files. Execute rules on demand across entire folders or automatically on individual messages.
- **Rule Actions**: Support for categorizing, starring/flagging, moving to target folders, archiving, and marking messages as read.
- **Quick Rule Creation**: Create new rules or append criteria to existing rules directly from any selected email.
- **Custom Folder Management**:
  - Reorder folders with custom sorting.
  - Rename folders and assign custom icons and accent colors.
  - Toggle folder visibility (show/hide folders).
  - Nested folder tree hierarchy support.
- **Flexible Reading Pane Layouts**: Toggle between **Right**, **Bottom**, or **Off** (list-only) reading pane orientations.
- **Real-Time Filtering & Search**: Instant full-text search across sender, subject, and message previews.
- **Built-in Demo Mode**: Built-in mock email service allowing full UI testing and rule execution without requiring Microsoft 365 credentials.

---

## Tech Stack

- **Target Framework**: .NET 9.0 (`net9.0-windows7.0`)
- **UI Framework**: Windows Presentation Foundation (WPF) with `WPF-UI` (Fluent Design)
- **Email & Cloud API**: `Microsoft.Graph` v5.x & `Azure.Identity`
- **Rule Serialization**: `YamlDotNet`
- **JSON Serialization**: `System.Text.Json`
- **Test Framework**: `xUnit` with `Microsoft.NET.Test.Sdk`

---

## Project Structure

```text
maildirector/
├── MailDirector/                     # Main WPF Application
│   ├── Converters/                   # XAML Value Converters (Icons, Colors, Layouts)
│   ├── Models/                       # Domain models (AppConfig, FilterRule, EmailModels)
│   ├── Services/                     # Business logic and external services
│   │   ├── IEmailService.cs          # Email provider contract
│   │   ├── MicrosoftGraphEmailService.cs # MS Graph API client
│   │   ├── MockEmailService.cs       # Mock provider for demo mode
│   │   ├── RuleService.cs            # YAML rule loader, matcher, and executor
│   │   ├── SettingsService.cs        # App configuration & preferences persistence
│   │   ├── StorageService.cs         # AppData directory and file path management
│   │   └── FolderPreferenceService.cs# Custom folder ordering, colors, and visibility
│   ├── Themes/                       # Fluent theme definitions and resource dictionaries
│   ├── ViewModels/                   # MVVM ViewModels (MainViewModel, Rules, Settings, etc.)
│   ├── Views/                        # Windows and Dialog views (MainWindow, CreateRule, etc.)
│   └── App.xaml / App.xaml.cs        # Application entry point & service bootstrap
├── MailDirector.Tests/               # Unit test suite (xUnit)
│   ├── FolderPreferenceServiceTests.cs
│   ├── MainViewModelTests.cs
│   └── RuleServiceTests.cs
├── MailDirector.sln                  # Visual Studio / Rider solution
└── README.md                         # Project documentation
```

---

## Getting Started

### Prerequisites

- **Operating System**: Windows 10 (version 1809 or higher) / Windows 11
- **SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (version 9.0.100 or later)
- **IDE**: JetBrains Rider, Visual Studio 2022 (v17.12+), or Visual Studio Code with C# Dev Kit.

### Installation & Build

1. Clone the repository:
   ```bash
   git clone https://github.com/ARmunro/maildirector.git
   cd maildirector
   ```

2. Restore dependencies and build the solution:
   ```powershell
   dotnet build MailDirector.sln
   ```

3. Run the application:
   ```powershell
   dotnet run --project MailDirector/MailDirector.csproj
   ```

---

## Microsoft 365 & Azure Setup

To connect MailDirector to an active Microsoft 365 / Outlook mailbox:

1. Register an application in the **[Microsoft Entra Admin Center](https://entra.microsoft.com/)** (formerly Azure Active Directory):
   - **Supported account types**: Accounts in any organizational directory or personal Microsoft accounts (depending on your requirements).
   - **Redirect URI**: Add a **Public client/native (mobile & desktop)** redirect URI: `http://localhost`.
2. Configure **API Permissions** (Delegated permissions):
   - `User.Read`
   - `Mail.Read`
   - `Mail.ReadWrite`
   - `offline_access`
3. In MailDirector:
   - Click the **Settings** gear icon in the title bar / sidebar.
   - Enter your **Tenant ID** (or `common` / `consumers`) and **Client ID** (Application ID).
   - Save the settings. On the next refresh or startup, a browser window will open for OAuth2 interactive authentication.
   - Tokens are securely cached locally in `%APPDATA%\MailDirector\auth_record_flightmail.bin`.

---

## Configuration & Storage

MailDirector stores user settings, token caches, and rule definitions in the user's roaming application data directory (`%APPDATA%\MailDirector`):

- **`config.json`**: Primary application configuration (Azure credentials, theme, reading pane layout, refresh interval).
- **`email_preferences.json`**: Folder customizations (ordering, custom display names, icons, colors, hidden state).
- **`Rules/*.yaml`**: Filter rules stored as individual YAML files.
- **`auth_record_flightmail.bin`**: Serialized Microsoft Graph authentication token cache.

### Example `config.json`

```json
{
  "MicrosoftGraph": {
    "TenantId": "your-azure-tenant-id",
    "ClientId": "your-azure-client-id",
    "ClientSecret": ""
  },
  "EmailPreferences": {
    "ReadingPaneLayout": "Right",
    "PageSize": 50,
    "RefreshIntervalSeconds": 120,
    "AutoRefreshEnabled": true,
    "DarkTheme": true
  },
  "Debug": {
    "DemoMode": false
  }
}
```

> **Note**: If `DemoMode` is set to `true` or Microsoft Graph credentials are empty, the application automatically runs in **Demo Mode**, populated with mock email messages and folders for testing.

---

## Rules Engine

MailDirector includes a powerful rule matching engine. Rules can match against sender addresses, subject lines, body text, or attachment presence, and execute multiple automated actions.

### Rule YAML Schema

Each rule is saved as a `.yaml` file under `%APPDATA%\MailDirector\Rules\`.

```yaml
name: GitHub Activity
color: '#2EA44F'
rootFolder: Inbox
filters:
  - from:
      - github.com
      - notifications@github.com
    subjectContains:
      - '[GitHub]'
actions:
  - type: AddCategory
    value: Dev
  - type: Star
```

### Supported Filter Criteria

| Field | Type | Description |
|---|---|---|
| `from` | List of strings | Matches sender display name or email address (case-insensitive substring). |
| `subjectContains` | List of strings | Matches subject line content (case-insensitive substring). |
| `bodyContains` | List of strings | Matches email body text or preview snippet (case-insensitive substring). |
| `hasAttachments` | Boolean | Checks whether the email contains attachments. |

### Supported Actions

| Action Type | Value | Description |
|---|---|---|
| `AddCategory` | Category name | Assigns an Outlook category to the email. |
| `Star` | None | Flags / stars the email. |
| `ClearFlag` | None | Removes flag from the email. |
| `MarkAsRead` | None | Marks the message as read. |
| `Move` | Destination folder | Moves the email to the specified folder (e.g. `Archive`, `Junk`). |
| `Archive` | None | Moves the email to the default Archive folder. |

---

## Running Tests

The test suite is written with **xUnit** and covers folder preference management, email view model logic, and rule execution.

Run all tests via .NET CLI:

```powershell
dotnet test MailDirector.Tests/MailDirector.Tests.csproj
```

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
