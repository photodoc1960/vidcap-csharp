using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using EegScreenCapture.Models;

namespace EegScreenCapture.Cloud
{
    /// <summary>
    /// Uploads video files to Google Cloud Storage
    /// </summary>
    public class CloudUploader
    {
        private readonly Configuration _config;
        private StorageClient? _storageClient;

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
}
