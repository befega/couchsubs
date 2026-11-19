// SPDX-License-Identifier: AGPL-3.0-only

using CouchSubs.Shell;
using Xunit;

namespace CouchSubs.Shell.Tests;

/// <summary>Fork derlemesi resmî sürümü önermesin: güncelleme deposu derlemeden gelir, "off" kapatır.</summary>
public class UpdateChannelTests
{
    [Theory]
    [InlineData(null, UpdateChannel.Upstream)]
    [InlineData("", UpdateChannel.Upstream)]
    [InlineData("https://github.com/befega/couchsubs", "https://github.com/befega/couchsubs")]
    [InlineData(" https://github.com/befega/couchsubs/ ", "https://github.com/befega/couchsubs")]
    [InlineData("off", null)]
    [InlineData("OFF", null)]
    [InlineData("none", null)]
    [InlineData("http://github.com/befega/couchsubs", null)]
    [InlineData("https://example.com/befega/couchsubs", null)]
    [InlineData("https://github.com/befega", null)]
    [InlineData("https://github.com/befega/couchsubs/releases", null)]
    [InlineData("https://github.com/befega/couch subs", null)]
    [InlineData("https://github.com/befega/couchsubs?x=1", null)]
    public void RepositoryComesFromTheBuild(string? configured, string? expected) =>
        Assert.Equal(expected, UpdateChannel.Repository(configured));

    [Fact]
    public void LatestReleaseAsksTheChosenRepository() =>
        Assert.Equal("https://api.github.com/repos/befega/couchsubs/releases/latest",
                     UpdateChannel.LatestReleaseApi("https://github.com/befega/couchsubs"));
}
