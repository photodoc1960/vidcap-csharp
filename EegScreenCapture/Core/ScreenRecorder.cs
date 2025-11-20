using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EegScreenCapture.Models;
using EegScreenCapture.VideoEncoder;
using EegScreenCapture.Utils;
using EegScreenCapture.Cloud;

namespace EegScreenCapture.Core
{
    /// <summary>
    /// Records screen segments with automatic 5-minute segmentation
    /// </summary>
    public class ScreenRecorder
    {
        private readonly Configuration _config;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRecording;
        private readonly List<Task> _conversionTasks = new List<Task>();
        private readonly List<PendingSegment> _pendingSegments = new List<PendingSegment>();
        private CloudUploader? _cloudUploader;

        public event EventHandler<SegmentCompletedEventArgs>? SegmentCompleted;
        public event EventHandler<RecordingErrorEventArgs>? RecordingError;
        public event EventHandler<string>? StatusMessage;
        public event EventHandler<SeizureDetectionEventArgs>? SeizureDetected;

        public bool IsRecording => _isRecording;
        public IReadOnlyList<PendingSegment> PendingSegments => _pendingSegments.AsReadOnly();

        public ScreenRecorder(Configuration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Start recording with automatic segmentation
        /// </summary>
        public async Task StartRecordingAsync(Rectangle captureRegion, string patientId)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording already in progress");

            _isRecording = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await RecordingLoopAsync(captureRegion, patientId, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, new RecordingErrorEventArgs(ex.Message));
            }
            finally
            {
                _isRecording = false;
            }
        }

        /// <summary>
        /// Stop the current recording
        /// </summary>
        public void StopRecording()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task RecordingLoopAsync(Rectangle captureRegion, string patientId, CancellationToken cancellationToken)
        {
            var segmentNumber = 1;
            var segmentDuration = TimeSpan.FromMinutes(_config.Recording.SegmentDurationMinutes);

            // Ensure output directory exists
            if (!Directory.Exists(_config.Recording.OutputDirectory))
            {
                Directory.CreateDirectory(_config.Recording.OutputDirectory);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var startTime = DateTime.Now;
                var fileName = GenerateFileName(patientId, segmentNumber, startTime);
                var filePath = Path.Combine(_config.Recording.OutputDirectory, fileName);

                StatusMessage?.Invoke(this, $"Recording segment {segmentNumber}...");

                try
                {
                    await RecordSegmentAsync(captureRegion, filePath, segmentDuration, cancellationToken);

                    // Convert to H.265/MP4 if FFmpeg is enabled (run in background)
                    if (_config.Recording.FFmpeg.Enabled)
                    {
                        var segNum = segmentNumber; // Capture for closure
                        var aviPath = filePath;     // Capture for closure

                        // Start conversion in background without blocking
                        var conversionTask = Task.Run(async () =>
                        {
                            try
                            {
                                var converter = new FFmpegConverter(
                                    _config.Recording.FFmpeg.FFmpegPath,
                                    _config.Recording.FFmpeg.Crf,
                                    _config.Recording.FFmpeg.Preset,
                                    _config.Recording.FFmpeg.DeleteIntermediateAvi);

                                // Check if FFmpeg is available
                                if (converter.IsFFmpegAvailable())
                                {
                                    StatusMessage?.Invoke(this, $"Converting segment {segNum} to H.265 (background)...");
                                    await converter.ConvertToH265Async(aviPath);
                                    StatusMessage?.Invoke(this, $"Segment {segNum} converted successfully");
                                }
                                else
                                {
                                    Logger.Log("WARNING: FFmpeg not found. Skipping H.265 conversion.");
                                    Logger.Log($"  Please install FFmpeg and ensure '{_config.Recording.FFmpeg.FFmpegPath}' is in PATH");
                                    StatusMessage?.Invoke(this, "FFmpeg not found - saved as MJPEG/AVI");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"FFmpeg conversion failed for segment {segNum}", ex);
                                StatusMessage?.Invoke(this, $"Segment {segNum} conversion failed - saved as MJPEG/AVI");
                            }
                        });

                        _conversionTasks.Add(conversionTask);
                    }

                    // Notify segment completion (will be converted in background if enabled)
                    SegmentCompleted?.Invoke(this, new SegmentCompletedEventArgs
                    {
                        SegmentNumber = segmentNumber,
                        FilePath = filePath,  // AVI path initially, will be converted to MP4 in background
                        PatientId = patientId,
                        StartTime = startTime
                    });

                    // Upload to cloud and start polling for seizure detection (in background)
                    var currentSegNum = segmentNumber;
                    _ = Task.Run(async () => await UploadAndPollForResultAsync(filePath, currentSegNum, patientId));

                    segmentNumber++;
                }
                catch (OperationCanceledException)
                {
                    // Recording stopped by user
                    break;
                }
                catch (Exception ex)
                {
                    RecordingError?.Invoke(this, new RecordingErrorEventArgs($"Segment {segmentNumber} failed: {ex.Message}"));
                    break;
                }
            }

            // Wait for all background conversions to complete
            if (_conversionTasks.Count > 0)
            {
                StatusMessage?.Invoke(this, $"Recording stopped - waiting for {_conversionTasks.Count} conversion(s) to complete...");
                Logger.Log($"Waiting for {_conversionTasks.Count} background conversion tasks to complete...");
                await Task.WhenAll(_conversionTasks);
                Logger.Log("All background conversions completed");
                _conversionTasks.Clear();
            }

            StatusMessage?.Invoke(this, "Recording stopped");
        }

