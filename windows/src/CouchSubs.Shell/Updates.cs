// SPDX-License-Identifier: AGPL-3.0-only

using System.Reflection;

namespace CouchSubs.Shell;

/// <summary>Uygulama güncellemesi (gerçekte Velopack, GitHub sürümlerinden).</summary>
public interface IUpdater
{
    /// <summary>Kurulum dosyasıyla kurulduysa kendini günceller; zip'ten açıldıysa yalnız sürüm sayfasını gösteririz.</summary>
    bool CanUpdateItself { get; }
    Uri DownloadPage { get; }
    /// <summary>Daha yeni sürüm varsa numarası, yoksa null.</summary>
    Task<string?> CheckAsync();
    /// <summary>İndirir, kurar ve uygulamayı yeniden başlatır.</summary>
    Task InstallAsync(IProgress<double> progress);
}

/// <summary>
/// Güncellemelerin geldiği GitHub deposu. Derlemedeki <c>CouchSubsUpdateRepository</c> bilgisiyle seçilir (App projesinde
/// MSBuild özelliği): bu fork'ta varsayılan fork'un kendi deposudur; böylece uygulama resmî sürümü önermez, resmî sürüm
/// kurulup OBS kaynağı ile seçici kaybolmaz. "off" güncelleme denetimini kapatır.
/// </summary>
public static class UpdateChannel
{
    public const string Upstream = "https://github.com/yorulmazsinan/couchsubs";
    public const string MetadataKey = "CouchSubsUpdateRepository";

    /// <summary>
    /// Depo adresi (<c>https://github.com/sahip/ad</c>); kapalıysa ya da adres GitHub deposu değilse null (başka bir yere
    /// güncelleme sormaktansa hiç sormamak güvenlidir). Bilgi hiç yoksa resmî depo.
    /// </summary>
    public static string? Repository(string? configured)
    {
        var value = configured?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(value)) return Upstream;
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com"
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || !parts.All(part => part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            return null;
        return $"https://github.com/{parts[0]}/{parts[1]}";
    }

    public static string? Repository(Assembly? assembly) =>
        Repository(assembly?.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == MetadataKey)?.Value);

    /// <summary>Deponun en son sürümünü veren GitHub API adresi.</summary>
    public static string LatestReleaseApi(string repository) =>
        "https://api.github.com/repos/" + repository["https://github.com/".Length..] + "/releases/latest";
}

/// <summary>Sürüm numaraları: "0.1.0-beta.10" (anlamsal sürüm, ön sürüm parçaları sayıysa sayı olarak karşılaştırılır).</summary>
public static class AppVersion
{
    /// <summary>Derlemenin sürümü (CI etiketten verir: -p:Version=0.1.0-beta.10); "+commit" eki atılır.</summary>
    public static string Current(Assembly assembly) =>
        (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    public static int Compare(string a, string b)
    {
        var (coreA, preA) = Split(a);
        var (coreB, preB) = Split(b);
        for (var i = 0; i < Math.Max(coreA.Length, coreB.Length); i++)
        {
            var x = i < coreA.Length ? coreA[i] : 0;
            var y = i < coreB.Length ? coreB[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        // Ön sürümü olmayan, ön sürümlüden yenidir (0.1.0 > 0.1.0-beta.9).
        if (preA.Length == 0 || preB.Length == 0) return preB.Length.CompareTo(preA.Length);
        for (var i = 0; i < Math.Max(preA.Length, preB.Length); i++)
        {
            if (i >= preA.Length) return -1;
            if (i >= preB.Length) return 1;
            var numberA = int.TryParse(preA[i], out var na);
            var numberB = int.TryParse(preB[i], out var nb);
            var result = numberA && numberB ? na.CompareTo(nb)
                : numberA ? -1 : numberB ? 1 : string.CompareOrdinal(preA[i], preB[i]);
            if (result != 0) return Math.Sign(result);
        }
        return 0;
    }

    private static (int[] Core, string[] Pre) Split(string version)
    {
        var text = version.Trim().TrimStart('v').Split('+')[0];
        var dash = text.IndexOf('-');
        var core = (dash < 0 ? text : text[..dash]).Split('.').Select(part => int.TryParse(part, out var n) ? n : 0).ToArray();
        var pre = dash < 0 ? [] : text[(dash + 1)..].Split('.');
        return (core, pre);
    }
}
