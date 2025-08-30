namespace SubverseIM
{
    public static class UnitHelpers
    {

        private static readonly string[] byteCountUnits = ["B", "KiB", "MiB", "GiB"];

        public static string ByteCountToString(long? byteCount)
        {
            if (byteCount is null) return string.Empty;

            int idx = 0;
            long prev = 0, curr = byteCount.Value;
            while (curr >= 1024 && idx++ < byteCountUnits.Length)
            {
                prev = curr & 1023;
                curr >>= 10;
            }

            decimal result = curr + prev / 1000M;
            return $" ({result:F2} {byteCountUnits[idx]})";
        }
    }
}