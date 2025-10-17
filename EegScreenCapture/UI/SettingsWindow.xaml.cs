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
