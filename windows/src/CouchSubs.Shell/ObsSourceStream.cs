// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using CouchSubs.Core;
using CouchSubs.RemotePlay;

namespace CouchSubs.Shell;

/// <summary>
/// Görüntüyü OBS'teki bir kaynaktan (ör. capture card'ın Video Yakalama Aygıtı) obs-websocket ile alır. CouchSubs capture
/// card'ı kendisi açmaz: kartı OBS tutar, CouchSubs yalnız o kaynağın anlık görüntüsünü ister (GetSourceScreenshot). Yayın,
/// kayıt ve sahneler değişmez. Kaynağın kendisi okunduğu için sahnedeki Türkçe altyazı katmanı görüntüye girmez; çeviri
/// kendi üstüne geri beslenmez. Kareler <see cref="FrameReceived"/> ile gelir; okuma hattının geri kalanı aynıdır.
/// </summary>
public sealed class ObsSourceStream : IStreamSession
{
    /// <summary>Saniyede en çok 8 kare: altyazı saniyelerce ekranda kalır; fazlası yalnız OBS'i ve işlemciyi yorar.</summary>
    public static readonly TimeSpan DefaultFrameInterval = TimeSpan.FromMilliseconds(125);
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);

    public delegate Task<IObsConnection> Connector(ObsEndpoint endpoint, string? password, CancellationToken cancellationToken);

    public event Action<Frame>? FrameReceived;
    public event Action<SessionState>? StateChanged;
    /// <summary>Bu kaynakta PS5'in adresi bilinmez; hiç çağrılmaz.</summary>
    public event Action<RegisteredConsole>? ConsoleMoved { add { } remove { } }

    private readonly ObsSourceTarget target;
    private readonly Func<string?> password;
    private readonly Func<byte[], Frame?> decode;
    private readonly Connector connect;
    private readonly TimeSpan frameInterval;
    private readonly TimeSpan retryDelay;
    private readonly Localizer text;
    private readonly object gate = new();
    private CancellationTokenSource? loop;
    private SessionState? reported;
    private string? lastLogged;

    /// <param name="password">Her bağlantıda yeniden okunur: Ayarlar'da parola değişince yeniden bağlanmak yeter.</param>
    /// <param name="decode">JPEG baytlarını BGRA kareye çevirir (Windows'ta WPF); okunamazsa null.</param>
    public ObsSourceStream(ObsSourceTarget target, Func<string?> password, Func<byte[], Frame?> decode, Connector? connect = null,
                           TimeSpan? frameInterval = null, TimeSpan? retryDelay = null, Localizer? text = null)
    {
        if (string.IsNullOrWhiteSpace(target.SourceName))
            throw new ArgumentException("An OBS source must be selected.", nameof(target));
        this.target = target;
        this.password = password;
        this.decode = decode;
        this.connect = connect ?? ((endpoint, secret, token) => ObsWebSocketClient.ConnectAsync(endpoint, secret, token));
        this.frameInterval = frameInterval ?? DefaultFrameInterval;
        this.retryDelay = retryDelay ?? DefaultRetryDelay;
        this.text = text ?? Localizer.Current;
    }

    public void Start() => Restart();

    public void ReconnectNow()
    {
        lock (gate) if (loop is null) return;
        Restart();
    }

    public void Stop()
    {
        CancellationTokenSource? old;
        lock (gate)
        {
            old = loop;
            loop = null;
            reported = null;
        }
        old?.Cancel();
        if (old is not null) StateChanged?.Invoke(SessionState.Disconnected(DisconnectKind.Stopped));
    }

    public void Dispose() => Stop();

    private void Restart()
    {
        var next = new CancellationTokenSource();
        CancellationTokenSource? old;
        lock (gate)
        {
            old = loop;
            loop = next;
            reported = null;
        }
        old?.Cancel();
        Report(SessionState.Connecting, next.Token);
        _ = Task.Run(() => RunAsync(next.Token));
    }

    private async Task RunAsync(CancellationToken token)
    {
        var endpoint = ObsEndpoint.Parse(target.Address);
        if (endpoint is null)
        {
            Report(SessionState.Disconnected(DisconnectKind.Other, Describe(ObsFailure.InvalidAddress, target, text)), token);
            return;
        }
        while (!token.IsCancellationRequested)
        {
            IObsConnection? connection = null;
            try
            {
                connection = await connect(endpoint, password(), token).ConfigureAwait(false);
                while (!token.IsCancellationRequested)
                {
                    var started = Stopwatch.GetTimestamp();
                    var image = await connection.ScreenshotAsync(target.SourceName, token).ConfigureAwait(false);
                    if (decode(image) is { Width: > 0, Height: > 0 } frame)
                    {
                        Report(SessionState.Streaming, token);
                        Deliver(frame, token);
                    }
                    var wait = frameInterval - Stopwatch.GetElapsedTime(started);
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (ObsException error)
            {
                Log(error.Message);
                Report(SessionState.Disconnected(Kind(error.Failure), Describe(error.Failure, target, text)), token);
            }
            catch (Exception error)
            {
                Log(error.GetBaseException().Message);
                Report(SessionState.Disconnected(DisconnectKind.Other, Describe(ObsFailure.Failed, target, text)), token);
            }
            finally
            {
                if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            }
            try { await Task.Delay(retryDelay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Durdurulan ya da yeniden başlatılan döngü artık bildirim yapmaz; aynı durum iki kez bildirilmez.</summary>
    private void Report(SessionState state, CancellationToken token)
    {
        lock (gate)
        {
            if (token.IsCancellationRequested || reported == state) return;
            reported = state;
            StateChanged?.Invoke(state);
        }
    }

    private void Deliver(Frame frame, CancellationToken token)
    {
        if (!token.IsCancellationRequested) FrameReceived?.Invoke(frame);
    }

    /// <summary>Aynı hata her denemede günlüğü doldurmasın.</summary>
    private void Log(string message)
    {
        if (message == lastLogged) return;
        lastLogged = message;
        RemotePlayLog.Note($"OBS kaynağı ({target.SourceName}): {message}");
    }

    public static DisconnectKind Kind(ObsFailure failure) => failure switch
    {
        ObsFailure.Unreachable => DisconnectKind.Network,
        ObsFailure.PasswordNeeded or ObsFailure.PasswordRejected => DisconnectKind.Refused,
        ObsFailure.SourceMissing => DisconnectKind.NotFound,
        _ => DisconnectKind.Other,
    };

    /// <summary>Hatanın kullanıcıya söylenişi: ne yapacağı yazılır (ana ekran ve Ayarlar aynı metni kullanır).</summary>
    public static string Describe(ObsFailure failure, ObsSourceTarget? target, Localizer text)
    {
        var address = ObsEndpoint.Parse(target?.Address)?.ToString() ?? target?.Address ?? ObsEndpoint.DefaultAddress;
        var source = target?.SourceName ?? "";
        return failure switch
        {
            ObsFailure.InvalidAddress => text["Enter the OBS address as host:port, for example localhost:4455."],
            ObsFailure.PasswordNeeded => text["OBS asks for the WebSocket password. Enter it in Settings › Picture source."],
            ObsFailure.PasswordRejected => text["OBS did not accept the WebSocket password. Copy it again from Tools › WebSocket Server Settings › Show Connect Info."],
            ObsFailure.SourceMissing => text.Format("OBS has no source named %@. Choose the capture card source again in Settings › Picture source.", source),
            ObsFailure.Unsupported => text["This OBS version is too old. Use OBS 28 or later."],
            ObsFailure.Failed when source.Length > 0 => text.Format("OBS could not take a picture of %@. Make sure the source is shown in a scene.", source),
            ObsFailure.Failed => text["OBS could not complete the request. Try again."],
            _ => text.Format("OBS is not reachable at %@. Open OBS and turn on Tools › WebSocket Server Settings › Enable WebSocket server.", address),
        };
    }

    /// <summary>Ayarlar'daki kaynak listesi için kısa bir bağlantı açar.</summary>
    public static async Task<IReadOnlyList<ObsInput>> ListInputsAsync(ObsEndpoint endpoint, string? password, CancellationToken cancellationToken)
    {
        await using var connection = await ObsWebSocketClient.ConnectAsync(endpoint, password, cancellationToken).ConfigureAwait(false);
        return await connection.InputsAsync(cancellationToken).ConfigureAwait(false);
    }
}
