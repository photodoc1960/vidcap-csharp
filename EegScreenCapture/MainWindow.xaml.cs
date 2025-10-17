using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

        public MainWindow()
        {
            InitializeComponent();

            // Load configuration
            _config = Configuration.Load();
            _recorder = new ScreenRecorder(_config);

            // Subscribe to recorder events
            _recorder.SegmentCompleted += OnSegmentCompleted;
            _recorder.RecordingError += OnRecordingError;
            _recorder.StatusMessage += OnStatusMessage;

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
    }
}
