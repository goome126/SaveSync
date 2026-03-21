# SaveSync - User Guide

## Quick Start

1. **Launch SaveSync**
   ```bash
   dotnet run
   ```

2. **Connect to Google Drive**
   - Click the "Connect Google Drive" button in the top-right corner
   - The connection will be simulated (placeholder for MVP)
   - You'll see a green "✓ Google Drive Connected" indicator when connected

3. **Add Your First Game**
   - In the "Add New Game" section on the left:
     - Enter the game name (e.g., "Minecraft")
     - Enter or browse for the save directory path
     - Click "Browse..." to use the native file picker
   - Click "Add Game"
   - The game will appear in the list below

4. **Sync Your Games**
   - **Sync One Game**: Select a game from the list and click "Sync Selected Game"
   - **Sync All Games**: Click "Sync All Games" to backup all games at once
   - Watch the progress bar and status messages

## Features Explained

### Game Library Management

**Adding Games**
- Game Name: A friendly name for the game
- Save Path: The full path to where the game stores its save files
- Use the "Browse..." button to navigate to the folder visually

**Removing Games**
- Select a game from the list
- Click "Remove Selected Game"
- The game will be removed from your library (but files remain on your PC)

### Cloud Sync

**Cloud Destination Folder**
- Default: `SaveSync/Saves`
- Change this to organize your backups differently
- Each game gets its own subfolder (e.g., `SaveSync/Saves/Minecraft`)

**Sync Operations**
- **Sync Selected Game**: Backs up only the selected game
- **Sync All Games**: Backs up all games in your library
- Progress is shown with a percentage bar
- Status messages keep you informed

### Connection Management

**Connect Google Drive**
- Currently simulates connection (placeholder)
- In production, this will open Google's OAuth login

**Disconnect**
- Removes the connection
- Clears stored credentials

## Common Game Save Locations

### Windows

**Steam Games**
```
C:\Program Files (x86)\Steam\userdata\[USER_ID]\[GAME_ID]\remote
```

**Epic Games**
```
C:\Users\[USERNAME]\AppData\Local\[GAME_NAME]\Saved
```

**Xbox Game Pass**
```
C:\Users\[USERNAME]\AppData\Local\Packages\[GAME_PACKAGE]
```

**Minecraft**
```
C:\Users\[USERNAME]\AppData\Roaming\.minecraft\saves
```

**Terraria**
```
C:\Users\[USERNAME]\Documents\My Games\Terraria
```

### macOS

**Steam Games**
```
~/Library/Application Support/Steam/userdata/[USER_ID]/[GAME_ID]/remote
```

**Minecraft**
```
~/Library/Application Support/minecraft/saves
```

### Linux

**Steam Games**
```
~/.local/share/Steam/userdata/[USER_ID]/[GAME_ID]/remote
```

**Minecraft**
```
~/.minecraft/saves
```

## Configuration File

SaveSync stores your configuration in a JSON file:

**Windows**: `%AppData%\SaveSync\config.json`
**macOS**: `~/Library/Application Support/SaveSync/config.json`
**Linux**: `~/.config/SaveSync/config.json`

You can manually edit this file if needed, but the app manages it automatically.

## Tips and Tricks

1. **Add Multiple Games**
   - Add all your games at once, then use "Sync All Games" regularly

2. **Organize by Type**
   - Use descriptive names: "Minecraft - Creative World" vs "Minecraft - Survival"

3. **Test Before Important Games**
   - Add a test game first to verify the sync works as expected

4. **Regular Backups**
   - Sync after important gaming sessions
   - Consider setting up a schedule (future feature)

5. **Check the Status Bar**
   - Always check the bottom status bar for operation results
   - Error messages will appear there

## Troubleshooting

### "Please connect to Google Drive first"
- Click the "Connect Google Drive" button in the top-right
- Wait for the connection indicator to show green

### "Please enter both game name and save path"
- Make sure both fields are filled before clicking "Add Game"

### "Please select a game to remove"
- Click on a game in the list to select it before removing

### Game doesn't appear in list after adding
- Check the status bar for error messages
- Verify both name and path were entered correctly

### Can't find my game's save folder
- Check the "Common Game Save Locations" section above
- Search online: "[Game Name] save file location"
- Enable "Show hidden files" in your file explorer

## Keyboard Shortcuts

Currently, all operations are mouse-based. Keyboard shortcuts may be added in future updates.

## Data Privacy

- All configuration is stored locally on your computer
- No data is sent anywhere except when you manually trigger a sync
- In the production version, data goes directly to your personal Google Drive
- SaveSync developers never have access to your save files or credentials

## Known Limitations (MVP)

1. **Google Drive is simulated** - Not actually uploading yet (see TODO-GOOGLE-DRIVE.md)
2. **No download/restore** - Coming in post-MVP
3. **No conflict resolution** - Coming in post-MVP
4. **No sync history** - Coming in post-MVP
5. **No scheduled syncs** - Manual only for now

## Getting Help

For issues or questions:
1. Check this user guide
2. See IMPLEMENTATION.md for technical details
3. See TODO-GOOGLE-DRIVE.md for cloud setup
4. Check the GitHub repository for updates

---

**Enjoy automatic game save backups with SaveSync!** 🎮☁️
