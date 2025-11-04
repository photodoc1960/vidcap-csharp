using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace EegScreenCapture.VideoEncoder
{
    /// <summary>
    /// Writes AVI files with Motion JPEG encoding
    /// Pure C# implementation - no external dependencies
    /// </summary>
    public class AviWriter : IDisposable
    {
        private readonly string _filePath;
        private readonly int _width;
        private readonly int _height;
        private readonly int _fps;
        private readonly List<byte[]> _frames;
        private bool _isFinalized;

        public AviWriter(string filePath, int width, int height, int fps = 30)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _width = width;
            _height = height;
            _fps = fps;
            _frames = new List<byte[]>();
            _isFinalized = false;
        }

        /// <summary>
        /// Add a frame to the video (expects RGB24 bitmap data)
        /// </summary>
        public void AddFrame(Bitmap frame)
        {
            if (_isFinalized)
                throw new InvalidOperationException("Cannot add frames after finalization");

            // Encode frame as JPEG with 60% quality
            byte[] jpegData = BitmapToJpeg(frame, 60L);
            _frames.Add(jpegData);
        }

        /// <summary>
        /// Finalize and write the AVI file
        /// </summary>
        public void FinalizeVideo()
        {
            if (_isFinalized)
                return;

            WriteAviFile();
            _isFinalized = true;
        }

        private byte[] BitmapToJpeg(Bitmap bitmap, long quality)
        {
            using var ms = new MemoryStream();
            var encoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            bitmap.Save(ms, encoder, encoderParameters);
            return ms.ToArray();
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid)
                ?? throw new Exception("JPEG encoder not found");
        }

        private void WriteAviFile()
        {
            if (_frames.Count == 0)
                return;

            // Ensure parent directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            uint maxFrameSize = (uint)_frames.Max(f => f.Length);
            uint totalFrames = (uint)_frames.Count;
            long moviSizeLong = _frames.Sum(f => (long)f.Length + 8); // +8 for chunk header
            uint moviSize = (uint)Math.Min(moviSizeLong, uint.MaxValue);

            // Calculate sizes
            uint hdrlSize = 4 + 64 + 4 + 56 + 4 + 16;
            uint strlSize = 4 + 64 + 4 + 56;
            uint moviListSize = moviSize + 4;
            uint riffSize = 4 + hdrlSize + strlSize + 4 + moviListSize;

            // Write RIFF header
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(riffSize);
            bw.Write(new[] { 'A', 'V', 'I', ' ' });

            // Write hdrl LIST
            bw.Write(new[] { 'L', 'I', 'S', 'T' });
            bw.Write(hdrlSize);
            bw.Write(new[] { 'h', 'd', 'r', 'l' });

            // Write avih (AVI header) chunk
            bw.Write(new[] { 'a', 'v', 'i', 'h' });
            bw.Write(56u); // chunk size

            uint microsecPerFrame = 1000000u / (uint)_fps;
            bw.Write(microsecPerFrame);          // dwMicroSecPerFrame
            bw.Write((uint)Math.Min((long)maxFrameSize * _fps, uint.MaxValue)); // dwMaxBytesPerSec
            bw.Write(0u);                        // dwPaddingGranularity
            bw.Write(0x10u);                     // dwFlags (AVIF_HASINDEX)
            bw.Write(totalFrames);               // dwTotalFrames
            bw.Write(0u);                        // dwInitialFrames
            bw.Write(1u);                        // dwStreams
            bw.Write(maxFrameSize);              // dwSuggestedBufferSize
            bw.Write((uint)_width);              // dwWidth
            bw.Write((uint)_height);             // dwHeight
            bw.Write(new byte[16]);              // dwReserved[4]

            // Write strl LIST
            bw.Write(new[] { 'L', 'I', 'S', 'T' });
            bw.Write(strlSize);
            bw.Write(new[] { 's', 't', 'r', 'l' });

            // Write strh (stream header)
            bw.Write(new[] { 's', 't', 'r', 'h' });
            bw.Write(56u);
            bw.Write(new[] { 'v', 'i', 'd', 's' });     // fccType
            bw.Write(new[] { 'M', 'J', 'P', 'G' });     // fccHandler (Motion JPEG)
            bw.Write(0u);                                // dwFlags
            bw.Write((ushort)0);                         // wPriority
            bw.Write((ushort)0);                         // wLanguage
            bw.Write(0u);                                // dwInitialFrames
            bw.Write(1u);                                // dwScale
            bw.Write((uint)_fps);                        // dwRate
            bw.Write(0u);                                // dwStart
            bw.Write(totalFrames);                       // dwLength
            bw.Write(maxFrameSize);                      // dwSuggestedBufferSize
            bw.Write(0xFFFFFFFFu);                       // dwQuality (-1 = default)
            bw.Write(0u);                                // dwSampleSize
            bw.Write((ushort)0);                         // rcFrame.left
            bw.Write((ushort)0);                         // rcFrame.top
            bw.Write((ushort)_width);                    // rcFrame.right
            bw.Write((ushort)_height);                   // rcFrame.bottom

            // Write strf (stream format)
            bw.Write(new[] { 's', 't', 'r', 'f' });
            bw.Write(40u); // BITMAPINFOHEADER size
            bw.Write(40u);                               // biSize
            bw.Write((uint)_width);                      // biWidth
            bw.Write((uint)_height);                     // biHeight
            bw.Write((ushort)1);                         // biPlanes
            bw.Write((ushort)24);                        // biBitCount
            bw.Write(new[] { 'M', 'J', 'P', 'G' });     // biCompression
            bw.Write((uint)Math.Min((long)_width * _height * 3, uint.MaxValue)); // biSizeImage
            bw.Write(0u);                                // biXPelsPerMeter
            bw.Write(0u);                                // biYPelsPerMeter
            bw.Write(0u);                                // biClrUsed
            bw.Write(0u);                                // biClrImportant

            // Write movi LIST
            bw.Write(new[] { 'L', 'I', 'S', 'T' });
            bw.Write(moviListSize);
            bw.Write(new[] { 'm', 'o', 'v', 'i' });

            // Write frames
            foreach (var frame in _frames)
            {
                bw.Write(new[] { '0', '0', 'd', 'c' }); // chunk ID
                bw.Write((uint)frame.Length);
                bw.Write(frame);

                // Add padding byte if odd size
                if (frame.Length % 2 == 1)
                    bw.Write((byte)0);
            }
        }

        public void Dispose()
        {
            if (!_isFinalized)
            {
                FinalizeVideo();
            }
        }
    }
}
