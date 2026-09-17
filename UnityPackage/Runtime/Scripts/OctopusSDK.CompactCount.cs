using System.Globalization;

public partial class OctopusSDK
{
    /// <summary>
    /// Formats a count using K (thousands), M (millions) or B (billions).
    /// Values below 1000, including negatives, are returned unchanged. Within a band,
    /// values below 10 are truncated to one decimal with trailing zero omitted;
    /// larger values are truncated to an integer. The decimal separator is always a dot.
    /// This pure C# helper works before initialization and in the Editor Mock.
    /// </summary>
    public static string FormatOctopusCompactCount(long count)
    {
        if (count < 1000) return count.ToString(CultureInfo.InvariantCulture);
        if (count >= 1000000000) return FormatCompactCountBand(count, 1000000000, "B");
        if (count >= 1000000) return FormatCompactCountBand(count, 1000000, "M");
        return FormatCompactCountBand(count, 1000, "K");
    }

    private static string FormatCompactCountBand(long count, long divisor, string suffix)
    {
        // Integer arithmetic preserves truncation across the entire long range.
        long whole = count / divisor;
        long tenth = (count % divisor) / (divisor / 10);
        string value = whole.ToString(CultureInfo.InvariantCulture);
        if (whole < 10 && tenth != 0)
        {
            value += "." + tenth.ToString(CultureInfo.InvariantCulture);
        }
        return value + suffix;
    }
}
