using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EegScreenCapture.Utils;

namespace EegScreenCapture.VideoEncoder
{
    /// <summary>
    /// Converts AVI/MJPEG files to H.265/MP4 using FFmpeg
    /// </summary>
    public class FFmpegConverter
    {
        private readonly string _ffmpegPath;
        private readonly int _crf;
        private readonly string _preset;
        private readonly bool _deleteSourceAfterConversion;

        public FFmpegConverter(
            string ffmpegPath = "ffmpeg.exe",
            int crf = 20,
            string preset = "slow",
            bool deleteSourceAfterConversion = true)
        {
            _ffmpegPath = ffmpegPath;
            _crf = crf;
            _preset = preset;
            _deleteSourceAfterConversion = deleteSourceAfterConversion;
        }

        /// <summary>
        /// Check if FFmpeg is available
        /// </summary>
        public bool IsFFmpegAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Convert AVI/MJPEG to H.265/MP4
        /// </summary>
        public async Task<string> ConvertToH265Async(string inputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input file not found: {inputPath}");

            // Generate output filename (replace .avi with .mp4)
            var outputPath = Path.ChangeExtension(inputPath, ".mp4");

            Logger.Log($"Starting FFmpeg conversion: {inputPath} -> {outputPath}");
            Logger.Log($"FFmpeg settings: CRF={_crf}, Preset={_preset}");

            try
            {
                // Build FFmpeg command
                // -i input.avi: Input file
                // -vf scale: Ensure even dimensions (H.265 requirement)
                // -c:v libx265: Use H.265/HEVC codec
                // -crf 20: Constant Rate Factor (18-23 for screen recording)
                // -preset slow: Encoding speed/quality tradeoff
                // -pix_fmt yuv420p: Pixel format for compatibility
                // -y: Overwrite output file
                var arguments = $"-i \"{inputPath}\" " +
                               $"-vf \"scale='if(mod(iw,2),iw+1,iw)':'if(mod(ih,2),ih+1,ih)'\" " +
                               $"-c:v libx265 " +
                               $"-crf {_crf} " +
                               $"-preset {_preset} " +
                               $"-pix_fmt yuv420p " +
                               $"-y \"{outputPath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };

                // Capture output for logging
                var outputData = string.Empty;
                var errorData = string.Empty;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputData += e.Data + "\n";
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorData += e.Data + "\n";
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for completion (use WaitForExitAsync in .NET 5+)
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Logger.Log($"FFmpeg conversion failed with exit code {process.ExitCode}");
                    Logger.Log($"FFmpeg stderr: {errorData}");
                    throw new Exception($"FFmpeg conversion failed: {errorData}");
                }

                // Verify output file was created
                if (!File.Exists(outputPath))
                {
                    throw new Exception("FFmpeg completed but output file was not created");
                }

                var inputSize = new FileInfo(inputPath).Length / (1024 * 1024);
                var outputSize = new FileInfo(outputPath).Length / (1024 * 1024);
                var compressionRatio = (double)inputSize / outputSize;

                Logger.Log($"FFmpeg conversion completed successfully");
                Logger.Log($"  Input:  {inputSize} MB (MJPEG/AVI)");
                Logger.Log($"  Output: {outputSize} MB (H.265/MP4)");
                Logger.Log($"  Compression ratio: {compressionRatio:F2}x");

                // Delete source file if configured
                if (_deleteSourceAfterConversion)
                {
                    try
                    {
                        File.Delete(inputPath);
                        Logger.Log($"Deleted source file: {inputPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Warning: Could not delete source file: {ex.Message}");
                    }
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                Logger.LogError($"FFmpeg conversion error", ex);
                throw;
            }
        }

        /// <summary>
        /// Get estimated output size (rough estimate)
        /// </summary>
        public long EstimateOutputSize(string inputPath)
        {
            if (!File.Exists(inputPath))
                return 0;

            var inputSize = new FileInfo(inputPath).Length;
            // H.265 typically achieves 5-10x compression over MJPEG
            // Using conservative 5x estimate
            return inputSize / 5;
        }
    }
}
