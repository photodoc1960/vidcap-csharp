using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EegScreenCapture.Models;
using EegScreenCapture.VideoEncoder;

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

        public event EventHandler<SegmentCompletedEventArgs>? SegmentCompleted;
        public event EventHandler<RecordingErrorEventArgs>? RecordingError;
        public event EventHandler<string>? StatusMessage;

        public bool IsRecording => _isRecording;

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

                    // Notify segment completion
                    SegmentCompleted?.Invoke(this, new SegmentCompletedEventArgs
                    {
                        SegmentNumber = segmentNumber,
                        FilePath = filePath,
                        PatientId = patientId,
                        StartTime = startTime
                    });

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

            StatusMessage?.Invoke(this, "Recording stopped");
        }

        private async Task RecordSegmentAsync(Rectangle captureRegion, string filePath, TimeSpan duration, CancellationToken cancellationToken)
        {
            using var aviWriter = new AviWriter(filePath, captureRegion.Width, captureRegion.Height, _config.Recording.Fps);

            var startTime = DateTime.Now;
            var frameDuration = TimeSpan.FromMilliseconds(1000.0 / _config.Recording.Fps);
            var frameCount = 0;

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
                    Console.WriteLine($"Failed to capture frame {frameCount}: {ex.Message}");
                }

                // Maintain target FPS
                var elapsed = DateTime.Now - frameStart;
                var delay = frameDuration - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            // Finalize the video file
            aviWriter.FinalizeVideo();

            Console.WriteLine($"Segment completed: {frameCount} frames written to {filePath}");
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
