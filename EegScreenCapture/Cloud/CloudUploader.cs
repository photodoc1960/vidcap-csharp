using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;
using EegScreenCapture.Models;
using EegScreenCapture.Utils;

namespace EegScreenCapture.Cloud
{
    /// <summary>
    /// Uploads video files to Google Cloud Storage and checks for seizure detection results
    /// </summary>
    public class CloudUploader
    {
        private readonly Configuration _config;
        private StorageClient? _storageClient;

        public event EventHandler<SeizureDetectionEventArgs>? SeizureDetected;

        public CloudUploader(Configuration config)
        {
            _config = config;
        }

        /// <summary>
        /// Upload a file to Google Cloud Storage
        /// </summary>
        public async Task<bool> UploadFileAsync(string filePath)
        {
            if (!_config.Storage.AutoUpload)
                return false;

            try
            {
                EnsureStorageClient();

                var fileName = Path.GetFileName(filePath);
                using var fileStream = File.OpenRead(filePath);

                await _storageClient!.UploadObjectAsync(
                    _config.Storage.GoogleCloudBucket,
                    fileName,
                    "video/x-msvideo",
                    fileStream
                );

                Console.WriteLine($"Uploaded: {fileName} to {_config.Storage.GoogleCloudBucket}");

                // Delete local file if configured
                if (_config.Storage.DeleteAfterUpload)
                {
                    File.Delete(filePath);
                    Console.WriteLine($"Deleted local file: {filePath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upload failed for {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Upload with retry logic
        /// </summary>
        public async Task<bool> UploadWithRetryAsync(string filePath)
        {
            var attempts = 0;
            var maxAttempts = _config.Storage.RetryAttempts;

            while (attempts < maxAttempts)
            {
                attempts++;

                var success = await UploadFileAsync(filePath);
                if (success)
                    return true;

                if (attempts < maxAttempts)
                {
                    Console.WriteLine($"Retrying upload ({attempts}/{maxAttempts})...");
                    await Task.Delay(TimeSpan.FromSeconds(5 * attempts)); // Exponential backoff
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a result file exists for a given segment
        /// </summary>
        public async Task<SeizureResult?> CheckForResultAsync(string resultFileName)
        {
            if (!_config.Storage.AutoUpload)
                return null;

            try
            {
                EnsureStorageClient();

                // Try to download the result file
                using var memoryStream = new MemoryStream();
                await _storageClient!.DownloadObjectAsync(
                    _config.Storage.GoogleCloudBucket,
                    resultFileName,
                    memoryStream
                );

                // Parse JSON result
                memoryStream.Position = 0;
                using var reader = new StreamReader(memoryStream);
                var jsonContent = await reader.ReadToEndAsync();
                var result = JsonConvert.DeserializeObject<SeizureResult>(jsonContent);

                Logger.Log($"Seizure detection result received for {resultFileName}: {result?.SeizureDetected}");
                return result;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Result file doesn't exist yet - this is normal
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking for result {resultFileName}", ex);
                return null;
            }
        }

        /// <summary>
        /// Poll for seizure detection result with timeout
        /// </summary>
        public async Task<int?> PollForResultAsync(PendingSegment segment, TimeSpan timeout, TimeSpan pollInterval)
        {
            var startTime = DateTime.Now;

            Logger.Log($"Starting to poll for result: {segment.ResultFileName}");

            while (DateTime.Now - startTime < timeout)
            {
                var result = await CheckForResultAsync(segment.ResultFileName);

                if (result != null)
                {
                    Logger.Log($"Result found for {segment.FileName}: {result.SeizureDetected}");

                    // Raise event if seizure detected
                    if (result.SeizureDetected == 1)
                    {
                        SeizureDetected?.Invoke(this, new SeizureDetectionEventArgs
                        {
                            FileName = segment.FileName,
                            PatientId = segment.PatientId,
                            SegmentNumber = segment.SegmentNumber,
                            DetectedAt = result.ProcessedAt
                        });
                    }

                    return result.SeizureDetected;
                }

                // Wait before next poll
                await Task.Delay(pollInterval);
            }

            Logger.Log($"Polling timeout for {segment.ResultFileName} - no result received after {timeout.TotalMinutes} minutes");
            return null;
        }

        private void EnsureStorageClient()
        {
            if (_storageClient == null)
            {
                // Initialize Google Cloud Storage client
                // Requires GOOGLE_APPLICATION_CREDENTIALS environment variable
                _storageClient = StorageClient.Create();
            }
        }
    }

    /// <summary>
    /// Event args for seizure detection notifications
    /// </summary>
    public class SeizureDetectionEventArgs : EventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public int SegmentNumber { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
