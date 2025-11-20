using System;
using System.IO;

namespace EegScreenCapture.Models
{
    /// <summary>
    /// Represents the result of cloud-based seizure detection analysis
    /// </summary>
    public class SeizureResult
    {
        /// <summary>
        /// 0 = Normal, 1 = Seizure detected
        /// </summary>
        public int SeizureDetected { get; set; }

        /// <summary>
        /// Timestamp when the analysis was processed
        /// </summary>
        public DateTime ProcessedAt { get; set; }
    }

    /// <summary>
    /// Tracks a video segment awaiting analysis results
    /// </summary>
    public class PendingSegment
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ResultFileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public int SegmentNumber { get; set; }
        public string PatientId { get; set; } = string.Empty;

        /// <summary>
        /// Result status: null = pending, 0 = normal, 1 = seizure
        /// </summary>
        public int? Result { get; set; }

        public PendingSegment(string filePath, int segmentNumber, string patientId)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            SegmentNumber = segmentNumber;
            PatientId = patientId;
            UploadedAt = DateTime.Now;

            // Generate expected result file name
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            ResultFileName = $"{fileNameWithoutExt}_result.json";
        }
    }
}
