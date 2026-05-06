// Styles/DesignSystem.cs
// Centralized design tokens — same values as the macOS DesignSystem.swift.
// All UI references DS.Colors.* and DS.Spacing.* instead of hardcoding values.

using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ClickyWindows;

internal static class DS
{
    internal static class Colors
    {
        public static readonly Color Background       = ColorFromHex("#101211");
        public static readonly Color Surface1         = ColorFromHex("#171918");
        public static readonly Color Surface2         = ColorFromHex("#202221");
        public static readonly Color Surface3         = ColorFromHex("#272A29");
        public static readonly Color Surface4         = ColorFromHex("#2E3130");
        public static readonly Color BorderSubtle     = ColorFromHex("#373B39");
        public static readonly Color BorderStrong     = ColorFromHex("#444947");
        public static readonly Color TextPrimary      = ColorFromHex("#ECEEED");
        public static readonly Color TextSecondary    = ColorFromHex("#ADB5B2");
        public static readonly Color TextTertiary     = ColorFromHex("#6B736F");
        public static readonly Color Accent           = ColorFromHex("#2563eb");
        public static readonly Color AccentHover      = ColorFromHex("#1d4ed8");
        public static readonly Color AccentText       = ColorFromHex("#60a5fa");
        public static readonly Color Success          = ColorFromHex("#34D399");
        public static readonly Color Warning          = ColorFromHex("#FFB224");
        public static readonly Color Destructive      = ColorFromHex("#E5484D");
        public static readonly Color OverlayCursorBlue = ColorFromHex("#3380FF");

        private static Color ColorFromHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromArgb(255, r, g, b);
        }

        // Brush helpers for XAML binding
        public static SolidColorBrush BackgroundBrush
            => new(Background);
        public static SolidColorBrush TextPrimaryBrush
            => new(TextPrimary);
        public static SolidColorBrush TextSecondaryBrush
            => new(TextSecondary);
        public static SolidColorBrush AccentBrush
            => new(Accent);
        public static SolidColorBrush BorderSubtleBrush
            => new(BorderSubtle);
    }

    internal static class Spacing
    {
        public const double XS = 4;
        public const double SM = 8;
        public const double MD = 12;
        public const double LG = 16;
        public const double XL = 20;
        public const double XXL = 24;
    }

    internal static class CornerRadius
    {
        public const double Small  = 6;
        public const double Medium = 8;
        public const double Large  = 10;
        public const double Panel  = 12;
    }
}
