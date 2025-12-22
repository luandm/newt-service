<img src="logo.png" width="64" height="64" align="left" style="margin-right: 15px;">

# Newt Service

Windows service wrapper for the [Newt VPN client](https://github.com/fosrl/newt).

<br clear="left">

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

- **Windows Service**: Runs Newt as a background service that starts with Windows
- **System Tray**: Control the service from the taskbar
- **Auto-Update Notifications**: Checks daily at midnight for new Newt and app versions
- **Auto-Download**: Downloads Newt client from GitHub on first run
- **Logging**: Newt output logged to file (max 100MB, auto-truncated)

## Tray Menu

| Option | Description |
|--------|-------------|
| Install/Uninstall Service | Manage Windows service registration |
| Check for Newt Update | Check for new Newt client versions |
| Check for App Update | Check for new NewtService versions |
| Config | Open configuration window |
| Exit | Close tray application |

## Config Window

- **Endpoint, ID, Secret**: Your Pangolin connection credentials
- **Check Service**: Verify service is installed and configured correctly
- **Check for Update**: Check/install Newt client updates
- **Check App Update**: Check/install NewtService updates
- **Open Logs**: View newt.exe output log

## Logs

Newt output is logged to:
```
C:\ProgramData\NewtService\logs\newt.log
```

The log file is automatically truncated when it reaches 100MB.

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
