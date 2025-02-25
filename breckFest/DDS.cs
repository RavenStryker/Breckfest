using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Squish;

namespace breckFest
{
    public enum D3DFormat : uint
    {
        A8R8G8B8 = 21,
        A8 = 28,
        ATI2 = 0x32495441,  // MakeFourCC('A', 'T', 'I', '2')
        DXT1 = 0x31545844,  // MakeFourCC('D', 'X', 'T', '1')
        DXT3 = 0x33545844,  // MakeFourCC('D', 'X', 'T', '3')
        DXT5 = 0x35545844   // MakeFourCC('D', 'X', 'T', '5')
    }

    public enum PixelFormatFourCC
    {
        DXT1 = 0x31545844,
        DXT3 = 0x33545844,
        DXT5 = 0x35545844
    }

    [Flags]
    public enum PixelFormatFlags : uint
    {
        DDPF_ALPHAPIXELS = 0x1,
        DDPF_ALPHA = 0x2,
        DDPF_FOURCC = 0x4,
        DDPF_RGB = 0x40,
        DDPF_YUV = 0x200,
        DDPF_LUMINANCE = 0x20000
    }

    [Flags]
    public enum DDSCaps : uint
    {
        DDSCAPS_COMPLEX = 0x8,
        DDSCAPS_TEXTURE = 0x1000,
        DDSCAPS_MIPMAP = 0x400000
    }

    [Flags]
    public enum DDSCaps2 : uint
    {
        DDSCAPS2_CUBEMAP = 0x200,
        DDSCAPS2_CUBEMAP_POSITIVEX = 0x400,
        DDSCAPS2_CUBEMAP_NEGATIVEX = 0x800,
        DDSCAPS2_CUBEMAP_POSITIVEY = 0x1000,
        DDSCAPS2_CUBEMAP_NEGATIVEY = 0x2000,
        DDSCAPS2_CUBEMAP_POSITIVEZ = 0x4000,
        DDSCAPS2_CUBEMAP_NEGATIVEZ = 0x8000,
        DDSCAPS2_VOLUME = 0x200000
    }

    public class DDSPixelFormat
    {
        public PixelFormatFlags Flags { get; set; }
        public PixelFormatFourCC FourCC { get; set; }
        public int RGBBitCount { get; set; }
        public uint RBitMask { get; set; }
        public uint GBitMask { get; set; }
        public uint BBitMask { get; set; }
        public uint ABitMask { get; set; }
    }

    public class DDS
    {
        [Flags]
        public enum HeaderFlags
        {
            Caps = 0x1,
            Height = 0x2,
            Width = 0x4,
            Pitch = 0x8,
            PixelFormat = 0x1000,
            MipMapCount = 0x20000,
            LinearSize = 0x80000,
            Depth = 0x800000
        }

