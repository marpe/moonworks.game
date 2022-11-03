using System.Text.RegularExpressions;

namespace MyGame.Utils;

public static class IEnumerableExt
{
    public static IOrderedEnumerable<T> OrderByNatural<T>(
        this IEnumerable<T> items,
        Func<T, string?> selector,
        StringComparer? stringComparer = null
    )
    {
        var regex = new Regex(@"\d+", RegexOptions.Compiled);

        var list = items.ToList();

        int maxDigits = list
            .SelectMany(i =>
                regex.Matches(selector(i) ?? string.Empty)
                    .Select(digitChunk => (int?)digitChunk.Value.Length)
            )
            .Max() ?? 0;

        return list.OrderBy(
            i =>
                regex.Replace(
                    selector(i) ?? string.Empty,
                    match => match.Value.PadLeft(maxDigits, '0')
                ),
            stringComparer ?? StringComparer.CurrentCulture
        );
    }
}
