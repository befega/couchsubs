// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.Http;
using CouchSubs.Shell;
using Velopack;
using Velopack.Sources;

namespace CouchSubs.App.Services;

/// <summary>
/// GitHub'daki sürümlerden güncelleme (Velopack). Yalnız kurulum dosyasıyla (CouchSubs-Setup.exe) kurulan uygulama
/// kendini günceller; zip'ten açılan eski kopyada sürüm sayfası açılır. Depo derlemeden gelir (<see cref="UpdateChannel"/>);
/// kapalıysa güncelleme hiç sorulmaz.
/// </summary>
internal sealed class Updater : IUpdater
{
    private readonly string? repository = UpdateChannel.Repository(typeof(Updater).Assembly);
    private readonly UpdateManager? manager;
    private UpdateInfo? pending;

    public Updater()
    {
        if (repository is not null) manager = new UpdateManager(new GithubSource(repository, null, false));
        DownloadPage = new($"{repository ?? UpdateChannel.Upstream}/releases/latest");
    }

    public bool CanUpdateItself => manager?.IsInstalled == true;
    public Uri DownloadPage { get; }

    public async Task<string?> CheckAsync()
    {
        if (repository is null || manager is null) return null;
        if (!manager.IsInstalled) return await LatestTagAsync(repository);
        pending = await manager.CheckForUpdatesAsync();
        return pending?.TargetFullRelease.Version.ToString();
    }

    public async Task InstallAsync(IProgress<double> progress)
    {
        if (pending is null || manager is null) return;
        await manager.DownloadUpdatesAsync(pending, percent => progress.Report(percent / 100.0));
        manager.ApplyUpdatesAndRestart(pending);
    }

    /// <summary>Zip kullanıcısı için: GitHub'daki en son sürümün etiketi, bu sürümden yeniyse.</summary>
    private static async Task<string?> LatestTagAsync(string repository)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CouchSubs");
            using var document = System.Text.Json.JsonDocument.Parse(
                await http.GetStringAsync(UpdateChannel.LatestReleaseApi(repository)));
            var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
            return tag is not null && AppVersion.IsNewer(tag, AppVersion.Current(typeof(Updater).Assembly)) ? tag : null;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                          or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
