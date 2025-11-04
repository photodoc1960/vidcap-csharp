using Newtonsoft.Json;
using System;
using System.IO;

namespace EegScreenCapture.Models
{
    public class Configuration
    {
        public RecordingConfig Recording { get; set; } = new();
        public StorageConfig Storage { get; set; } = new();
        public UiConfig Ui { get; set; } = new();

        public static Configuration Load(string configPath = "config.json")
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<Configuration>(json) ?? CreateDefault();
            }

            var defaultConfig = CreateDefault();
            defaultConfig.Save(configPath);
            return defaultConfig;
        }

        public void Save(string configPath = "config.json")
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }

        private static Configuration CreateDefault()
        {
            return new Configuration
            {
                Recording = new RecordingConfig
                {
                    Fps = 30,
                    SegmentDurationMinutes = 5,
                    VideoFormat = "avi",
                    Codec = "mjpeg",
                    OutputDirectory = "./recordings"
                },
                Storage = new StorageConfig
                {
                    GoogleCloudBucket = "eeg-seizure-detection",
                    AutoUpload = true,
                    DeleteAfterUpload = false,
                    RetryAttempts = 3
                },
                Ui = new UiConfig
                {
                    TimestampOverlay = true,
                    TimestampFormat = "yyyy-MM-dd HH:mm:ss"
                }
            };
        }
    }

    public class RecordingConfig
    {
        public int Fps { get; set; }
        public int SegmentDurationMinutes { get; set; }
        public string VideoFormat { get; set; } = "avi";
        public string Codec { get; set; } = "mjpeg";
        public string OutputDirectory { get; set; } = "./recordings";
        public FFmpegConfig FFmpeg { get; set; } = new();
    }

    public class FFmpegConfig
    {
        public bool Enabled { get; set; } = true;
        public string FFmpegPath { get; set; } = "ffmpeg.exe";
        public int Crf { get; set; } = 20; // 18-23 recommended for screen recording
        public string Preset { get; set; } = "slow"; // slow, slower, or medium
        public bool DeleteIntermediateAvi { get; set; } = true;
    }

    public class StorageConfig
    {
        public string GoogleCloudBucket { get; set; } = string.Empty;
        public bool AutoUpload { get; set; }
        public bool DeleteAfterUpload { get; set; }
        public int RetryAttempts { get; set; }
    }

    public class UiConfig
    {
        public bool TimestampOverlay { get; set; }
        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    }
}