        public HeaderFlags Flags { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int Pitch { get; set; }
        public int Depth { get; set; } = 0;
        public DDSPixelFormat PixelFormat { get; set; }
        public DDSCaps Caps { get; set; }
        public DDSCaps2 Caps2 { get; set; }
        public string Name { get; set; }
        public string Extension { get; } = "dds";
        public List<MipMap> MipMaps { get; set; } = new List<MipMap>();
        public D3DFormat Format { get; set; }

        public DDS() { }

        public DDS(D3DFormat format, Bitmap bitmap)
        {
            SquishFlags flags = SquishFlags.kDxt1;
            bool compressed = true;

            switch (format)
            {
                case D3DFormat.DXT1:
                    flags = SquishFlags.kDxt1;
                    break;
                case D3DFormat.DXT3:
                    flags = SquishFlags.kDxt3;
                    break;
                case D3DFormat.DXT5:
                    flags = SquishFlags.kDxt5;
                    break;
                default:
                    compressed = false;
                    break;
            }

            Format = format;
            Width = bitmap.Width;
            Height = bitmap.Height;

            MipMap mip = new MipMap
            {
                Width = Width,
                Height = Height
            };

            byte[] data = new byte[mip.Width * mip.Height * 4];

            BitmapData bmpdata = bitmap.LockBits(new Rectangle(0, 0, mip.Width, mip.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(bmpdata.Scan0, data, 0, bmpdata.Stride * bmpdata.Height);
            bitmap.UnlockBits(bmpdata);

            if (compressed)
            {
                for (uint i = 0; i < data.Length - 4; i += 4)
                {
                    byte r = data[i + 0];
                    data[i + 0] = data[i + 2];
                    data[i + 2] = r;
                }

                byte[] dest = new byte[Squish.Squish.GetStorageRequirements(mip.Width, mip.Height, flags | SquishFlags.kColourIterativeClusterFit)];
                Squish.Squish.CompressImage(data, mip.Width, mip.Height, dest, flags | SquishFlags.kColourIterativeClusterFit, true);
                mip.Data = dest;
            }
            else
            {
                mip.Data = data;
            }

            MipMaps.Add(mip);
        }

        public static DDS Load(string path)
        {
            return Load(File.ReadAllBytes(path));
        }

        public static DDS Load(byte[] data)
        {
            DDS dds = new DDS();

            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                if (!IsDDS(br)) { return null; }

                br.ReadUInt32();    // header length
                dds.Flags = (HeaderFlags)br.ReadUInt32();
                dds.Height = (int)br.ReadUInt32();
                dds.Width = (int)br.ReadUInt32();
                dds.Pitch = (int)br.ReadUInt32();
                dds.Depth = (int)br.ReadUInt32();
                int mipCount = (int)br.ReadUInt32();
                for (int i = 0; i < 11; i++) { br.ReadUInt32(); }
                br.ReadUInt32();    // pixel format length
                dds.PixelFormat = new DDSPixelFormat
                {
                    Flags = (PixelFormatFlags)br.ReadUInt32(),
                    FourCC = (PixelFormatFourCC)br.ReadUInt32(),
                    RGBBitCount = (int)br.ReadUInt32(),
                    RBitMask = br.ReadUInt32(),
                    GBitMask = br.ReadUInt32(),
                    BBitMask = br.ReadUInt32(),
                    ABitMask = br.ReadUInt32()
                };
                dds.Caps = (DDSCaps)br.ReadUInt32();
                dds.Caps2 = (DDSCaps2)br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();

                if (dds.PixelFormat.Flags.HasFlag(PixelFormatFlags.DDPF_FOURCC))
                {
                    uint fourCC = (uint)dds.PixelFormat.FourCC;
                    // Map older BC5U files (1429553986) to ATI2 for backward compatibility.
                    if (fourCC == 1429553986) // "BC5U"
                    {
                        dds.Format = D3DFormat.ATI2;
                    }
                    else
                    {
                        dds.Format = (D3DFormat)fourCC;
                    }
                }
                else if (dds.PixelFormat.Flags.HasFlag(PixelFormatFlags.DDPF_RGB) &&
                         dds.PixelFormat.Flags.HasFlag(PixelFormatFlags.DDPF_ALPHAPIXELS))
                {
                    dds.Format = D3DFormat.A8R8G8B8;
                }

                for (int i = 0; i < Math.Max(1, mipCount); i++)
                {
                    MipMap mip = new MipMap
                    {
                        Width = dds.Width >> i,
                        Height = dds.Height >> i
                    };

                    switch (dds.Format)
                    {
                        case D3DFormat.A8R8G8B8:
                            mip.Data = br.ReadBytes(mip.Width * mip.Height * 4);
                            break;
                        case D3DFormat.DXT1:
                            mip.Data = br.ReadBytes((((mip.Width + 3) / 4) * ((mip.Height + 3) / 4)) * 8);
                            break;
                        case D3DFormat.DXT3:
                        case D3DFormat.DXT5:
                        case D3DFormat.ATI2:
                            mip.Data = br.ReadBytes((((mip.Width + 3) / 4) * ((mip.Height + 3) / 4)) * 16);
                            break;
                    }

                    dds.MipMaps.Add(mip);
                }
            }

            return dds;
        }

        public static bool IsDDS(BinaryReader br)
        {
            return (
                br.ReadByte() == 0x44 && // D
                br.ReadByte() == 0x44 && // D
                br.ReadByte() == 0x53 && // S
                br.ReadByte() == 0x20    //
            );
        }

        public void Save(string path)
        {
            using (BinaryWriter bw = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                Save(bw, this);
            }
        }

        public static void Save(BinaryWriter bw, DDS dds)
        {
            HeaderFlags flags = HeaderFlags.Caps | HeaderFlags.Height | HeaderFlags.Width | HeaderFlags.PixelFormat | HeaderFlags.MipMapCount;
            flags |= dds.Format == D3DFormat.A8R8G8B8 ? HeaderFlags.Pitch : HeaderFlags.LinearSize;

            bw.Write(new byte[] { 0x44, 0x44, 0x53, 0x20 });    // 'DDS '
            bw.Write(124);
            bw.Write((int)flags);
            bw.Write(dds.Height);
            bw.Write(dds.Width);
            bw.Write(flags.HasFlag(HeaderFlags.Pitch) ? dds.Width * 4 : dds.MipMaps[0].Data.Length);
            bw.Write(dds.Depth);
            bw.Write(dds.MipMaps.Count);

            for (int i = 0; i < 11; i++) { bw.Write(0); }

            // PixelFormat
            bw.Write(32);

            switch (dds.Format)
            {
                case D3DFormat.DXT1:
                case D3DFormat.DXT3:
                case D3DFormat.DXT5:
                case D3DFormat.ATI2:
                    bw.Write(4);        // fourCC length
                    char[] fourCC = dds.Format.ToString().ToCharArray();
                    bw.Write(fourCC);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    break;

                default:
                    bw.Write(0);    // fourCC length
                    bw.Write(0);
                    bw.Write(32);   //  RGB bit count
                    bw.Write(255 << 16);    // R mask
                    bw.Write(255 << 8);     // G mask
                    bw.Write(255 << 0);     // B mask
                    bw.Write(255 << 24);    // A mask
                    break;
            }

            bw.Write((int)DDSCaps.DDSCAPS_TEXTURE);
            bw.Write(0);    // Caps 2
            bw.Write(0);    // Caps 3
            bw.Write(0);    // Caps 4
            bw.Write(0);    // Reserved

            for (int i = 0; i < dds.MipMaps.Count; i++)
            {
                bw.Write(dds.MipMaps[i].Data);
            }
        }

        public Bitmap Decompress(int mipLevel = 0, bool suppressAlpha = false)
        {
            MipMap mip = MipMaps[mipLevel];

            if (Format == D3DFormat.ATI2)
            {
                return DecompressATI2(mip, suppressAlpha);
            }

            // Use fully qualified PixelFormat to avoid ambiguity.
            Bitmap b = new Bitmap(mip.Width, mip.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            SquishFlags flags = 0;
            bool notCompressed = false;

            switch (Format)
            {
                case D3DFormat.DXT1:
                    flags = SquishFlags.kDxt1;
                    break;
                case D3DFormat.DXT5:
                    flags = SquishFlags.kDxt5;
                    break;
                case D3DFormat.A8R8G8B8:
                    notCompressed = true;
                    break;
                default:
                    throw new NotImplementedException($"Can't decompress: {Format}");
            }

            byte[] dest = new byte[mip.Width * mip.Height * 4];
            byte[] data = mip.Data;

            if (notCompressed)
            {
                for (uint i = 0; i < data.Length - 4; i += 4)
                {
                    uint colour = (uint)((data[i + 3] << 24) | (data[i + 2] << 16) | (data[i + 1] << 8) | (data[i + 0] << 0));

                    dest[i + 0] = (byte)(colour & 0x000000FF);
                    dest[i + 1] = (byte)((colour & 0x0000FF00) >> 8);
                    dest[i + 2] = (byte)((colour & 0x00FF0000) >> 16);
                    dest[i + 3] = (byte)((colour & 0xFF000000) >> 24);
                }
            }
            else
            {
                Squish.Squish.DecompressImage(dest, mip.Width, mip.Height, data, flags);

                for (uint i = 0; i < dest.Length - 4; i += 4)
                {
                    byte r = dest[i + 0];
                    dest[i + 0] = dest[i + 2];
                    dest[i + 2] = r;
                }
            }

            BitmapData bmpdata = b.LockBits(new Rectangle(0, 0, mip.Width, mip.Height),
                ImageLockMode.WriteOnly, (suppressAlpha ? System.Drawing.Imaging.PixelFormat.Format32bppRgb : b.PixelFormat));
            Marshal.Copy(dest, 0, bmpdata.Scan0, dest.Length);
            b.UnlockBits(bmpdata);

            return b;
        }

        private Bitmap DecompressATI2(MipMap mip, bool suppressAlpha)
        {
            int width = mip.Width;
            int height = mip.Height;
            // Create output bitmap using fully qualified PixelFormat.
            Bitmap b = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            byte[] dest = new byte[width * height * 4];

            int blocksX = (width + 3) / 4;
            int blocksY = (height + 3) / 4;
            byte[] blockData = mip.Data;

            // Loop over each 4x4 block.
            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockIndex = (by * blocksX + bx) * 16;
                    // Each block: first 8 bytes and second 8 bytes.
                    // We'll use the first 8 bytes as one channel and the second 8 bytes as the other.
                    // Based on our testing, treating the first block as green and the second as red gives better results.
                    byte[] block1 = new byte[8];
                    byte[] block2 = new byte[8];
                    Array.Copy(blockData, blockIndex, block1, 0, 8);
                    Array.Copy(blockData, blockIndex + 8, block2, 0, 8);

                    byte[] redValues = new byte[16];
                    byte[] greenValues = new byte[16];

                    // Decode both blocks using your DXT5 alpha block decompression routine.
                    DecompressDXT5AlphaBlock(block2, redValues);   // Use second block for red.
                    DecompressDXT5AlphaBlock(block1, greenValues);   // Use first block for green.

                    // Process each pixel in the 4x4 block.
                    for (int j = 0; j < 16; j++)
                    {
                        int x = bx * 4 + (j % 4);
                        int y = by * 4 + (j / 4);
                        if (x < width && y < height)
                        {
                            // Normalize the values assuming 128 is neutral:
                            double xNorm = (greenValues[j] - 128) / 127.0;  // x from green block
                            double yNorm = (redValues[j] - 128) / 127.0;      // y from red block
                            double zNorm = Math.Sqrt(Math.Max(0, 1 - xNorm * xNorm - yNorm * yNorm));

                            // Remap normalized values back to [0,255]:
                            byte rOut = (byte)(xNorm * 127 + 128);   // computed red value
                            byte gOut = (byte)(yNorm * 127 + 128);   // computed green value
                            byte bOut = (byte)(zNorm * 127 + 128);     // computed blue value

                            int destIndex = (y * width + x) * 4;
                            // Here, swap the red and blue output so that a neutral normal becomes (128,128,255)
                            dest[destIndex + 0] = bOut;  // assign computed blue to red channel
                            dest[destIndex + 1] = gOut;  // green remains the same
                            dest[destIndex + 2] = rOut;  // assign computed red to blue channel
                            dest[destIndex + 3] = 255;
                        }
                    }
                }
            }

            BitmapData bmpdata = b.LockBits(new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Marshal.Copy(dest, 0, bmpdata.Scan0, dest.Length);
            b.UnlockBits(bmpdata);

            return b;
        }


        private static void DecompressDXT5AlphaBlock(byte[] block, byte[] output)
        {
            byte alpha0 = block[0];
            byte alpha1 = block[1];

            ulong indices = 0;
            for (int i = 0; i < 6; i++)
            {
                indices |= ((ulong)block[i + 2]) << (8 * i);
            }

            byte[] alphas = new byte[8];
            alphas[0] = alpha0;
            alphas[1] = alpha1;
            if (alpha0 > alpha1)
            {
                for (int i = 1; i < 7; i++)
                {
                    alphas[i + 1] = (byte)(((8 - i) * alpha0 + i * alpha1) / 8);
                }
            }
            else
            {
                for (int i = 1; i < 5; i++)
                {
                    alphas[i + 1] = (byte)(((6 - i) * alpha0 + i * alpha1) / 6);
                }
                alphas[6] = 0;
                alphas[7] = 255;
            }

            for (int i = 0; i < 16; i++)
            {
                int index = (int)(indices & 0x7);
                indices >>= 3;
                output[i] = alphas[index];
            }
        }
    }

    public class MipMap
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Data { get; set; }
    }
}
