// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;
using CouchSubs.Core;
using Xunit;

namespace CouchSubs.Shell.Tests;

/// <summary>Yayın görünümü: boşsa TV'ninki kullanılır; ayar dosyasında yalnız seçilen görünüm saklanır.</summary>
public class StreamStyleTests
{
    [Fact]
    public void StreamFollowsTheTvUntilASeparateStyleIsChosen()
    {
        var settings = new UserSettings { Style = new SubtitleStyle(FontSize: 40) };
        Assert.Same(settings.Style, settings.StreamStyle);
        var separate = settings with { ObsStyle = new SubtitleStyle(FontSize: 64, TextColor: SubtitleTextColor.Orange) };
        Assert.Equal(64, separate.StreamStyle.FontSize);
        Assert.Equal(40, separate.Style.FontSize);
    }

    /// <summary>Ayar dosyası (FileSettingsStore) varsayılan JSON seçenekleriyle yazılır; eski dosyada alan yoktur.</summary>
    [Fact]
    public void SettingsFileKeepsTheStreamStyle()
    {
        var settings = new UserSettings { ObsStyle = new SubtitleStyle(FontSize: 64, TextColor: SubtitleTextColor.Cyan) };
        var json = JsonSerializer.Serialize(settings);
        Assert.DoesNotContain("StreamStyle", json);
        Assert.Equal(settings.ObsStyle, JsonSerializer.Deserialize<UserSettings>(json)!.ObsStyle);
        Assert.Null(JsonSerializer.Deserialize<UserSettings>("{}")!.ObsStyle);
    }
}
