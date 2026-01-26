using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Optimized GIF89a encoder for Unity.
    /// Uses global color table for better compression and performance.
    /// Features:
    /// - Global color table (written once, not per frame)
    /// - Optimized LZW compression with int-based lookup
    /// - Pre-computed color lookup table for O(1) quantization
    /// - Reusable buffers to minimize GC pressure
    /// </summary>
    public class GifEncoder : IDisposable
    {
        private readonly Stream _stream;
        private readonly int _width;
        private readonly int _height;
        private readonly int _colorCount;
        private readonly int _frameDelay;
        private bool _headerWritten;
        private bool _disposed;

        private Color32[] _palette;
        private byte[] _colorLookup;
        private int _colorTableBits;

        // Reusable buffers to reduce GC pressure
        private readonly byte[] _indexedPixelsBuffer;
        private readonly LzwEncoder _lzwEncoder;

        public GifEncoder(Stream stream, int width, int height, int fps = 20, int colorCount = 128)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _width = width;
            _height = height;
            _colorCount = Mathf.Clamp(colorCount, 2, 256);
            _frameDelay = Mathf.Max(1, 100 / fps);

            _indexedPixelsBuffer = new byte[width * height];
            _lzwEncoder = new LzwEncoder();
        }

        public void Initialize(byte[] firstFramePixels)
        {
            if (_headerWritten) return;

            _palette = BuildPaletteFast(firstFramePixels, _colorCount);
            _colorTableBits = GetColorTableBits();
            InitColorLookup(_palette);
            WriteHeaderWithGlobalColorTable();
            _headerWritten = true;
        }

        /// <summary>
        /// Add a frame to the GIF.
        /// </summary>
        /// <param name="pixels">RGBA pixel data</param>
        /// <param name="frameDelay">Frame delay in 1/100 seconds. If -1, uses default delay based on fps.</param>
        public void AddFrame(byte[] pixels, int frameDelay = -1)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GifEncoder));
            if (pixels == null || pixels.Length != _width * _height * 4)
                throw new ArgumentException($"Invalid pixel data length. Expected {_width * _height * 4}, got {pixels?.Length ?? 0}");

            if (!_headerWritten)
                Initialize(pixels);

            // Use provided delay or fall back to default
            int delay = frameDelay > 0 ? frameDelay : _frameDelay;

            QuantizePixelsFast(pixels, _indexedPixelsBuffer);
            WriteGraphicControlExtension(delay);
            WriteImageDescriptor();
            WriteLzwData(_indexedPixelsBuffer);
        }

        public void Finish()
        {
            if (_disposed) return;
            if (_headerWritten)
                _stream.WriteByte(0x3B);
            _stream.Flush();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Finish();
            _disposed = true;
        }

        #region Header & Extensions

        private void WriteHeaderWithGlobalColorTable()
        {
            WriteString("GIF89a");
            WriteUInt16(_width);
            WriteUInt16(_height);

            byte packed = (byte)(0x80 | ((_colorTableBits - 1) << 4) | (_colorTableBits - 1));
            _stream.WriteByte(packed);
            _stream.WriteByte(0x00);
            _stream.WriteByte(0x00);

            WriteColorTable(_palette);
            WriteNetscapeExtension(0);
        }

        private void WriteNetscapeExtension(int loopCount)
        {
            _stream.WriteByte(0x21);
            _stream.WriteByte(0xFF);
            _stream.WriteByte(0x0B);
            WriteString("NETSCAPE2.0");
            _stream.WriteByte(0x03);
            _stream.WriteByte(0x01);
            WriteUInt16(loopCount);
            _stream.WriteByte(0x00);
        }

        private void WriteGraphicControlExtension(int delay)
        {
            _stream.WriteByte(0x21);
            _stream.WriteByte(0xF9);
            _stream.WriteByte(0x04);
            _stream.WriteByte(0x00);
            WriteUInt16(delay);
            _stream.WriteByte(0x00);
            _stream.WriteByte(0x00);
        }

        private void WriteImageDescriptor()
        {
            _stream.WriteByte(0x2C);
            WriteUInt16(0);
            WriteUInt16(0);
            WriteUInt16(_width);
            WriteUInt16(_height);
            _stream.WriteByte(0x00); // No local color table
        }

        private void WriteColorTable(Color32[] palette)
        {
            int tableSize = 1 << _colorTableBits;
            for (int i = 0; i < tableSize; i++)
            {
                if (i < palette.Length)
                {
                    _stream.WriteByte(palette[i].r);
                    _stream.WriteByte(palette[i].g);
                    _stream.WriteByte(palette[i].b);
                }
                else
                {
                    _stream.WriteByte(0);
                    _stream.WriteByte(0);
                    _stream.WriteByte(0);
                }
            }
        }

        #endregion

        #region Color Quantization

        private Color32[] BuildPaletteFast(byte[] pixels, int maxColors)
        {
            var colors = new List<Color32>(20000);
            int step = Mathf.Max(1, pixels.Length / 4 / 20000);

            for (int i = 0; i < pixels.Length; i += step * 4)
                colors.Add(new Color32(pixels[i], pixels[i + 1], pixels[i + 2], 255));

            var boxes = new List<ColorBox> { new ColorBox(colors) };

            while (boxes.Count < maxColors)
            {
                int bestIndex = 0, bestRange = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    int range = boxes[i].GetLargestRange();
                    if (range > bestRange && boxes[i].Colors.Count > 1)
                    {
                        bestRange = range;
                        bestIndex = i;
                    }
                }

                if (bestRange == 0) break;

                var box = boxes[bestIndex];
                boxes.RemoveAt(bestIndex);
                var (box1, box2) = box.Split();
                if (box1.Colors.Count > 0) boxes.Add(box1);
                if (box2.Colors.Count > 0) boxes.Add(box2);
            }

            var palette = new Color32[maxColors];
            for (int i = 0; i < boxes.Count && i < maxColors; i++)
                palette[i] = boxes[i].GetAverageColor();

            return palette;
        }

        private void InitColorLookup(Color32[] palette)
        {
            _colorLookup = new byte[32768];

            for (int r = 0; r < 32; r++)
                for (int g = 0; g < 32; g++)
                    for (int b = 0; b < 32; b++)
                    {
                        int idx = (r << 10) | (g << 5) | b;
                        _colorLookup[idx] = FindClosestColor(
                            (byte)((r << 3) | (r >> 2)),
                            (byte)((g << 3) | (g >> 2)),
                            (byte)((b << 3) | (b >> 2)),
                            palette);
                    }
        }

        private void QuantizePixelsFast(byte[] pixels, byte[] indexed)
        {
            int pixelCount = _width * _height;
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                int lookupIdx = ((pixels[offset] >> 3) << 10) |
                               ((pixels[offset + 1] >> 3) << 5) |
                               (pixels[offset + 2] >> 3);
                indexed[i] = _colorLookup[lookupIdx];
            }
        }

        private byte FindClosestColor(byte r, byte g, byte b, Color32[] palette)
        {
            int bestIndex = 0, bestDist = int.MaxValue;

            for (int i = 0; i < palette.Length; i++)
            {
                int dr = r - palette[i].r;
                int dg = g - palette[i].g;
                int db = b - palette[i].b;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return (byte)bestIndex;
        }

        private class ColorBox
        {
            public List<Color32> Colors;

            public ColorBox(List<Color32> colors) { Colors = colors; }

            public int GetLargestRange()
            {
                if (Colors.Count == 0) return 0;

                int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;

                foreach (var c in Colors)
                {
                    if (c.r < minR) minR = c.r; if (c.r > maxR) maxR = c.r;
                    if (c.g < minG) minG = c.g; if (c.g > maxG) maxG = c.g;
                    if (c.b < minB) minB = c.b; if (c.b > maxB) maxB = c.b;
                }

                return Mathf.Max(maxR - minR, Mathf.Max(maxG - minG, maxB - minB));
            }

            public (ColorBox, ColorBox) Split()
            {
                if (Colors.Count <= 1)
                    return (new ColorBox(new List<Color32>(Colors)), new ColorBox(new List<Color32>()));

                int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;

                foreach (var c in Colors)
                {
                    if (c.r < minR) minR = c.r; if (c.r > maxR) maxR = c.r;
                    if (c.g < minG) minG = c.g; if (c.g > maxG) maxG = c.g;
                    if (c.b < minB) minB = c.b; if (c.b > maxB) maxB = c.b;
                }

                int rangeR = maxR - minR, rangeG = maxG - minG, rangeB = maxB - minB;

                if (rangeR >= rangeG && rangeR >= rangeB)
                    Colors.Sort((a, b) => a.r.CompareTo(b.r));
                else if (rangeG >= rangeB)
                    Colors.Sort((a, b) => a.g.CompareTo(b.g));
                else
                    Colors.Sort((a, b) => a.b.CompareTo(b.b));

                int mid = Colors.Count / 2;
                return (new ColorBox(Colors.GetRange(0, mid)), new ColorBox(Colors.GetRange(mid, Colors.Count - mid)));
            }

            public Color32 GetAverageColor()
            {
                if (Colors.Count == 0) return new Color32(0, 0, 0, 255);

                int r = 0, g = 0, b = 0;
                foreach (var c in Colors) { r += c.r; g += c.g; b += c.b; }

                return new Color32((byte)(r / Colors.Count), (byte)(g / Colors.Count), (byte)(b / Colors.Count), 255);
            }
        }

        #endregion

        #region LZW Compression

        private void WriteLzwData(byte[] indexedPixels)
        {
            int minCodeSize = Mathf.Max(2, _colorTableBits);
            _stream.WriteByte((byte)minCodeSize);

            var compressed = _lzwEncoder.Encode(indexedPixels, minCodeSize);

            int offset = 0;
            while (offset < compressed.Length)
            {
                int blockSize = Mathf.Min(255, compressed.Length - offset);
                _stream.WriteByte((byte)blockSize);
                _stream.Write(compressed, offset, blockSize);
                offset += blockSize;
            }

            _stream.WriteByte(0x00);
        }

        private class LzwEncoder
        {
            private const int MaxCodeTableSize = 4096;
            private const int HashSize = 5003;

            private readonly int[] _hashTable = new int[HashSize];
            private readonly int[] _codeTable = new int[HashSize];
            private readonly List<byte> _outputBytes = new List<byte>(65536);

            private int _codeSize, _nextCode, _clearCode, _endCode;
            private int _bitBuffer, _bitCount;

            public byte[] Encode(byte[] data, int minCodeSize)
            {
                InitCodeTable(minCodeSize);
                _outputBytes.Clear();
                _bitBuffer = 0;
                _bitCount = 0;

                WriteBits(_clearCode, _codeSize);

                if (data.Length == 0)
                {
                    WriteBits(_endCode, _codeSize);
                    FlushBits();
                    return _outputBytes.ToArray();
                }

                int currentCode = data[0];

                for (int i = 1; i < data.Length; i++)
                {
                    int pixel = data[i];
                    int hash = GetHash(currentCode, pixel);

                    while (_hashTable[hash] != -1)
                    {
                        if (_hashTable[hash] == ((currentCode << 8) | pixel))
                        {
                            currentCode = _codeTable[hash];
                            goto nextPixel;
                        }
                        hash = (hash + 1) % HashSize;
                    }

                    WriteBits(currentCode, _codeSize);

                    if (_nextCode < MaxCodeTableSize)
                    {
                        _hashTable[hash] = (currentCode << 8) | pixel;
                        _codeTable[hash] = _nextCode++;

                        if (_nextCode > (1 << _codeSize) && _codeSize < 12)
                            _codeSize++;
                    }
                    else
                    {
                        WriteBits(_clearCode, _codeSize);
                        InitCodeTable(minCodeSize);
                    }

                    currentCode = pixel;
                nextPixel:;
                }

                WriteBits(currentCode, _codeSize);
                WriteBits(_endCode, _codeSize);
                FlushBits();

                return _outputBytes.ToArray();
            }

            private void InitCodeTable(int minCodeSize)
            {
                for (int i = 0; i < HashSize; i++) _hashTable[i] = -1;

                int tableSize = 1 << minCodeSize;
                _clearCode = tableSize;
                _endCode = tableSize + 1;
                _nextCode = tableSize + 2;
                _codeSize = minCodeSize + 1;
            }

            private int GetHash(int code, int pixel) => ((code << 8) ^ pixel) % HashSize;

            private void WriteBits(int value, int numBits)
            {
                _bitBuffer |= (value << _bitCount);
                _bitCount += numBits;

                while (_bitCount >= 8)
                {
                    _outputBytes.Add((byte)(_bitBuffer & 0xFF));
                    _bitBuffer >>= 8;
                    _bitCount -= 8;
                }
            }

            private void FlushBits()
            {
                if (_bitCount > 0) _outputBytes.Add((byte)(_bitBuffer & 0xFF));
            }
        }

        #endregion

        #region Helpers

        private int GetColorTableBits()
        {
            int bits = 1;
            while ((1 << bits) < _colorCount) bits++;
            return Mathf.Clamp(bits, 2, 8);
        }

        private void WriteString(string s)
        {
            foreach (char c in s) _stream.WriteByte((byte)c);
        }

        private void WriteUInt16(int value)
        {
            _stream.WriteByte((byte)(value & 0xFF));
            _stream.WriteByte((byte)((value >> 8) & 0xFF));
        }

        #endregion
    }
}
