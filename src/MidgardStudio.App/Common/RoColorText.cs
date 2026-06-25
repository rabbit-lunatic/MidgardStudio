using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MidgardStudio.App.Common;

/// <summary>
/// Renders Ragnarok <c>^RRGGBB</c>-coded text into a <see cref="TextBlock"/>'s inlines (default black,
/// newlines honored). Shared by the Client Items preview and the Autocomplete settings live preview.
/// </summary>
public static class RoColorText
{
    public static void Render(TextBlock target, string? text)
    {
        target.Inlines.Clear();
        if (string.IsNullOrEmpty(text)) return;

        var color = Colors.Black;
        var sb = new StringBuilder();

        void Flush()
        {
            if (sb.Length == 0) return;
            target.Inlines.Add(new Run(sb.ToString()) { Foreground = new SolidColorBrush(color) });
            sb.Clear();
        }

        for (int i = 0; i < text!.Length;)
        {
            char c = text[i];
            if (c == '^' && i + 6 < text.Length && IsHex6(text, i + 1))
            {
                Flush();
                color = (Color)ColorConverter.ConvertFromString("#" + text.Substring(i + 1, 6))!;
                i += 7;
            }
            else if (c == '\n') { Flush(); target.Inlines.Add(new LineBreak()); i++; }
            else if (c == '\r') { i++; }
            else { sb.Append(c); i++; }
        }
        Flush();
    }

    private static bool IsHex6(string s, int start)
    {
        for (int k = start; k < start + 6; k++)
            if (!Uri.IsHexDigit(s[k])) return false;
        return true;
    }
}
