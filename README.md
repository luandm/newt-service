# Newt Service

Windows service wrapper for the [Newt VPN client](https://github.com/fosrl/newt).

## Features

- **Windows Service**: Runs Newt as a background Windows service
- **Modern UI**: Built with [Avalonia UI](https://github.com/AvaloniaUI/Avalonia)
- **System Tray**: Native tray icon with context menu
- **Auto-Download**: Automatically downloads Newt from GitHub on first run

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Usage

1. Build and run:
   ```powershell
   dotnet build
   dotnet run --project NewtService.Tray
   ```

2. Right-click the tray icon → **Install Service**

3. Click **Config** and enter your Pangolin connection details:
   - Endpoint
   - ID
   - Secret

4. Click **Save**, then start the service

## Tray Menu

| Option | Description |
|--------|-------------|
| Install Service | Install as Windows service (requires admin) |
| Uninstall Service | Remove the Windows service |
| Check for Updates | Check GitHub for new Newt versions |
| Update Now | Download and install update (appears when available) |
| Config | Open the configuration dashboard |
| Exit | Close the tray application |

## File Locations

| Item | Path |
|------|------|
| Newt executable | `%ProgramData%\NewtService\newt.exe` |
| Configuration | `%ProgramData%\NewtService\config.json` |
| Logs | `%ProgramData%\NewtService\logs\` |

## How It Works

The service runs `newt.exe` with your configured credentials:
```
newt.exe --id <your-id> --secret <your-secret> --endpoint <your-endpoint>
```

## License

MIT
