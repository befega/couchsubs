// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CouchSubs.Core.Dubbing;

namespace CouchSubs.Core;

/// <summary>Seçicide yarışan bir motor: adı (günlük ve ayar için) ve çevirmeni.</summary>
public sealed record ConductorEngine(string Name, ITextTranslator Translator);

/// <summary>Bir motorun çevirisi. <paramref name="Rank"/>: eşitlikte öncelik (küçük olan önce).</summary>
public sealed record ConductorCandidate(string Engine, string Text, int Rank);

/// <summary>Seçilen çeviri ve neden seçildiği (günlük için).</summary>
public sealed record ConductorChoice(ConductorCandidate Winner, IReadOnlyList<(ConductorCandidate Candidate, double Score)> Scores);

/// <summary>
/// Birden çok motorun çevirisinden en iyisini seçer; yapay zekâ hakemi kullanılmaz, karar milisaniyenin altında verilir.
/// Önce bariz hatalar elenir ya da cezalandırılır (çevrilmeden dönen satır, çeviride kalan İngilizce kelimeler,
/// kaybolan özel adlar, tuhaf uzunluk, modelin açıklama ya da tekrar üretmesi). Kalanlar arasında diğer motorlarla en
/// çok örtüşen çeviri öne geçer: iki motorun benzer bulduğu çeviri genelde doğrudur. Eşitlikte motor önceliği bozar.
/// </summary>
public static class ConductorScoring
{
    /// <summary>Türkçede geçmeyen sık İngilizce kelimeler; kaynakta da varsa çeviride kalmış sayılır.</summary>
    private static readonly HashSet<string> EnglishWords = new(StringComparer.Ordinal)
    {
        "the", "and", "you", "your", "is", "are", "was", "were", "to", "of", "what", "with", "this", "that", "have",
        "has", "not", "don't", "i'm", "it's", "we", "they", "he", "she", "for", "just", "be", "do", "get", "me", "my",
        "there", "here", "can", "will", "would", "about", "from", "but", "so", "if", "know", "want", "need", "gonna",
        "it", "in", "on", "at", "all", "out", "up", "go", "come", "let's", "where", "why", "how", "who", "when",
    };

    /// <summary>Modelin çeviri yerine açıklama yazdığını gösteren başlangıçlar.</summary>
    private static readonly string[] Chatter = ["translation:", "çeviri:", "here is", "here's", "işte çeviri", "turkish:", "türkçe:"];

    public static ConductorChoice? Pick(string source, IReadOnlyList<ConductorCandidate> candidates)
    {
        var usable = candidates.Where(c => Penalty(source, c.Text) is not null).ToList();
        if (usable.Count == 0) return null;
        var scores = usable.Select(candidate =>
        {
            var others = usable.Where(other => !ReferenceEquals(other, candidate)).ToList();
            var consensus = others.Count == 0 ? 0 : others.Average(other => Similarity(candidate.Text, other.Text));
            return (Candidate: candidate, Score: 2 * consensus - Penalty(source, candidate.Text)!.Value - 0.05 * candidate.Rank);
        }).OrderByDescending(s => s.Score).ThenBy(s => s.Candidate.Rank).ToList();
        return new ConductorChoice(scores[0].Candidate, scores);
    }