        private async Task RecordSegmentAsync(Rectangle captureRegion, string filePath, TimeSpan duration, CancellationToken cancellationToken)
        {
            AviWriter? aviWriter = null;
            var frameCount = 0;
            var startTime = DateTime.Now;

            try
            {
                Logger.Log($"Starting segment recording to: {filePath}");
                aviWriter = new AviWriter(filePath, captureRegion.Width, captureRegion.Height, _config.Recording.Fps);

                var frameDuration = TimeSpan.FromMilliseconds(1000.0 / _config.Recording.Fps);

                while (DateTime.Now - startTime < duration && !cancellationToken.IsCancellationRequested)
                {
                    var frameStart = DateTime.Now;

                    try
                    {
                        Bitmap? frame = null;
                        try
                        {
                            // Capture frame (this creates a new bitmap, safe for threading)
                            frame = ScreenCapture.CaptureRegion(captureRegion);

                            // Add timestamp overlay if enabled
                            if (_config.Ui.TimestampOverlay)
                            {
                                AddTimestampOverlay(frame, DateTime.Now);
                            }

                            // Add frame to video
                            aviWriter.AddFrame(frame);
                            frameCount++;
                        }
                        finally
                        {
                            frame?.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to capture frame {frameCount}: {ex.Message}");
                    }

                    // Maintain target FPS
                    var elapsed = DateTime.Now - frameStart;
                    var delay = frameDuration - elapsed;
                    if (delay > TimeSpan.Zero)
                    {
                        try
                        {
                            await Task.Delay(delay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // Recording stopped by user - this is expected
                            Logger.Log("Recording cancelled by user");
                            break;
                        }
                    }
                }

                var actualDuration = DateTime.Now - startTime;
                Logger.Log($"Recording loop completed. Captured {frameCount} frames in {actualDuration.TotalSeconds:F2}s. Finalizing...");
            }
            catch (Exception ex)
            {
                Logger.LogError($"ERROR in RecordSegmentAsync", ex);
                throw;
            }
            finally
            {
                // ALWAYS finalize the video file, even if cancelled
                if (aviWriter != null)
                {
                    try
                    {
                        var actualDuration = DateTime.Now - startTime;
                        Logger.Log($"Calling FinalizeVideo() for {frameCount} frames over {actualDuration.TotalSeconds:F2}s...");
                        aviWriter.FinalizeVideo(actualDuration);
                        aviWriter.Dispose();
                        Logger.Log($"Segment completed: {frameCount} frames written to {filePath}");
                        var fileInfo = new System.IO.FileInfo(filePath);
                        Logger.Log($"File exists: {fileInfo.Exists}, Size: {fileInfo.Length / (1024 * 1024)} MB");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"ERROR during finalization", ex);
                        throw;
                    }
                }
            }
        }

        private void AddTimestampOverlay(Bitmap frame, DateTime timestamp)
        {
            using var graphics = Graphics.FromImage(frame);
            var timestampText = timestamp.ToString(_config.Ui.TimestampFormat);

            // Create semi-transparent background
            using var font = new Font("Arial", 12, FontStyle.Bold);
            var textSize = graphics.MeasureString(timestampText, font);
            var bgRect = new RectangleF(
                frame.Width - textSize.Width - 10,
                frame.Height - textSize.Height - 10,
                textSize.Width + 6,
                textSize.Height + 4
            );

            using var bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0));
            graphics.FillRectangle(bgBrush, bgRect);

