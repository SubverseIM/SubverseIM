using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using System;

namespace SubverseIM.Extensions
{
    public static class ImageFrameExtensions
    {
        public static TimeSpan GetFrameDelay(this ImageFrame frame)
        {
            var frameMetadata = frame.Metadata;

            if (frameMetadata.TryGetGifMetadata(out GifFrameMetadata? gifFrameMetadata) && gifFrameMetadata.FrameDelay > 0)
            {
                return TimeSpan.FromMilliseconds(gifFrameMetadata.FrameDelay * 10);
            }
            else if (frameMetadata.TryGetPngMetadata(out PngFrameMetadata? pngFrameMetadata) && pngFrameMetadata.FrameDelay.Denominator > 0)
            {
                return TimeSpan.FromSeconds((double)pngFrameMetadata.FrameDelay.Numerator / pngFrameMetadata.FrameDelay.Denominator);
            }
            else if (frameMetadata.TryGetWebpFrameMetadata(out WebpFrameMetadata? webpFrameMetadata) && webpFrameMetadata.FrameDelay > 0)
            {
                return TimeSpan.FromMilliseconds(webpFrameMetadata.FrameDelay);
            }
            else
            {
                return TimeSpan.Zero;
            }
        }
    }
}