    /// <summary>Çevirinin cezası (0 kusursuz); kullanılamazsa (boş, çevrilmemiş, yabancı alfabe) null.</summary>
    public static double? Penalty(string source, string translation)
    {
        var text = translation.Trim();
        if (text.Length == 0 || !text.Any(char.IsLetter)) return null;
        var sourceWords = Words(source);
        if (sourceWords.Count(w => w.Any(char.IsLetter)) >= 2 && TextMatch.Normalize(text) == TextMatch.Normalize(source)) return null;
        if (text.Any(c => char.IsLetter(c) && c > 'ɏ')) return null; // Latin dışı alfabe (ör. Kiril, Çince)

        var words = Words(text);
        var penalty = 0.0;
        // Kaynakta da geçen sık İngilizce kelime çeviride kalmışsa yarım çevrilmiştir.
        var sourceSet = sourceWords.ToHashSet(StringComparer.Ordinal);
        penalty += 0.5 * words.Count(w => EnglishWords.Contains(w) && sourceSet.Contains(w));
        // Cümle ortasındaki özel adlar (Franklin, Los Santos) çeviride de geçmeli; Türkçe ek alabilir (Franklin'e).
        foreach (var name in Names(source))
        {
            var stem = name[..Math.Min(name.Length, 5)];
            if (!words.Any(w => w.StartsWith(stem, StringComparison.Ordinal))) penalty += 0.6;
        }
        // Türkçe çeviri İngilizceden çok kısa ya da çok uzun olmaz.
        var ratio = (double)text.Length / Math.Max(1, source.Trim().Length);
        if (ratio < 0.5) penalty += (0.5 - ratio) * 4;
        if (ratio > 2.0) penalty += (ratio - 2.0) * 2;
        // Modelin açıklaması ya da fazladan satırı.
        var lower = text.ToLower(CultureInfo.InvariantCulture);
        if (Chatter.Any(lower.StartsWith) || text.Count(c => c == '\n') > source.Count(c => c == '\n')) penalty += 2;
        // Kendini tekrar eden çıktı (modelin döngüye girmesi).
        if (Repeats(words)) penalty += 1.5;
        return penalty;
    }

    /// <summary>İki çevirinin kelime köklerine göre benzerliği (0-1, Dice). Türkçe ekler için kelimenin ilk 5 harfi.</summary>
    public static double Similarity(string a, string b)
    {
        var left = Stems(a);
        var right = Stems(b);
        if (left.Count == 0 || right.Count == 0) return 0;
        var shared = left.Intersect(right).Count();
        return 2.0 * shared / (left.Count + right.Count);
    }

    private static HashSet<string> Stems(string text) =>
        Words(text).Where(w => w.Any(char.IsLetter)).Select(w => w[..Math.Min(w.Length, 5)]).ToHashSet(StringComparer.Ordinal);

    /// <summary>Küçük harfli kelimeler (Türkçe harflerle); noktalama atılır, kesme işareti kalır.</summary>
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text.ToLower(CultureInfo.GetCultureInfo("tr-TR")))
        {
            if (char.IsLetterOrDigit(c) || c is '\'' or '’') current.Append(c == '’' ? '\'' : c);
            else Flush();
        }
        Flush();
        return words;

        void Flush()
        {
            if (current.Length > 0) words.Add(current.ToString().Trim('\''));
            current.Clear();
        }
    }

    /// <summary>Cümle başında olmayan, büyük harfle başlayan kelimeler (özel ad sayılır), küçük harfle.</summary>
    private static IEnumerable<string> Names(string source)
    {
        var sentenceStart = true;
        foreach (var raw in source.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw.Trim('"', '\'', '(', ')', ',', ';', ':', '-', '—', '’');
            var ends = raw.EndsWith('.') || raw.EndsWith('!') || raw.EndsWith('?') || raw.EndsWith(':');
            word = word.TrimEnd('.', '!', '?');
            if (!sentenceStart && word.Length >= 3 && char.IsUpper(word[0]) && word.Skip(1).Any(char.IsLower)
                && !EnglishWords.Contains(word.ToLowerInvariant()))
                yield return word.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
            sentenceStart = ends || word.EndsWith(':');
        }
    }

    private static bool Repeats(List<string> words)
    {
        if (words.Count < 6) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 2 < words.Count; i++)
            if (!seen.Add($"{words[i]} {words[i + 1]} {words[i + 2]}")) return true;
        return false;
    }
}

/// <summary>
/// Seçici ("conductor"): satır kurulu motorların hepsine aynı anda gönderilir, <see cref="Deadline"/> dolana kadar gelen
/// çeviriler arasından en iyisi seçilir (<see cref="ConductorScoring"/>). Hepsi erken biterse beklenmez; süre dolduğunda
/// hiç çeviri gelmemişse ilk gelen beklenir. Geç kalan motor arka planda biter, çevirisi kendi önbelleğine girer (aynı
/// satır bir daha geldiğinde hazırdır). Bir motorun hatası satırı düşürmez; hepsi başarısızsa son hata fırlatılır.
/// </summary>
public sealed class ConductorTranslator : ITextTranslator
{
    /// <summary>
    /// Varsayılan bekleme: ücretsiz motor onlarca, DeepL ve yerel model (Gemma, 3090'da) birkaç yüz milisaniyede döner.
    /// Daha uzun bekleme altyazıyı geciktirir; daha kısası en iyi motoru çoğu zaman dışarıda bırakır.
    /// </summary>
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromMilliseconds(700);