            // Draw timestamp text
            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString(timestampText, font, textBrush, bgRect.X + 3, bgRect.Y + 2);
        }

        private string GenerateFileName(string patientId, int segmentNumber, DateTime startTime)
        {
            return $"EEG_{patientId}_{startTime:yyyyMMdd_HHmmss}_seg{segmentNumber:D3}.avi";
        }

        /// <summary>
        /// Upload segment to cloud and poll for seizure detection result
        /// </summary>
        private async Task UploadAndPollForResultAsync(string aviFilePath, int segmentNumber, string patientId)
        {
            try
            {
                // Wait for FFmpeg conversion to complete (if enabled)
                string finalFilePath = aviFilePath;
                if (_config.Recording.FFmpeg.Enabled)
                {
                    // Convert .avi extension to .mp4 for the final file path
                    finalFilePath = Path.ChangeExtension(aviFilePath, ".mp4");

                    // Wait up to 30 minutes for conversion to complete
                    var waitTime = TimeSpan.Zero;
                    var maxWait = TimeSpan.FromMinutes(30);
                    while (!File.Exists(finalFilePath) && waitTime < maxWait)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        waitTime += TimeSpan.FromSeconds(10);
                    }

                    if (!File.Exists(finalFilePath))
                    {
                        Logger.Log($"FFmpeg conversion did not complete for {aviFilePath}, uploading AVI instead");
                        finalFilePath = aviFilePath;
                    }
                }

                // Create pending segment tracker
                var segment = new PendingSegment(finalFilePath, segmentNumber, patientId);
                lock (_pendingSegments)
                {
                    _pendingSegments.Add(segment);
                }

                // Initialize cloud uploader if needed
                if (_cloudUploader == null)
                {
                    _cloudUploader = new CloudUploader(_config);
                    _cloudUploader.SeizureDetected += (sender, e) =>
                    {
                        StatusMessage?.Invoke(this, $"🚨 SEIZURE DETECTED - {e.PatientId} Segment {e.SegmentNumber}");
                        SeizureDetected?.Invoke(this, e);
                    };
                }

                // Upload the file
                Logger.Log($"Uploading {segment.FileName} to cloud...");
                StatusMessage?.Invoke(this, $"Uploading segment {segmentNumber}...");

                var uploaded = await _cloudUploader.UploadWithRetryAsync(finalFilePath);

                if (uploaded)
                {
                    Logger.Log($"Upload successful: {segment.FileName}");
                    StatusMessage?.Invoke(this, $"Segment {segmentNumber} uploaded - polling for results...");

                    // Start polling for results (2 minute intervals, 30 minute timeout)
                    var result = await _cloudUploader.PollForResultAsync(
                        segment,
                        timeout: TimeSpan.FromMinutes(30),
                        pollInterval: TimeSpan.FromMinutes(2)
                    );

                    // Update segment with result
                    lock (_pendingSegments)
                    {
                        segment.Result = result;
                    }

                    if (result.HasValue)
                    {
                        var status = result.Value == 0 ? "✅ Normal" : "🚨 SEIZURE DETECTED";
                        StatusMessage?.Invoke(this, $"Segment {segmentNumber}: {status}");
                        Logger.Log($"Result for {segment.FileName}: {status}");
                    }
                    else
                    {
                        StatusMessage?.Invoke(this, $"Segment {segmentNumber}: No result (timeout)");
                        Logger.Log($"No result received for {segment.FileName} after polling timeout");
                    }
                }
                else
                {
                    Logger.Log($"Upload failed for {segment.FileName}");
                    StatusMessage?.Invoke(this, $"Segment {segmentNumber}: Upload failed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in upload/poll for segment {segmentNumber}", ex);
                StatusMessage?.Invoke(this, $"Segment {segmentNumber}: Error - {ex.Message}");
            }
        }
    }

    public class SegmentCompletedEventArgs : EventArgs
    {
        public int SegmentNumber { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    public class RecordingErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; }

        public RecordingErrorEventArgs(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
