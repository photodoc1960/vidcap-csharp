# EEG Screen Capture - C# Edition

A professional screen capture application designed for EEG seizure detection monitoring. Built with C# and WPF for seamless integration with Windows medical workstations.

## Features

### Core Functionality
- **Visual Region Selector**: Interactive click-and-drag to select EEG display area
- **High-Quality Video Recording**: H.265/HEVC encoding with configurable quality (CRF 18-23)
- **Automatic 5-Minute Segmentation**: Seamless continuous 5-minute video segments
- **Background Conversion**: FFmpeg converts videos in background while recording continues
- **Timestamp Overlay**: Configurable timestamp display on recordings
- **Real-time Upload**: Automatic Google Cloud Storage upload for seizure detection analysis
- **Medical Workflow Optimized**: Simple WPF interface designed for busy EEG monitoring technicians

### Technical Features
- **Hybrid C#/FFmpeg Architecture**: Pure C# recording with FFmpeg post-processing for optimal compression
- **Background Processing**: Video conversion runs in parallel with recording
- **Accurate Frame Rate Tracking**: Automatically adjusts to system capabilities
- **Windows Native**: Built with WPF for optimal Windows performance
- **Reliable Recording**: No gaps between segments, continuous monitoring
- **Google Cloud Integration**: Automatic upload with retry logic
- **Configuration Management**: JSON-based configuration with GUI settings editor