    private readonly IReadOnlyList<ConductorEngine> _engines;
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<string, ConductorEngine> _winners = new(StringComparer.Ordinal);

    /// <param name="engines">Öncelik sırasıyla (eşitlikte öndeki seçilir).</param>
    /// <param name="log">Hangi motorun neden seçildiği (altyazı günlüğü).</param>
    public ConductorTranslator(IReadOnlyList<ConductorEngine> engines, TimeSpan? deadline = null, Action<string>? log = null)
    {
        if (engines.Count == 0) throw new ArgumentException("At least one engine is needed.", nameof(engines));
        _engines = engines;
        Deadline = deadline ?? DefaultDeadline;
        _log = log;
    }

    public TimeSpan Deadline { get; }

    public IReadOnlyList<ConductorEngine> Engines => _engines;

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var started = Stopwatch.GetTimestamp();
        var errors = new ConcurrentQueue<Exception>();
        var pending = _engines.Select((engine, rank) => RunAsync(engine, rank, text, errors, cancellationToken)).ToList();
        var done = new List<ConductorCandidate>();
        var deadline = Task.Delay(Deadline, cancellationToken);
        while (pending.Count > 0)
        {
            // Süre dolduysa ve elde çeviri varsa beklenmez; yoksa ilk gelen beklenir.
            Task next;
            if (deadline.IsCompleted && done.Count == 0) next = await Task.WhenAny(pending).ConfigureAwait(false);
            else next = await Task.WhenAny(pending.Cast<Task>().Append(deadline)).ConfigureAwait(false);
            if (next == deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (done.Count > 0) break;
                continue;
            }
            var finished = (Task<ConductorCandidate?>)next;
            pending.Remove(finished);
            if (finished.Result is { } candidate) done.Add(candidate);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var choice = ConductorScoring.Pick(text, done);
        if (choice is null)
        {
            if (errors.LastOrDefault() is { } error) throw error;
            throw new TranslatorException(TranslatorStatus.EngineFailed);
        }
        var winner = _engines[choice.Winner.Rank];
        if (_winners.Count >= 512) _winners.Clear();
        _winners[text] = winner;
        if (_log is not null && (choice.Scores.Count > 1 || _engines.Count > 1))
        {
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var late = _engines.Count - done.Count;
            var scores = string.Join(", ", choice.Scores.Select(s => $"{s.Candidate.Engine} {s.Score:0.00}"));
            _log($"seçici: {winner.Name} ({scores}{(late > 0 ? $"; {late} motor yetişmedi" : "")}; {elapsed} ms)");
        }
        return choice.Winner.Text;
    }

    private static async Task<ConductorCandidate?> RunAsync(ConductorEngine engine, int rank, string text,
                                                           ConcurrentQueue<Exception> errors, CancellationToken cancellationToken)
    {
        try
        {
            // Yavaş motor diğerlerini bekletmesin: her biri iş parçacığı havuzunda başlar.
            var translation = await Task.Run(() => engine.Translator.TranslateAsync(text, cancellationToken), cancellationToken).ConfigureAwait(false);
            return new ConductorCandidate(engine.Name, translation, rank);
        }
        catch (Exception error)
        {
            if (error is not OperationCanceledException) errors.Enqueue(error);
            return null;
        }
    }

    /// <summary>Dublaj, seçilen motorun cümlelerini okur.</summary>
    public IReadOnlyList<SpokenLine> SpokenLines(string text) =>
        (_winners.TryGetValue(text, out var winner) ? winner : _engines[0]).Translator.SpokenLines(text);

    /// <summary>"Ekranı çevir" çok satırlıdır ve acele değildir: öncelikli motor çevirir, olmazsa sıradaki.</summary>
    public bool TranslatesTogether => _engines[0].Translator.TranslatesTogether;

    public async Task<IReadOnlyList<string>> TranslateAllAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var engine in _engines)
        {
            try { return await engine.Translator.TranslateAllAsync(texts, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException) { last = error; }
        }
        throw last ?? new TranslatorException(TranslatorStatus.EngineFailed);
    }
}
