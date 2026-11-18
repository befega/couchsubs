// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CouchSubs.Core;
using CouchSubs.Core.Dubbing;

namespace CouchSubs.Translation;

/// <summary>
/// İsteğe bağlı çevrimiçi çeviri: kullanıcının kendi DeepL anahtarıyla. Altyazı cümle cümle, konuşmacı adları
/// çevrilmeden gönderilir. Son gösterilen 3 satır bağlam olarak eklenir: DeepL'in <c>context</c> alanı (tek metin)
/// çeviriyi etkiler ama çevrilmez ve ücretlendirilmez ("Characters included in the context parameter will not be
/// counted toward billing", developers.deepl.com/docs/api-reference/translate, Kasım 2026). Önbellekteki çeviri
/// bağlamsız eski haliyle döner; anahtar yalnız satırın kendisidir.
/// DeepL'e ulaşılamazsa ya da kota biterse o satır cihaz üstü modelle çevrilir; oyun hiç altyazısız kalmaz.
/// </summary>
public sealed class DeepLTranslator : ITextTranslator, IOnlineTranslator
{
    /// <summary>Kalıcı önbellek dosyasındaki motor adı.</summary>
    public const string EngineId = "deepl";
    /// <summary>Bağlama giden önceki satır sayısı.</summary>
    internal const int ContextLines = 3;
    /// <summary>Hata sonrası bu süre DeepL denenmez; her satırda bağlantı zaman aşımını beklemeyelim.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Bağlantı boşta 5 dakika açık kalır (varsayılan 1 dakikaydı): diyalogsuz bir sahneden sonraki ilk satırda bağlantı ve
    /// TLS yeniden kurulmasın; bu, satıra birkaç yüz milisaniye ekliyordu. Ayrıca bkz. <see cref="KeepWarm"/>.
    /// </summary>
    private static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient(new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(30),
    }) { Timeout = TimeSpan.FromSeconds(4) });

    /// <summary>Çeviri sürerken bağlantı bu aralıkla yoklanır (sunucu tarafı boştaki bağlantıyı da kapatabilir).</summary>
    internal static readonly TimeSpan WarmInterval = TimeSpan.FromSeconds(40);
    /// <summary>Bu kadar süre hiç satır gelmezse yoklama durur (çeviri durmuş ya da oyun kapalı).</summary>
    internal static readonly TimeSpan WarmFor = TimeSpan.FromMinutes(10);

    private readonly string _key;
    private readonly ITextTranslator? _fallback;
    private readonly HttpClient _http;
    private readonly TranslationCache _cache;
    private readonly ConcurrentDictionary<string, IReadOnlyList<SpokenLine>> _speech = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.Ordinal);
    private readonly List<string> _recent = [];
    /// <summary>Çevrilmeyecek adlar ve terimler (ücretsiz çeviriyle aynı sözlük); DeepL'e etiketle korunarak gider.</summary>
    private readonly GlossaryStore _glossary;
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
    private readonly bool _keepWarm;
    private long _lastUse = Environment.TickCount64;
    private long _lastRequest;
    private int _warming;

    /// <param name="cache">Kalıcı önbellek; verilmezse yalnız bellekte tutulur.</param>
    /// <param name="glossary">Verilmezse kullanıcının sözlük klasörü (ücretsiz çeviriyle aynı).</param>
    /// <param name="keepWarm">Bağlantı açık tutulsun mu (bkz. <see cref="KeepWarm"/>); verilmezse yalnız ortak istemcide.</param>
    public DeepLTranslator(string key, ITextTranslator? fallback, HttpClient? http = null, TranslationCache? cache = null,
        GlossaryStore? glossary = null, bool? keepWarm = null)
    {
        _key = key.Trim();
        _fallback = fallback;
        _http = http ?? SharedClient.Value;
        _cache = cache ?? new TranslationCache();
        _glossary = glossary ?? new GlossaryStore(GlossaryStore.UserDirectory);
        _keepWarm = keepWarm ?? http is null;
        // Çeviri başlarken bağlantı hemen kurulur; ilk altyazı TLS el sıkışmasını beklemez.
        KeepWarm();
    }

    /// <summary>
    /// "HTTP isteğini uyanık tutmak": çeviri sürerken DeepL'e açık bağlantı hazır bekler. Son satırdan bu yana
    /// <see cref="WarmFor"/> geçmediyse ve <see cref="WarmInterval"/> boyunca hiç istek gitmediyse kullanım sorgusu
    /// (<c>/v2/usage</c>) gönderilir; bu sorgu karakter kotasından düşmez. Hata önemsizdir, yoklama sessizce sürer.
    /// </summary>
    private void KeepWarm()
    {
        Interlocked.Exchange(ref _lastUse, Environment.TickCount64);
        if (!_keepWarm || Interlocked.Exchange(ref _warming, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await PingAsync().ConfigureAwait(false);
                while (Environment.TickCount64 - Interlocked.Read(ref _lastUse) < WarmFor.TotalMilliseconds)
                {
                    await Task.Delay(WarmInterval).ConfigureAwait(false);
                    if (Environment.TickCount64 - Interlocked.Read(ref _lastRequest) >= WarmInterval.TotalMilliseconds)
                        await PingAsync().ConfigureAwait(false);
                }
            }
            finally { Volatile.Write(ref _warming, 0); }
        });
    }

    private async Task PingAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri(_key), "usage"));
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _key);
            Interlocked.Exchange(ref _lastRequest, Environment.TickCount64);
            using var response = await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
    }

    /// <summary>Son DeepL hatası; çeviri düzgün gidiyorsa null.</summary>
    public OnlineKeyState? LastError { get; private set; }

    public event Action<OnlineKeyState?>? FailureChanged;

    private void Report(OnlineKeyState? state)
    {
        if (LastError == state) return;
        LastError = state;
        FailureChanged?.Invoke(state);
    }

    /// <summary>Ücretsiz anahtarlar ":fx" ile biter ve ayrı adrese gider.</summary>
    internal static Uri BaseUri(string key) =>
        new(key.Trim().EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com/v2/" : "https://api.deepl.com/v2/");

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        KeepWarm();
        // Bağlam, bu satırdan önce gösterilenlerdir; satırın kendisi bağlama girmez.
        var context = Context();
        Shown(text);
        var glossary = _glossary.Current;
        if (_cache.TryGet(DeepLMarkup.CacheKey(text, glossary), out var cached)) return cached;
        if (DateTimeOffset.UtcNow < _pausedUntil) return await FallbackAsync(text, cancellationToken).ConfigureAwait(false);

        var pieces = SubtitleSegmenter.Split(text);
        foreach (var name in pieces.Select(SubtitleSegmenter.LabelName).OfType<string>())
            foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2))
                _names[word.ToLowerInvariant()] = 0;
        var names = _names.Keys.ToHashSet(StringComparer.Ordinal);
        names.UnionWith(glossary.CapitalWords);
        // Önbellekte duran cümleler yeniden gönderilmez; kota yalnız yeni cümlelere gider.
        var translations = pieces.Select(p => p.Translate && _cache.TryGet(DeepLMarkup.CacheKey(p.Text, glossary), out var known) ? known : null).ToList();
        var missing = Enumerable.Range(0, pieces.Count).Where(i => pieces[i].Translate && translations[i] is null).ToList();
        var plain = missing.Select(i => SubtitleSegmenter.NormalizeCase(pieces[i].Text, names)).ToList();
        // Sözlükteki adlar etiketle korunur; etiket varsa istek XML olarak gider, diğer cümleler de kaçırılır.
        var wrapped = plain.Select(p => DeepLMarkup.Wrap(p, glossary)).ToList();
        var tagged = wrapped.Any(w => w is not null);
        var sources = tagged ? plain.Select((p, i) => wrapped[i] ?? DeepLMarkup.Escape(p)).ToList() : plain;

        string[] translated;
        try
        {
            var reply = sources.Count == 0 ? [] : await RequestAsync(sources, context, tagged, cancellationToken).ConfigureAwait(false);
            translated = tagged ? [.. reply.Select(DeepLMarkup.Unwrap)] : reply;
            Report(null);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DeepLException
                                      && !cancellationToken.IsCancellationRequested)
        {
            Report(error is DeepLException deepL ? deepL.State : OnlineKeyState.Unreachable);
            _pausedUntil = DateTimeOffset.UtcNow + RetryAfter;
            return await FallbackAsync(text, cancellationToken).ConfigureAwait(false);
        }
        for (var i = 0; i < missing.Count; i++)
        {
            translations[missing[i]] = translated[i];
            _cache.Store(DeepLMarkup.CacheKey(pieces[missing[i]].Text, glossary), translated[i]);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < pieces.Count; i++) builder.Append(translations[i] ?? pieces[i].Text);
        if (_speech.Count >= 512) _speech.Clear();
        _speech[text] = SpeechScript.Lines(pieces, translations);
        var result = builder.ToString();
        _cache.Store(DeepLMarkup.CacheKey(text, glossary), result);
        return result;
    }

    /// <summary>Son gösterilen satırlar, eskiden yeniye; DeepL'e bağlam olarak gider.</summary>
    private string? Context()
    {
        lock (_recent) return _recent.Count == 0 ? null : string.Join("\n", _recent);
    }

    private void Shown(string text)
    {
        lock (_recent)
        {
            // Aynı satır yeniden okunduysa bağlam tekrarla dolmasın.
            if (_recent.Count > 0 && _recent[^1] == text) return;
            _recent.Add(text);
            if (_recent.Count > ContextLines) _recent.RemoveAt(0);
        }
    }

    public IReadOnlyList<SpokenLine> SpokenLines(string text) =>
        _speech.TryGetValue(text, out var lines) ? lines
        : SpeechScript.FromCache(text, _cache) is { Count: > 0 } cached ? cached
        : _fallback?.SpokenLines(text) ?? [];

    private async Task<string> FallbackAsync(string text, CancellationToken cancellationToken) =>
        _fallback is null ? throw new TranslatorException(TranslatorStatus.NeedsDownload)
            : await _fallback.TranslateAsync(text, cancellationToken).ConfigureAwait(false);

    private async Task<string[]> RequestAsync(List<string> sentences, string? context, bool tagged, CancellationToken cancellationToken)
    {
        // Oyun konuşması: "siz" değil "sen" (DeepL Türkçede desteklemiyorsa yok sayar).
        var body = new TranslateRequest(sentences, "EN", "TR", context, "prefer_less", true,
            tagged ? "xml" : null, tagged ? [DeepLMarkup.Tag] : null);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri(_key), "translate"))
        {
            Content = JsonContent.Create(body, DeepLJson.Default.TranslateRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _key);
        Interlocked.Exchange(ref _lastRequest, Environment.TickCount64);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(response);
        var reply = await response.Content.ReadFromJsonAsync(DeepLJson.Default.TranslateResponse, cancellationToken).ConfigureAwait(false);
        if (reply?.Translations is not { } list || list.Count != sentences.Count) throw new DeepLException(OnlineKeyState.Unreachable);
        return [.. list.Select(t => t.Text)];
    }

    /// <summary>Ayarlardaki "Dene" düğmesi: anahtar geçerli mi, bu ay ne kadar kullanıldı.</summary>
    public static async Task<OnlineKeyCheck> CheckAsync(string key, HttpClient? http = null, CancellationToken cancellationToken = default)
    {
        key = key.Trim();
        if (key.Length == 0) return new(OnlineKeyState.Invalid);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri(key), "usage"));
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
            using var response = await (http ?? SharedClient.Value).SendAsync(request, cancellationToken).ConfigureAwait(false);
            ThrowIfFailed(response);
            var usage = await response.Content.ReadFromJsonAsync(DeepLJson.Default.UsageResponse, cancellationToken).ConfigureAwait(false);
            if (usage is null) return new(OnlineKeyState.Unreachable);
            return new(usage.CharacterLimit > 0 && usage.CharacterCount >= usage.CharacterLimit ? OnlineKeyState.QuotaExceeded : OnlineKeyState.Valid,
                usage.CharacterCount, usage.CharacterLimit);
        }
        catch (DeepLException error) { return new(error.State); }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new(OnlineKeyState.Unreachable);
        }
    }

    private static void ThrowIfFailed(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new DeepLException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => OnlineKeyState.Invalid,
            (HttpStatusCode)456 => OnlineKeyState.QuotaExceeded,
            _ => OnlineKeyState.Unreachable,
        });
    }
}

