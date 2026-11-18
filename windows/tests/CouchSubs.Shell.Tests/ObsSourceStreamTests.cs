// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using CouchSubs.Core;
using CouchSubs.RemotePlay;
using CouchSubs.Shell;

namespace CouchSubs.Shell.Tests;

/// <summary>Sırayla verilen görüntüleri ya da hataları döndüren sahte OBS bağlantısı.</summary>
internal sealed class FakeObsConnection(Func<string, byte[]> screenshot) : IObsConnection
{
    public int Screenshots;
    public bool Disposed { get; private set; }
    public Task<IReadOnlyList<ObsInput>> InputsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ObsInput>>([]);
    public Task<byte[]> ScreenshotAsync(string sourceName, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Screenshots);
        return Task.FromResult(screenshot(sourceName));
    }
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

public class ObsSourceStreamTests
{
    private static readonly ObsSourceTarget Target = new("localhost:4455", "Capture Card");
    private static readonly Localizer English = Localizer.ForLanguage("en");

    private static Frame? Decode(byte[] bytes) => bytes.Length == 0 ? null : Frame.Blank(bytes[0], 1);

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    [Fact]
    public async Task DeliversFramesFromTheSelectedSource()
    {
        var sources = new ConcurrentQueue<string>();
        var connection = new FakeObsConnection(name => { sources.Enqueue(name); return [16]; });
        var states = new ConcurrentQueue<SessionState>();
        var frames = new ConcurrentQueue<Frame>();
        using var stream = new ObsSourceStream(Target, () => null, Decode, (_, _, _) => Task.FromResult<IObsConnection>(connection),
                                               frameInterval: TimeSpan.FromMilliseconds(5), text: English);
        stream.StateChanged += states.Enqueue;
        stream.FrameReceived += frames.Enqueue;

        stream.Start();
        await Until(() => frames.Count >= 3);
        Assert.Equal(SessionState.Connecting, states.First());
        Assert.Contains(SessionState.Streaming, states);
        Assert.Single(states, SessionState.Streaming); // aynı durum tekrar bildirilmez
        Assert.All(sources, name => Assert.Equal("Capture Card", name));
        Assert.Equal(16, frames.First().Width);
    }

    [Fact]
    public async Task UnreadableImagesAreSkipped()
    {
        var calls = 0;
        var connection = new FakeObsConnection(_ => Interlocked.Increment(ref calls) % 2 == 1 ? [] : [8]);
        var frames = new ConcurrentQueue<Frame>();
        using var stream = new ObsSourceStream(Target, () => null, Decode, (_, _, _) => Task.FromResult<IObsConnection>(connection),
                                               frameInterval: TimeSpan.FromMilliseconds(5), text: English);
        stream.FrameReceived += frames.Enqueue;
        stream.Start();
        await Until(() => frames.Count >= 2);
        Assert.True(calls >= 4);
    }

    [Fact]
    public async Task MissingSourceIsReportedAndRetriedWithTheCurrentPassword()
    {
        var passwords = new ConcurrentQueue<string?>();
        var password = "old";
        var attempt = 0;
        var states = new ConcurrentQueue<SessionState>();
        var frames = new ConcurrentQueue<Frame>();
        using var stream = new ObsSourceStream(Target, () => password, Decode, (_, secret, _) =>
        {
            passwords.Enqueue(secret);
            return Interlocked.Increment(ref attempt) == 1
                ? Task.FromResult<IObsConnection>(new FakeObsConnection(_ => throw new ObsException(ObsFailure.SourceMissing)))
                : Task.FromResult<IObsConnection>(new FakeObsConnection(_ => [4]));
        }, frameInterval: TimeSpan.FromMilliseconds(5), retryDelay: TimeSpan.FromMilliseconds(30), text: English);
        stream.StateChanged += states.Enqueue;
        stream.FrameReceived += frames.Enqueue;

        stream.Start();
        await Until(() => states.Any(s => s.Reason?.Kind == DisconnectKind.NotFound));
        password = "new";
        await Until(() => !frames.IsEmpty);
        var missing = states.First(s => s.Reason?.Kind == DisconnectKind.NotFound);
        Assert.Contains("Capture Card", missing.Reason!.Detail);
        Assert.Equal(["old", "new"], passwords.Take(2));
    }