## Installation

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK or Runtime
- FFmpeg (for H.265/MP4 conversion) - [Download FFmpeg](https://www.gyan.dev/ffmpeg/builds/)
- Visual Studio 2022 (for development) or just .NET Runtime (for running)
- Google Cloud Storage account with service account credentials (optional for cloud upload)

### Quick Start

#### Option 1: Build from Source

```powershell
# Clone the repository
cd G:\CODE\vidcap-csharp

# Build the project
dotnet build -c Release

# Run the application
dotnet run --project EegScreenCapture
```

#### Option 2: Build with Visual Studio

1. Open `EegScreenCapture.sln` in Visual Studio 2022
2. Set build configuration to **Release**
3. Build Solution (Ctrl+Shift+B)
4. Run (F5) or find executable in `bin/Release/net8.0-windows/`

### FFmpeg Setup (Required for H.265 Conversion)

#### Option 1: Place ffmpeg.exe with Application
1. Download FFmpeg from [gyan.dev/ffmpeg/builds](https://www.gyan.dev/ffmpeg/builds/)
2. Extract `ffmpeg-release-essentials.zip`
3. Copy `ffmpeg.exe` to the same folder as `EegScreenCapture.exe`

#### Option 2: Add FFmpeg to System PATH
1. Download and extract FFmpeg as above
2. Add the FFmpeg `bin` folder to your system PATH
3. Verify with: `ffmpeg -version`

**Note**: The application will record in MJPEG/AVI format if FFmpeg is not found, but H.265/MP4 conversion provides 8-15x smaller files.

### Configuration

The application creates a `config.json` file on first run:

```json
{
  "recording": {
    "fps": 30,
    "segmentDurationMinutes": 5,
    "videoFormat": "avi",
    "codec": "mjpeg",
    "outputDirectory": "./recordings",
    "ffmpeg": {
      "enabled": true,
      "ffmpegPath": "ffmpeg.exe",
      "crf": 20,
      "preset": "slow",
      "deleteIntermediateAvi": true
    }
  },
  "storage": {
    "googleCloudBucket": "your-eeg-bucket-name",
    "autoUpload": true,
    "deleteAfterUpload": false,
    "retryAttempts": 3
  },
  "ui": {
    "timestampOverlay": true,
    "timestampFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

## Usage for EEG Technicians

### Initial Setup
1. **Launch Application**: Double-click `EegScreenCapture.exe`
2. **Configure Capture Region**:
   - Click "🎯 Select Region Visually"
   - Click and drag to select EEG display area
   - Press ENTER to confirm (or ESC to cancel)

### Daily Recording Workflow
1. **Enter Patient ID**: Type patient identifier in the field
2. **Start Recording**: Click "🔴 Start Recording"
3. **Monitor Status**: Watch for segment completion notifications
4. **Stop When Done**: Click "⏹️ Stop Recording" when monitoring session ends

### Status Indicators
- **🔴 RECORDING**: Currently capturing EEG display
- **Segment Progress**: Shows current segment timing
- **Total Time**: Total recording duration
- **Message Log**: Displays all recording events

## File Organization

### Local Files
```
recordings/
├── EEG_PATIENT001_20241008_143022_seg001.mp4
├── EEG_PATIENT001_20241008_143022_seg002.mp4
├── EEG_PATIENT001_20241008_143022_seg003.mp4
└── ...
```

### Google Cloud Storage Structure
```
your-eeg-bucket/
├── EEG_PATIENT001_20241008_143022_seg001.mp4
├── EEG_PATIENT001_20241008_143022_seg002.mp4
└── ...
```

## Development

### Project Structure

```
EegScreenCapture/
├── Core/
│   ├── ScreenCapture.cs          # Screen capture functionality
│   └── ScreenRecorder.cs         # Recording loop with segmentation
├── VideoEncoder/
│   ├── AviWriter.cs              # Motion JPEG/AVI encoder
│   └── FFmpegConverter.cs        # H.265/MP4 conversion
├── Models/
│   └── Configuration.cs          # Configuration management
├── Cloud/
│   └── CloudUploader.cs          # Google Cloud Storage integration
├── UI/
│   ├── RegionSelectorWindow.xaml # Visual region selector
│   └── SettingsWindow.xaml       # Settings editor
├── Utils/
│   └── Logger.cs                 # Debug logging
├── MainWindow.xaml               # Main application window
└── App.xaml                      # Application entry point
```

### Key Technologies
- **WPF**: Windows Presentation Foundation for GUI
- **System.Drawing**: For screen capture and bitmap manipulation
- **Google.Cloud.Storage.V1**: Cloud storage integration
- **Newtonsoft.Json**: Configuration serialization

### Building

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained
```

## Configuration

### Google Cloud Setup

1. **Create Service Account**:
   - Go to Google Cloud Console
   - Create a service account
   - Grant "Storage Object Admin" role
   - Download JSON key file

2. **Set Environment Variable**:
   ```powershell
   $env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account.json"

   # Or set permanently
   [System.Environment]::SetEnvironmentVariable(
       "GOOGLE_APPLICATION_CREDENTIALS",
       "C:\path\to\service-account.json",
       [System.EnvironmentVariableTarget]::User
   )
   ```

## Troubleshooting

### Common Issues

**Application won't start:**
- Ensure .NET 8.0 Runtime is installed
- Check Windows Event Viewer for errors
- Run from command line to see console output

**Recording not starting:**
- Check capture region coordinates are within screen bounds
- Ensure patient ID is entered
- Verify recordings directory is writable

**Upload failures:**
- Check internet connection
- Verify Google Cloud credentials are correctly configured
- Ensure bucket name matches configuration
- Check GOOGLE_APPLICATION_CREDENTIALS environment variable

**Performance issues:**
- Reduce FPS in settings if needed
- Ensure sufficient disk space for local recordings
- Close unnecessary applications

## Video Format

### Output Format
- **Container**: MP4
- **Codec**: H.265/HEVC (libx265)
- **Quality**: CRF 20 (configurable 0-51, lower = better quality)
- **Encoding Preset**: Slow (configurable: ultrafast to veryslow)
- **File Size**: ~20-50 MB per 5-minute segment at 1080p (8-15x compression vs MJPEG)
- **Playback**: Compatible with VLC, modern media players, and web browsers

### Intermediate Format (Auto-deleted)
- **Container**: AVI
- **Codec**: Motion JPEG (MJPEG) at 60% quality
- **Purpose**: Fast real-time recording, converted to H.265 in background
- **Size**: ~200-400 MB per 5-minute segment (automatically deleted after conversion)

### FFmpeg Settings (Configurable in Settings UI)
- **CRF**: 18-23 recommended for screen recording (default: 20)
- **Preset**: Encoding speed vs compression tradeoff (default: "slow")
  - `ultrafast`, `superfast`, `veryfast` - Faster encoding, larger files
  - `fast`, `medium` - Balanced
  - `slow`, `slower`, `veryslow` - Better compression, slower encoding
- **Background Conversion**: Enabled by default, converts while recording continues

## License

[Your License Here]

## Support

For technical support or bug reports, please create an issue on the GitHub repository.

## Medical Use Disclaimer

This software is designed for EEG monitoring workflows but is not a medical device. Ensure compliance with your institution's medical software policies and patient data handling requirements.

---

**Last Updated**: 2025-10-08
**Version**: 1.0.0 (C# Edition)
**Framework**: .NET 8.0
