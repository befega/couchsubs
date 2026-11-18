// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CouchSubs.Core;

namespace CouchSubs.App.Services;

/// <summary>OBS'ten gelen anlık görüntüyü (JPEG ya da PNG) okuma hattının BGRA karesine çevirir.</summary>
public static class ObsFrameDecoder
{
    public static Frame? Decode(byte[] bytes)
    {
        try
        {
            using var input = new MemoryStream(bytes, writable: false);
            var source = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var converted = source.Format == PixelFormats.Bgra32 ? (BitmapSource)source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            return new Frame(pixels, converted.PixelWidth, converted.PixelHeight, stride);
        }
        catch (Exception error) when (error is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            // Bozuk tek kare atlanır; sonraki kare yeniden istenir.
            return null;
        }
    }
}
