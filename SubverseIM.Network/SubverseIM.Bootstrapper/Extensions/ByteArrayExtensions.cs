using System.Globalization;
using System.Text;

namespace SubverseIM.Bootstrapper.Extensions
{
    public static class ByteArrayExtensions
    {
        private static readonly CompositeFormat HEX_FORMAT = CompositeFormat.Parse("{0:x2}");

        public static string ToHexString(this byte[] arr)
        {
            StringBuilder builder = new();
            for (int i = 0; i < arr.Length; i++)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, HEX_FORMAT, arr[i]);
            }
            return builder.ToString();
        }
    }
}
