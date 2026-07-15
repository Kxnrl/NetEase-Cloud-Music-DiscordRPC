using System.Text;

namespace Kxnrl.Vanessa;

internal static class StringExtensions
{
    /// <summary>
    ///     Truncates a string to fit within <paramref name="maxBytes" /> UTF-8 bytes,
    ///     appending "..." when truncated. Cuts on rune boundaries, never splitting surrogate pairs.
    /// </summary>
    public static string TruncateByUtf8Bytes(this string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var budget = maxBytes - 3; // reserve 3 bytes for "..."
        var bytes  = 0;
        var chars  = 0;

        foreach (var rune in value.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > budget)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            chars += rune.Utf16SequenceLength;
        }

        return value[..chars] + "...";
    }
}
