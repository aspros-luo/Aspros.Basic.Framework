namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Lightweight string naming-conversion helpers.
/// 轻量级字符串命名格式转换工具。
/// </summary>
public static class StringExtension
{
    /// <summary>
    /// Converts Pascal/camel-style names to underscore-separated lowercase text.
    /// 将 Pascal/camel 风格名称转换为小写下划线格式。
    ///
    /// <para>
    /// This is intentionally simple: every uppercase character after the first
    /// becomes a separator.
    /// 这里采用简单规则：除第一个字符外，每个大写字符前增加下划线。
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Converts underscore-separated words to PascalCase.
    /// 将下划线分隔的单词转换为 PascalCase。
    /// </summary>
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