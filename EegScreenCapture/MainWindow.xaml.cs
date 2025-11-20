using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EegScreenCapture.Cloud;
using EegScreenCapture.Core;
using EegScreenCapture.Models;
using EegScreenCapture.UI;

namespace EegScreenCapture
{
    public partial class MainWindow : Window
    {
        private readonly Configuration _config;
        private readonly ScreenRecorder _recorder;
        private Rectangle? _captureRegion;
        private DateTime? _recordingStartTime;
        private DispatcherTimer? _uiUpdateTimer;
        private int _currentSegment = 1;
        private readonly ObservableCollection<SegmentStatusViewModel> _segmentStatuses;

        public MainWindow()
        {
            InitializeComponent();

            // Load configuration
            _config = Configuration.Load();
            _recorder = new ScreenRecorder(_config);
            _segmentStatuses = new ObservableCollection<SegmentStatusViewModel>();

            // Bind segment status list
            SegmentStatusList.ItemsSource = _segmentStatuses;

            // Subscribe to recorder events
            _recorder.SegmentCompleted += OnSegmentCompleted;
            _recorder.RecordingError += OnRecordingError;
            _recorder.StatusMessage += OnStatusMessage;
            _recorder.SeizureDetected += OnSeizureDetected;

            // Start a timer to update pending segment statuses
            var statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            statusUpdateTimer.Tick += UpdateSegmentStatuses;
            statusUpdateTimer.Start();

            AddLogMessage("Application started. Please configure capture region and enter patient ID.");
        }

        private void SelectRegionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selector = new RegionSelectorWindow();
                if (selector.ShowDialog() == true)
                {
                    _captureRegion = selector.SelectedRegion;
                    RegionTextBlock.Text = $"{_captureRegion.Value.Width}x{_captureRegion.Value.Height} at ({_captureRegion.Value.X}, {_captureRegion.Value.Y})";
                    RegionTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    AddLogMessage($"Capture region selected: {RegionTextBlock.Text}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting region: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_captureRegion == null)
            {
                MessageBox.Show("Please select a capture region first.", "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PatientIdTextBox.Text))
            {
                MessageBox.Show("Please enter a Patient ID.", "Patient ID Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PatientIdTextBox.IsEnabled = false;
            SelectRegionButton.IsEnabled = false;

            _recordingStartTime = DateTime.Now;
            _currentSegment = 1;

            // Start UI update timer
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _uiUpdateTimer.Tick += UpdateRecordingUI;
            _uiUpdateTimer.Start();

            // Capture patient ID on UI thread BEFORE starting background thread
            string patientId = PatientIdTextBox.Text;

            AddLogMessage($"Starting recording for patient: {patientId}");

            // Start recording on a dedicated thread (not Task.Run which uses thread pool)
            var recordingThread = new System.Threading.Thread(async () =>
            {
                try
                {
                    await _recorder.StartRecordingAsync(_captureRegion.Value, patientId);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Recording error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        ResetUI();
                    });
                }
            })
            {
                IsBackground = true,
                Name = "RecordingThread"
            };
            recordingThread.Start();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _recorder.StopRecording();
            _uiUpdateTimer?.Stop();
            ResetUI();
            AddLogMessage("Recording stopped by user");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_config);
            if (settingsWindow.ShowDialog() == true)
            {
                _config.Save();
                AddLogMessage("Settings saved");
            }
        }

        private void OnSegmentCompleted(object? sender, SegmentCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _currentSegment = e.SegmentNumber + 1;
                AddLogMessage($"Segment {e.SegmentNumber} completed: {e.FilePath}");
            });
        }

        private void OnRecordingError(object? sender, RecordingErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage($"ERROR: {e.ErrorMessage}");
                MessageBox.Show(e.ErrorMessage, "Recording Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUI();
            });
        }

        private void OnStatusMessage(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddLogMessage(message);
            });
        }

        private void UpdateRecordingUI(object? sender, EventArgs e)
        {
            if (_recordingStartTime == null) return;

            var elapsed = DateTime.Now - _recordingStartTime.Value;
            var segmentDuration = TimeSpan.FromMinutes(_config.Recording.SegmentDurationMinutes);
            var currentSegmentTime = TimeSpan.FromSeconds(elapsed.TotalSeconds % (segmentDuration.TotalSeconds));

            StatusTextBlock.Text = $"🔴 RECORDING - Segment {_currentSegment}";
            SegmentTextBlock.Text = $"Segment Progress: {currentSegmentTime:mm\\:ss}/{segmentDuration:mm\\:ss}";
            TimeTextBlock.Text = $"Total Time: {elapsed:hh\\:mm\\:ss}";
        }

        private void ResetUI()
        {
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PatientIdTextBox.IsEnabled = true;
            SelectRegionButton.IsEnabled = true;
            StatusTextBlock.Text = "Ready";
            SegmentTextBlock.Text = "";
            TimeTextBlock.Text = "";
            _recordingStartTime = null;
        }

        private void AddLogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            MessageLogTextBlock.Text += $"[{timestamp}] {message}\n";
        }

        private void OnSeizureDetected(object? sender, SeizureDetectionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Show alert message box
                var result = MessageBox.Show(
                    $"SEIZURE DETECTED!\n\nPatient: {e.PatientId}\nSegment: {e.SegmentNumber}\nFile: {e.FileName}\n\nPlease review the recording.",
                    "⚠️ SEIZURE ALERT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                AddLogMessage($"🚨 SEIZURE DETECTED - {e.PatientId} Segment {e.SegmentNumber}");
            });
        }

        private void UpdateSegmentStatuses(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Get current pending segments from recorder
                var pendingSegments = _recorder.PendingSegments.ToList();

                // Update existing statuses
                foreach (var segment in pendingSegments)
                {
                    var existing = _segmentStatuses.FirstOrDefault(s => s.FileName == segment.FileName);
                    if (existing != null)
                    {
                        existing.Result = segment.Result;
                    }
                    else
                    {
                        // Add new segment status
                        _segmentStatuses.Add(new SegmentStatusViewModel
                        {
                            SegmentNumber = segment.SegmentNumber,
                            FileName = segment.FileName,
                            Result = segment.Result,
                            PatientId = segment.PatientId
                        });
                    }
                }

                // Keep only last 20 segments for display
                while (_segmentStatuses.Count > 20)
                {
                    _segmentStatuses.RemoveAt(0);
                }
            });
        }
    }

    /// <summary>
    /// View model for displaying segment status in UI
    /// </summary>
    public class SegmentStatusViewModel : INotifyPropertyChanged
    {
        private int? _result;

        public int SegmentNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;

        public int? Result
        {
            get => _result;
            set
            {
                if (_result != value)
                {
                    _result = value;
                    OnPropertyChanged(nameof(Result));
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string SegmentLabel => $"Seg {SegmentNumber}:";

        public string Status
        {
            get
            {
                if (Result == null) return "⏳ Pending";
                return Result == 0 ? "✅ Normal" : "🚨 SEIZURE";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
