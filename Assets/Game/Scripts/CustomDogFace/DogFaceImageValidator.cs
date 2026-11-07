using System;
using System.Security.Cryptography;

namespace DogSimulator.CustomDogFace
{
    public static class DogFaceImageValidator
    {
        public static bool TryReadJpegSize(ReadOnlySpan<byte> data, out int width, out int height)
        {
            width = height = 0;
            if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8) return false;
            var offset = 2;
            while (offset + 3 < data.Length)
            {
                if (data[offset++] != 0xff) return false;
                while (offset < data.Length && data[offset] == 0xff) offset++;
                if (offset >= data.Length) return false;
                var marker = data[offset++];
                if (marker == 0xd9 || marker == 0xda) return false;
                if (marker is >= 0xd0 and <= 0xd7 || marker == 0x01) continue;
                if (offset + 1 >= data.Length) return false;
                var length = (data[offset] << 8) | data[offset + 1];
                if (length < 2 || offset + length > data.Length) return false;
                if (IsStartOfFrame(marker))
                {
                    if (length < 7) return false;
                    height = (data[offset + 3] << 8) | data[offset + 4];
                    width = (data[offset + 5] << 8) | data[offset + 6];
                    return width > 0 && height > 0;
                }
                offset += length;
            }
            return false;
        }

        public static bool IsValidFaceJpeg(byte[] bytes, out string error)
        {
            if (bytes == null || bytes.Length == 0) { error = "Face image is empty."; return false; }
            if (bytes.Length > CustomDogFaceConstants.MaximumJpegBytes) { error = "Face image exceeds 128 KiB."; return false; }
            if (!TryReadJpegSize(bytes, out var width, out var height)) { error = "Face image is not a valid JPEG."; return false; }
            if (width != CustomDogFaceConstants.TextureSize || height != CustomDogFaceConstants.TextureSize)
            { error = "Face JPEG must be 512 × 512."; return false; }
            error = string.Empty;
            return true;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool IsStartOfFrame(byte marker)
            => marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
                or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;
    }
}