    [Fact]
    public async Task UnreachableObsIsRetriedUntilItOpens()
    {
        var attempt = 0;
        var states = new ConcurrentQueue<SessionState>();
        var frames = new ConcurrentQueue<Frame>();
        using var stream = new ObsSourceStream(Target, () => null, Decode, (_, _, _) =>
            Interlocked.Increment(ref attempt) < 3
                ? Task.FromException<IObsConnection>(new ObsException(ObsFailure.Unreachable))
                : Task.FromResult<IObsConnection>(new FakeObsConnection(_ => [2])),
            frameInterval: TimeSpan.FromMilliseconds(5), retryDelay: TimeSpan.FromMilliseconds(10), text: English);
        stream.StateChanged += states.Enqueue;
        stream.FrameReceived += frames.Enqueue;

        stream.Start();
        await Until(() => !frames.IsEmpty);
        var down = states.First(s => s.Phase == SessionPhase.Disconnected);
        Assert.Equal(DisconnectKind.Network, down.Reason!.Kind);
        Assert.Contains("localhost:4455", down.Reason.Detail);
        Assert.Contains(SessionState.Streaming, states);
    }

    [Fact]
    public async Task StopEndsTheLoopAndClosesTheConnection()
    {
        var connection = new FakeObsConnection(_ => [2]);
        var states = new ConcurrentQueue<SessionState>();
        var stream = new ObsSourceStream(Target, () => null, Decode, (_, _, _) => Task.FromResult<IObsConnection>(connection),
                                         frameInterval: TimeSpan.FromMilliseconds(5), text: English);
        stream.StateChanged += states.Enqueue;
        stream.Start();
        await Until(() => connection.Screenshots >= 2);

        stream.Stop();
        await Until(() => connection.Disposed);
        var after = connection.Screenshots;
        await Task.Delay(50);
        Assert.InRange(connection.Screenshots, after, after + 1);
        Assert.Equal(DisconnectKind.Stopped, states.Last().Reason?.Kind);
    }

    [Fact]
    public async Task InvalidAddressIsReportedWithoutConnecting()
    {
        var connects = 0;
        var states = new ConcurrentQueue<SessionState>();
        using var stream = new ObsSourceStream(new ObsSourceTarget("wss://nope", "Capture"), () => null, Decode, (_, _, _) =>
        {
            Interlocked.Increment(ref connects);
            return Task.FromResult<IObsConnection>(new FakeObsConnection(_ => [1]));
        }, text: English);
        stream.StateChanged += states.Enqueue;
        stream.Start();
        await Until(() => states.Any(s => s.Phase == SessionPhase.Disconnected));
        Assert.Equal(0, connects);
        Assert.Contains("localhost:4455", states.Last().Reason!.Detail);
    }

    [Fact]
    public void EveryFailureTellsTheUserWhatToDo()
    {
        foreach (var failure in Enum.GetValues<ObsFailure>())
        {
            var english = ObsSourceStream.Describe(failure, Target, English);
            var turkish = ObsSourceStream.Describe(failure, Target, Localizer.ForLanguage("tr"));
            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.NotEqual(english, turkish); // Türkçesi katalogda var
        }
    }

    [Fact]
    public void NeedsASourceName() =>
        Assert.Throws<ArgumentException>(() => new ObsSourceStream(new ObsSourceTarget("localhost", " "), () => null, Decode));
}

public class ObsSourceControllerTests
{
    private static readonly ObsSourceTarget Target = new("localhost:4455", "Capture Card");

    [Fact]
    public async Task ObsSourceStartsWithoutPs5AndUsesTheSavedPassword()
    {
        var store = new MemoryStore();
        var settings = new MemorySettings();
        settings.Save(new UserSettings { FrameSource = FrameSource.ObsSource, Output = SubtitleOutput.Computer });
        var services = new FakeServices();
        var controller = AppControllerTests.Make(services: services, store: store, settings: settings);

        Assert.Equal(OnboardingStep.Done, controller.Step); // PSN ve PS5 eşleşmesi gerekmez
        Assert.False(controller.CanStart); // kaynak seçilmeden başlamaz
        Assert.Equal("Choose an OBS source in Settings › Picture source.", controller.Ps5StatusText);

        controller.Settings = controller.Settings with { ObsSource = Target };
        Assert.True(controller.CanStart);
        Assert.Equal("OBS", controller.ConsoleTitle);
        controller.SetObsPassword("secret");
        Assert.True(controller.HasObsPassword);

        await controller.StartAsync();
        Assert.True(controller.IsRunning);
        var (target, password) = Assert.Single(services.ObsStreams);
        Assert.Equal(Target, target);
        Assert.Equal("secret", password());
        Assert.Empty(services.StreamPasscodes);
        Assert.Empty(services.CaptureWindowStreams);

        // Parola değişince yeni oturum açılmaz, bağlantı yenilenir; parola her bağlantıda depodan okunur.
        var stream = services.Streams.Last();
        controller.SetObsPassword("changed");
        Assert.Equal(1, stream.Reconnects);
        Assert.Equal("changed", password());
        controller.RemoveObsPassword();
        Assert.False(controller.HasObsPassword);
        Assert.Null(password());
        controller.Dispose();
    }

