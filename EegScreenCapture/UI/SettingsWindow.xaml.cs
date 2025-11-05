using System.Windows;
using EegScreenCapture.Models;

namespace EegScreenCapture.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly Configuration _config;

        public SettingsWindow(Configuration config)
        {
            InitializeComponent();
            _config = config;
            LoadSettings();
        }

        private void LoadSettings()
        {
            FpsTextBox.Text = _config.Recording.Fps.ToString();
            SegmentDurationTextBox.Text = _config.Recording.SegmentDurationMinutes.ToString();
            OutputDirectoryTextBox.Text = _config.Recording.OutputDirectory;
            TimestampOverlayCheckBox.IsChecked = _config.Ui.TimestampOverlay;

            FFmpegEnabledCheckBox.IsChecked = _config.Recording.FFmpeg.Enabled;
            FFmpegPathTextBox.Text = _config.Recording.FFmpeg.FFmpegPath;
            CrfTextBox.Text = _config.Recording.FFmpeg.Crf.ToString();

            // Set preset in combo box
            foreach (System.Windows.Controls.ComboBoxItem item in PresetComboBox.Items)
            {
                if (item.Content.ToString() == _config.Recording.FFmpeg.Preset)
                {
                    PresetComboBox.SelectedItem = item;
                    break;
                }
            }

            DeleteIntermediateAviCheckBox.IsChecked = _config.Recording.FFmpeg.DeleteIntermediateAvi;

            BucketNameTextBox.Text = _config.Storage.GoogleCloudBucket;
            AutoUploadCheckBox.IsChecked = _config.Storage.AutoUpload;
            DeleteAfterUploadCheckBox.IsChecked = _config.Storage.DeleteAfterUpload;
            RetryAttemptsTextBox.Text = _config.Storage.RetryAttempts.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(FpsTextBox.Text, out int fps) && fps > 0 && fps <= 60)
            {
                _config.Recording.Fps = fps;
            }

            if (int.TryParse(SegmentDurationTextBox.Text, out int duration) && duration > 0)
            {
                _config.Recording.SegmentDurationMinutes = duration;
            }

            _config.Recording.OutputDirectory = OutputDirectoryTextBox.Text;
            _config.Ui.TimestampOverlay = TimestampOverlayCheckBox.IsChecked == true;

            _config.Recording.FFmpeg.Enabled = FFmpegEnabledCheckBox.IsChecked == true;
            _config.Recording.FFmpeg.FFmpegPath = FFmpegPathTextBox.Text;

            if (int.TryParse(CrfTextBox.Text, out int crf) && crf >= 0 && crf <= 51)
            {
                _config.Recording.FFmpeg.Crf = crf;
            }

            if (PresetComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedPreset)
            {
                _config.Recording.FFmpeg.Preset = selectedPreset.Content.ToString() ?? "slow";
            }

            _config.Recording.FFmpeg.DeleteIntermediateAvi = DeleteIntermediateAviCheckBox.IsChecked == true;

            _config.Storage.GoogleCloudBucket = BucketNameTextBox.Text;
            _config.Storage.AutoUpload = AutoUploadCheckBox.IsChecked == true;
            _config.Storage.DeleteAfterUpload = DeleteAfterUploadCheckBox.IsChecked == true;

            if (int.TryParse(RetryAttemptsTextBox.Text, out int retries) && retries >= 0)
            {
                _config.Storage.RetryAttempts = retries;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
