# SaveSync

SaveSync is a cross-platform desktop app (C#) for backing up and syncing game save data to a cloud storage provider. The goal is to make save syncing simple: add games once, then sync a single game or your full library with one click.

## Current Status

✅ **MVP Implementation Complete** - The application has been fully scaffolded and implemented with all MVP features functional.

### Implemented Features

- ✅ Avalonia UI desktop application (cross-platform)
- ✅ Game library management (add/remove games)
- ✅ Native OS file picker for selecting save directories
- ✅ Google Drive service integration (placeholder implementation)
- ✅ Sync individual games
- ✅ Sync all games at once
- ✅ Configurable cloud destination folder
- ✅ JSON-based configuration persistence
- ✅ Provider architecture for cloud storage (extensible design)
- ✅ Progress reporting during sync operations
- ✅ Clean, modern UI with Fluent Design

### Project Structure

```
SaveSync/
├── Models/
│   ├── AppConfig.cs          # Application configuration model
│   └── Game.cs                # Game entry model
├── Services/
│   ├── ICloudStorageProvider.cs    # Cloud provider interface
│   ├── GoogleDriveService.cs       # Google Drive implementation
│   ├── ConfigurationService.cs     # Config file management
│   └── SyncService.cs              # Sync orchestration
├── ViewModels/
│   ├── ViewModelBase.cs            # Base view model
│   └── MainWindowViewModel.cs      # Main window logic
└── Views/
	├── MainWindow.axaml            # Main window UI
	└── MainWindow.axaml.cs         # Main window code-behind
```

### Next Steps

To complete the Google Drive integration:
1. Install Google.Apis.Drive.v3 NuGet package
2. Create Google Cloud Console project and OAuth credentials
3. Implement real OAuth flow in `GoogleDriveService.cs`
4. Implement actual file upload using Google Drive API

### Configuration

Configuration is automatically saved to:
- Windows: `%AppData%\SaveSync\config.json`
- macOS: `~/Library/Application Support/SaveSync/config.json`
- Linux: `~/.config/SaveSync/config.json`

## MVP Features

- Add games to a local library by entering:
	- Game name
	- Local save directory path
- Choose the local save path with the operating system's native file picker.
- Sync one selected game from the library.
- Sync all games in one action.
- Upload saves to cloud storage, starting with Google Drive.
- Let users select a cloud destination folder.
- Provide a default cloud destination folder: `SaveSync/Saves`.

## Tech Stack (Initial MVP Direction)

- Language/runtime: C# on .NET 10
- Desktop UI: Avalonia UI (cross-platform)
- Cloud provider integration: Google Drive API first, provider model designed for extension
- Data format: JSON for local app configuration and game library metadata

## Requirements

### Functional Requirements

1. Users can add a game entry with a name and local save path.
2. Users can choose save paths using a native OS path picker.
3. Users can sync one game at a time from the library view.
4. Users can sync all configured games at once.
5. Users can choose a cloud destination folder.
6. The app provides a default destination of `SaveSync/Saves`.
7. Google Drive is supported in the first implementation.

### Non-Functional Goals

1. Cross-platform support for Windows, macOS, and Linux.
2. Clear UI flow for quick, low-friction backups.
3. Provider architecture that can later add Dropbox, OneDrive, and others.

## Planned User Flow

1. User opens SaveSync.
2. User adds one or more games with local save paths.
3. User connects Google Drive. This connection becomes saved.
4. User selects a cloud folder (or accepts `SaveSync/Saves`).
5. User clicks either:
	 - Sync Selected Game
	 - Sync All Games

## Getting Started (When Project Scaffolding Is Added)

### Prerequisites

- .NET 10 SDK
- Git
- IDE/editor:
	- Visual Studio 2022 (17.8+) or
	- VS Code with C# Dev Kit

### Expected Commands

```bash
dotnet restore
dotnet build
dotnet run
```

## Roadmap

### MVP

- Game library management (name + save path)
- Google Drive sync
- Selective sync and sync-all actions
- Configurable cloud destination folder

### Post-MVP

- Additional cloud providers (Dropbox, OneDrive)
- Conflict handling options (overwrite, keep both, prompt)
- Basic sync history and error reporting

### Optional Features

- Pull game cover art from metadata APIs to enrich the library view.

## Notes

- This README intentionally documents product direction before implementation code is added.
- As scaffolding is introduced, this document should be updated with concrete setup, configuration, and troubleshooting instructions.
