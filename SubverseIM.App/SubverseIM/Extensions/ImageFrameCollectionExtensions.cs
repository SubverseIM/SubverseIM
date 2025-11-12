using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;

namespace SubverseIM.Extensions
{
    public static class ImageFrameCollectionExtensions
    {
        public static IEnumerable<TimeSpan> GetCumulativeFrameDelays(this ImageFrameCollection frames)
        {
            TimeSpan accum = TimeSpan.Zero;
            for (int i = 0; i < frames.Count; i++)
            {
                yield return accum;
                accum += frames[i].GetFrameDelay();
            }
        }
    }
}
