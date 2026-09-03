using System.Text.RegularExpressions;

namespace DTS.Utils;

internal static class StringHelper
{
    private readonly static Regex WhiteSpaceRegex =
        new(@"\s+", RegexOptions.Compiled);

    public static string RemoveWhitespaceRegex(string input) =>
        WhiteSpaceRegex.Replace(input, "");

    public static string JoinWithBackSlash(params string[] paths) =>
        string.Join('\\', paths);
}