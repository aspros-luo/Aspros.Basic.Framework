namespace Aspros.Base.Framework.Infrastructure;

public static class StringExtension
{
    public static string ToUnderscoreCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        if (str.Length == 0)
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder(str.Length + 8);

        for (var i = 0; i < str.Length; i++)
        {
            var character = str[i];

            if (i > 0 && char.IsUpper(character))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }

    public static string ToPascalCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);

        var segments = str.Split(
            '_',
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder(str.Length);

        foreach (var segment in segments)
        {
            result.Append(char.ToUpperInvariant(segment[0]));

            if (segment.Length > 1)
            {
                result.Append(segment.AsSpan(1));
            }
        }

        return result.ToString();
    }
}