internal sealed class DeepLException(OnlineKeyState state) : Exception($"DeepL: {state}")
{
    public OnlineKeyState State { get; } = state;
}

internal sealed record TranslateRequest(
    [property: JsonPropertyName("text")] IReadOnlyList<string> Text,
    [property: JsonPropertyName("source_lang")] string SourceLang,
    [property: JsonPropertyName("target_lang")] string TargetLang,
    [property: JsonPropertyName("context"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context,
    [property: JsonPropertyName("formality")] string Formality,
    [property: JsonPropertyName("preserve_formatting")] bool PreserveFormatting,
    [property: JsonPropertyName("tag_handling"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TagHandling = null,
    [property: JsonPropertyName("ignore_tags"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? IgnoreTags = null);

internal sealed record TranslateResponse([property: JsonPropertyName("translations")] IReadOnlyList<TranslatedText>? Translations);

internal sealed record TranslatedText([property: JsonPropertyName("text")] string Text);

internal sealed record UsageResponse(
    [property: JsonPropertyName("character_count")] long CharacterCount,
    [property: JsonPropertyName("character_limit")] long CharacterLimit);

[JsonSerializable(typeof(TranslateRequest))]
[JsonSerializable(typeof(TranslateResponse))]
[JsonSerializable(typeof(UsageResponse))]
internal sealed partial class DeepLJson : JsonSerializerContext;