    /// <summary>
    /// Karşılamada OBS seçilince PSN girişi ve PS5 adımları gösterilmez: OBS adımında kaynak seçilir (OBS kapalıysa adım
    /// geçilir), sonra TV adımı gelir.
    /// </summary>
    [Fact]
    public void WizardWithObsSourceAsksForObsInsteadOfPsn()
    {
        var store = new MemoryStore();
        var settings = new MemorySettings();
        var controller = AppControllerTests.Make(store: store, settings: settings);
        controller.ChooseFrameSource(FrameSource.ObsSource);
        Assert.DoesNotContain(OnboardingStep.PsnLogin, controller.SetupSteps);
        Assert.DoesNotContain(OnboardingStep.SecondAccount, controller.SetupSteps);
        controller.Advance();
        Assert.Equal(OnboardingStep.Obs, controller.Step);
        Assert.False(controller.CanAdvance);

        controller.Settings = controller.Settings with { ObsSource = Target };
        Assert.True(controller.CanAdvance);
        controller.Advance();
        Assert.Equal(OnboardingStep.Tv, controller.Step);
        controller.Back();
        Assert.Equal(OnboardingStep.Obs, controller.Step);
        controller.Advance();
        controller.UseComputerScreen();
        Assert.False(controller.HasAccount);
        Assert.True(controller.CanStart);
        controller.Step = OnboardingStep.Done;
        Assert.Equal(OnboardingStep.Done, AppControllerTests.Make(store: store, settings: settings).Step);
        controller.Dispose();
    }

    [Fact]
    public async Task ChoosingAnotherSourceRestartsTheStream()
    {
        var settings = new MemorySettings();
        settings.Save(new UserSettings { FrameSource = FrameSource.ObsSource, ObsSource = Target, Output = SubtitleOutput.Computer });
        var services = new FakeServices();
        var controller = AppControllerTests.Make(services: services, settings: settings);

        await controller.StartAsync();
        var other = Target with { SourceName = "Second Card" };
        controller.Settings = controller.Settings with { ObsSource = other };
        for (var i = 0; i < 200 && services.ObsStreams.Count < 2; i++) await Task.Delay(10);
        Assert.Equal([Target, other], services.ObsStreams.Select(s => s.Target));
        Assert.True(controller.IsRunning);
        controller.Dispose();
    }

    [Fact]
    public void IncompleteTargetCannotStart()
    {
        var settings = new MemorySettings();
        settings.Save(new UserSettings { FrameSource = FrameSource.ObsSource, ObsSource = new ObsSourceTarget("not a host:x", "Card"), Output = SubtitleOutput.Computer });
        var controller = AppControllerTests.Make(settings: settings);
        Assert.False(controller.CanStart);
        controller.Dispose();
    }

    [Fact]
    public async Task ListingSourcesRejectsAnInvalidAddress()
    {
        var controller = AppControllerTests.Make();
        var error = await Assert.ThrowsAsync<ObsException>(() => controller.ObsInputsAsync("wss://example"));
        Assert.Equal(ObsFailure.InvalidAddress, error.Failure);
        controller.Dispose();
    }

    [Fact]
    public void StatusTextFollowsTheStream()
    {
        var text = Localizer.ForLanguage("en");
        Assert.Equal("Ready to read Capture Card from OBS when translation starts.",
            AppController.ObsSourceStatusText(SessionState.Disconnected(DisconnectKind.Stopped), false, text, Target));
        Assert.Equal("Reading Capture Card from OBS", AppController.ObsSourceStatusText(SessionState.Streaming, true, text, Target));
        Assert.Equal("Connecting to OBS…", AppController.ObsSourceStatusText(SessionState.Connecting, true, text, Target));
        Assert.Equal("detail", AppController.ObsSourceStatusText(SessionState.Disconnected(DisconnectKind.Network, "detail"), true, text, Target));
    }
}
