# Newt Service

Windows service wrapper for the [Newt VPN client](https://github.com/fosrl/newt).

## Download

Download the latest release from the [Releases page](https://github.com/memesalot/newt-service/releases).

- **NewtServiceSetup.msi** - Windows installer (recommended)
- **NewtService.Tray.exe** + **NewtService.Worker.exe** - Portable version

## Installation

### MSI Installer (Recommended)

1. Download and run `NewtServiceSetup.msi`
2. Enter your Pangolin connection details (Endpoint, ID, Secret)
3. Click **Save** - the service will auto-install and start

### Portable

1. Download both `.exe` files to the same folder
2. Run `NewtService.Tray.exe`
3. Right-click the tray icon → **Install Service**
4. Click **Config**, enter your details, and **Save**

## Features

- **Windows Service**: Runs Newt as a background service
- **System Tray**: Control the service from the taskbar
- **Auto-Update**: Notifies when new Newt versions are available
- **Auto-Download**: Downloads Newt client from GitHub on first run

## Tray Menu

| Option | Description |
|--------|-------------|
| Install/Uninstall Service | Manage Windows service |
| Check for Updates | Check for new Newt versions |
| Config | Open configuration window |
| Exit | Close tray application |

## Building

```powershell
# Build executables
dotnet publish NewtService.Worker -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=true -r win-x64
dotnet publish NewtService.Tray -c Release -o publish -p:PublishSingleFile=true -p:SelfContained=true -r win-x64

# Build MSI (requires WiX: dotnet tool install -g wix)
cd installer && wix build Package.wxs -o ..\NewtServiceSetup.msi
```

## License

MIT
