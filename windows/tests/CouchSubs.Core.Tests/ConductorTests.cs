// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using CouchSubs.Core;

namespace CouchSubs.Core.Tests;

public class ConductorScoringTests
{
    private static ConductorCandidate C(string engine, string text, int rank) => new(engine, text, rank);

    [Fact]
    public void UntranslatedLineLoses()
    {
        const string source = "I gotta tell you this, Franklin.";
        var choice = ConductorScoring.Pick(source, [C("DeepL", source, 0), C("OPUS-MT", "Bunu sana söylemem lazım, Franklin.", 1)]);
        Assert.Equal("OPUS-MT", choice!.Winner.Engine);
        Assert.Null(ConductorScoring.Penalty(source, source));
    }

    [Fact]
    public void LeftoverEnglishAndLostNamesArePenalized()
    {
        const string source = "Take the car to Lamar, you know what to do.";
        var good = ConductorScoring.Penalty(source, "Arabayı Lamar'a götür, ne yapacağını biliyorsun.")!.Value;
        var leftover = ConductorScoring.Penalty(source, "Arabayı the Lamar'a götür, you know ne yapacağını.")!.Value;
        var lostName = ConductorScoring.Penalty(source, "Arabayı ona götür, ne yapacağını biliyorsun.")!.Value;
        Assert.True(good < leftover);
        Assert.True(good < lostName);
    }

    [Fact]
    public void ModelChatterRepetitionAndForeignScriptsLose()
    {
        const string source = "Are you listening to me?";
        Assert.True(ConductorScoring.Penalty(source, "Translation: Beni dinliyor musun?") > ConductorScoring.Penalty(source, "Beni dinliyor musun?"));
        Assert.True(ConductorScoring.Penalty(source, "Dinle beni dinle beni dinle beni dinle beni") > 1);
        Assert.Null(ConductorScoring.Penalty(source, "Ты меня слушаешь?"));
        Assert.Null(ConductorScoring.Penalty(source, "  "));
    }

    [Fact]
    public void TheTranslationTheEnginesAgreeOnWins()
    {
        const string source = "We need to get out of here before the cops show up.";
        var choice = ConductorScoring.Pick(source,
        [
            C("DeepL", "Polisler gelmeden buradan çıkmamız gerek.", 0),
            C("gemma3:12b", "Polisler gelmeden önce buradan çıkmamız lazım.", 1),
            C("OPUS-MT", "Polis şovundan önce buradan çıkmalıyız.", 2),
        ]);
        Assert.NotEqual("OPUS-MT", choice!.Winner.Engine);
        Assert.Equal(3, choice.Scores.Count);
    }

    [Fact]
    public void PriorityBreaksTies()
    {
        var choice = ConductorScoring.Pick("Hello.", [C("OPUS-MT", "Merhaba.", 2), C("DeepL", "Merhaba.", 0)]);
        Assert.Equal("DeepL", choice!.Winner.Engine);
        Assert.Null(ConductorScoring.Pick("Hello.", []));
    }

    [Fact]
    public void NamesWithTurkishSuffixesCount()
    {
        const string source = "Give it to Michael.";
        Assert.Equal(0, ConductorScoring.Penalty(source, "Onu Michael'a ver.")!.Value, 3);
    }
}

public class ConductorTranslatorTests
{
    /// <summary>Verilen sürede verilen çeviriyi döndüren ya da hata fırlatan motor.</summary>
    private sealed class FakeEngine(Func<string, string> translate, int delayMs = 0) : ITextTranslator
    {
        public ConcurrentQueue<string> Seen { get; } = new();
        public int TranslateAllCalls;

        public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
        {
            Seen.Enqueue(text);
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
            return translate(text);
        }

        public Task<IReadOnlyList<string>> TranslateAllAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref TranslateAllCalls);
            return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(translate)]);
        }
    }

    [Fact]
    public async Task EveryEngineGetsTheLineAndTheBestWins()
    {
        var deepL = new FakeEngine(_ => "Bunu sana söylemem lazım, Franklin.");
        var local = new FakeEngine(_ => "Bunu sana söylemeliyim, Franklin.");
        var broken = new FakeEngine(text => text);
        var logs = new List<string>();
        var conductor = new ConductorTranslator([new("DeepL", deepL), new("gemma", local), new("OPUS-MT", broken)], log: logs.Add);

        var result = await conductor.TranslateAsync("I gotta tell you this, Franklin.");
        Assert.NotEqual("I gotta tell you this, Franklin.", result);
        Assert.All(new[] { deepL, local, broken }, engine => Assert.Single(engine.Seen));
        Assert.Contains("seçici:", Assert.Single(logs));
    }

    [Fact]
    public async Task SlowEnginesAreNotAwaitedPastTheDeadline()
    {
        var fast = new FakeEngine(_ => "Merhaba.");
        var slow = new FakeEngine(_ => "Selam.", delayMs: 3000);
        var conductor = new ConductorTranslator([new("slow", slow), new("fast", fast)], deadline: TimeSpan.FromMilliseconds(100));
        var started = DateTime.UtcNow;
        Assert.Equal("Merhaba.", await conductor.TranslateAsync("Hello."));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WhenNothingArrivesInTimeTheFirstAnswerIsAwaited()
    {
        var slow = new FakeEngine(_ => "Selam.", delayMs: 250);
        var slower = new FakeEngine(_ => "Merhaba.", delayMs: 3000);
        var conductor = new ConductorTranslator([new("slower", slower), new("slow", slow)], deadline: TimeSpan.FromMilliseconds(50));
        Assert.Equal("Selam.", await conductor.TranslateAsync("Hello."));
    }

    [Fact]
    public async Task AllEnginesFinishingEarlyIsNotDelayed()
    {
        var conductor = new ConductorTranslator([new("a", new FakeEngine(_ => "Merhaba.")), new("b", new FakeEngine(_ => "Merhaba!"))],
                                                deadline: TimeSpan.FromSeconds(5));
        var started = DateTime.UtcNow;
        await conductor.TranslateAsync("Hello.");
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AFailingEngineDropsOutAndAllFailingThrows()
    {
        var failing = new FakeEngine(_ => throw new TranslatorException(TranslatorStatus.NeedsDownload));
        var working = new FakeEngine(_ => "Merhaba.");
        Assert.Equal("Merhaba.", await new ConductorTranslator([new("x", failing), new("y", working)]).TranslateAsync("Hello."));

        var error = await Assert.ThrowsAsync<TranslatorException>(() =>
            new ConductorTranslator([new("x", failing), new("z", failing)]).TranslateAsync("Hello."));
        Assert.Equal(TranslatorStatus.NeedsDownload, error.Status);
    }

    [Fact]
    public async Task CancellationStopsTheLine()
    {
        var slow = new FakeEngine(_ => "Selam.", delayMs: 3000);
        using var cancel = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConductorTranslator([new("a", slow), new("b", slow)]).TranslateAsync("Hello.", cancel.Token));
    }

    [Fact]
    public async Task ScreenTranslationUsesThePreferredEngine()
    {
        var first = new FakeEngine(_ => "bir");
        var second = new FakeEngine(_ => "iki");
        var conductor = new ConductorTranslator([new("first", first), new("second", second)]);
        Assert.Equal(["bir", "bir"], await conductor.TranslateAllAsync(["a", "b"]));
        Assert.Equal(1, first.TranslateAllCalls);
        Assert.Equal(0, second.TranslateAllCalls);
    }
}
