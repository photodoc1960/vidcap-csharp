# FFmpeg Setup Guide

The EEG Screen Capture application uses FFmpeg to convert MJPEG/AVI files to high-quality H.265/MP4 format.

## Quick Setup (Windows)

### Option 1: Download Portable FFmpeg (Recommended)

1. **Download FFmpeg**:
   - Go to: https://www.gyan.dev/ffmpeg/builds/
   - Download: `ffmpeg-release-essentials.zip` (latest version)
   - Extract to a folder (e.g., `C:\ffmpeg`)

2. **Place ffmpeg.exe with the application**:
   ```powershell
   # Copy ffmpeg.exe to the same folder as EegScreenCapture.exe
   Copy-Item "C:\ffmpeg\bin\ffmpeg.exe" ".\EegScreenCapture\bin\Release\net8.0-windows\"
   ```

3. **Done!** The application will automatically find ffmpeg.exe

### Option 2: Add FFmpeg to System PATH

1. **Download FFmpeg** (same as above)

2. **Add to PATH**:
   ```powershell
   # Add FFmpeg to system PATH
   $ffmpegPath = "C:\ffmpeg\bin"
   [Environment]::SetEnvironmentVariable(
       "Path",
       [Environment]::GetEnvironmentVariable("Path", "User") + ";$ffmpegPath",
       "User"
   )
   ```

3. **Verify installation**:
   ```powershell
   ffmpeg -version
   ```

4. **Restart the application** if it was running

## Configuration

The application will automatically use FFmpeg if available. You can configure settings in `config.json`:

```json
{
  "recording": {
    "ffmpeg": {
      "enabled": true,
      "ffmpegPath": "ffmpeg.exe",
      "crf": 20,
      "preset": "slow",
      "deleteIntermediateAvi": true
    }
  }
}
```

### Settings Explained

- **enabled**: `true` to use H.265 conversion, `false` to keep MJPEG/AVI
- **ffmpegPath**: Path to ffmpeg.exe (`"ffmpeg.exe"` if in PATH, or full path like `"C:\\ffmpeg\\bin\\ffmpeg.exe"`)
- **crf**: Quality setting (18-23 recommended)
  - Lower = better quality, larger files
  - 18 = visually lossless
  - 20 = excellent quality (recommended)
  - 23 = good quality
- **preset**: Encoding speed
  - `"slow"` = good compression, moderate speed (recommended)
  - `"slower"` = better compression, slower
  - `"medium"` = faster, slightly larger files
- **deleteIntermediateAvi**: Delete the MJPEG/AVI file after conversion to save disk space

## How It Works

1. **Recording**: Application captures screen at 30 FPS to MJPEG/AVI
   - Reliable, no encoding failures
   - ~200 MB per minute

2. **Conversion**: After each segment completes, FFmpeg converts to H.265/MP4
   - High quality, small file size
   - ~20-40 MB per minute (5-10x compression)
   - Preserves full resolution

3. **Result**: Final H.265/MP4 files ready for cloud upload

## Troubleshooting

### FFmpeg Not Found

**Problem**: Application shows "FFmpeg not found - saved as MJPEG/AVI"

**Solutions**:
1. Verify ffmpeg.exe is in the same folder as EegScreenCapture.exe, OR
2. Verify FFmpeg is in system PATH (`ffmpeg -version` works), OR
3. Set full path in config.json: `"ffmpegPath": "C:\\ffmpeg\\bin\\ffmpeg.exe"`

### Conversion Fails

**Problem**: "Conversion failed - saved as MJPEG/AVI"

**Check**:
1. View `eeg-capture-debug.log` for error details
2. Ensure sufficient disk space (need 2x segment size during conversion)
3. Check FFmpeg version (5.0 or newer recommended)

### Files Too Large

**Adjust CRF**:
- Change `"crf": 20` to `"crf": 23` for smaller files
- Change to `"crf": 18` for larger, higher quality files

### Conversion Too Slow

**Adjust Preset**:
- Change `"preset": "slow"` to `"preset": "medium"`
- Faster encoding, slightly larger files

## File Size Comparison

Example for 5-minute segment at 1920x1080:

| Format | Size | Quality | Notes |
|--------|------|---------|-------|
| MJPEG/AVI (60%) | ~200 MB | Good | Intermediate format |
| H.265/MP4 (CRF 18) | ~30 MB | Excellent | Visually lossless |
| H.265/MP4 (CRF 20) | ~25 MB | Excellent | **Recommended** |
| H.265/MP4 (CRF 23) | ~20 MB | Good | More compression |

## Verification

To verify FFmpeg is working:

1. **Start recording** for 1 minute
2. **Stop recording**
3. **Check files**:
   ```powershell
   # Should see .mp4 files (not .avi)
   Get-ChildItem .\recordings\*.mp4

   # Check file size (should be small)
   Get-ChildItem .\recordings\*.mp4 | Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length/1MB, 2)}}
   ```

4. **Check log**:
   ```powershell
   Get-Content .\EegScreenCapture\bin\Release\net8.0-windows\eeg-capture-debug.log -Tail 20
   ```

You should see messages like:
```
FFmpeg conversion completed successfully
  Input:  200 MB (MJPEG/AVI)
  Output: 25 MB (H.265/MP4)
  Compression ratio: 8.00x
```

## Advanced: Custom FFmpeg Location

If FFmpeg is installed elsewhere:

```json
{
  "recording": {
    "ffmpeg": {
      "enabled": true,
      "ffmpegPath": "D:\\Tools\\ffmpeg\\bin\\ffmpeg.exe"
    }
  }
}
```

**Note**: Use double backslashes (`\\`) in JSON paths!

---

**Still having issues?** Check `eeg-capture-debug.log` for detailed error messages.
