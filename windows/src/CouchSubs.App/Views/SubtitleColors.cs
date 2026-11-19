// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows.Media;
using CouchSubs.Core;

namespace CouchSubs.App.Views;

/// <summary>Altyazı yazı renkleri; OBS katmanındaki (obs-overlay.html TEXT_COLORS) renklerle aynı.</summary>
internal static class SubtitleColors
{
    public static Color Of(SubtitleTextColor color) => color switch
    {
        SubtitleTextColor.Yellow => Color.FromRgb(0xFF, 0xE1, 0x35),
        SubtitleTextColor.Cyan => Color.FromRgb(0x5C, 0xE1, 0xFF),
        SubtitleTextColor.Green => Color.FromRgb(0x7C, 0xFF, 0x8A),
        SubtitleTextColor.Orange => Color.FromRgb(0xFF, 0xA8, 0x3D),
        _ => Colors.White,
    };

    public static SolidColorBrush Brush(SubtitleTextColor color) => new(Of(color));
}
