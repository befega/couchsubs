// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CouchSubs.Core;

/// <summary>
/// OBS'in WebSocket sunucusunun adresi (OBS 28 ve sonrasında yerleşik obs-websocket 5; Araçlar › WebSocket Sunucu
/// Ayarları). Varsayılan bu bilgisayardaki OBS: <c>localhost:4455</c>.
/// </summary>
public sealed record ObsEndpoint(string Host, int Port)
{
    public const int DefaultPort = 4455;
    public const string DefaultAddress = "localhost:4455";

    public Uri Uri => new UriBuilder("ws", Host, Port).Uri;

    public override string ToString() => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    /// <summary>
    /// "localhost", "localhost:4455", "192.168.1.20:4455" ya da "ws://…" kabul edilir; port yazılmazsa 4455. obs-websocket
    /// TLS konuşmaz; wss, yol, sorgu ve kullanıcı bilgisi olan adresler geçersizdir (null).
    /// </summary>
    public static ObsEndpoint? Parse(string? address)
    {
        var text = address?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "ws://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "ws" || string.IsNullOrEmpty(uri.IdnHost)) return null;
        if (uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0) return null;
        return new ObsEndpoint(uri.IdnHost, uri.IsDefaultPort ? DefaultPort : uri.Port);
    }
}

/// <summary>OBS'teki bir kaynak (girdi): ad ve türü (ör. "dshow_input" = Video Yakalama Aygıtı).</summary>
public sealed record ObsInput(string Name, string Kind)
{
    /// <summary>Capture card ve kamera türleri (Windows, Mac, Linux, Blackmagic). Listede önce bunlar gösterilir.</summary>
    private static readonly HashSet<string> VideoCaptureKinds =
        ["dshow_input", "decklink-input", "av_capture_input", "macos-avcapture", "macos-avcapture-fast", "v4l2_input"];

    public bool IsVideoCapture => VideoCaptureKinds.Contains(Kind);

    /// <summary>Yalnız ses veren kaynaklar (mikrofon, masaüstü sesi); görüntüleri yoktur, listede gösterilmez.</summary>
    public bool IsAudioOnly => Kind.Contains("audio", StringComparison.OrdinalIgnoreCase)
                               || Kind.StartsWith("wasapi_", StringComparison.Ordinal)
                               || Kind.StartsWith("pulse_", StringComparison.Ordinal)
                               || Kind.StartsWith("alsa_", StringComparison.Ordinal)
                               || Kind.StartsWith("jack_", StringComparison.Ordinal);
}

public enum ObsFailure
{
    /// <summary>Adres host:port biçiminde değil.</summary>
    InvalidAddress,
    /// <summary>OBS açık değil, WebSocket sunucusu kapalı ya da yanıt vermedi.</summary>
    Unreachable,
    /// <summary>OBS parola istiyor, kayıtlı parola yok.</summary>
    PasswordNeeded,
    /// <summary>OBS parolayı kabul etmedi (kapanış kodu 4009).</summary>
    PasswordRejected,
    /// <summary>Bu adda kaynak yok (istek durumu 600).</summary>
    SourceMissing,
    /// <summary>OBS bu protokol sürümünü konuşmuyor (4010; OBS 28'den eski).</summary>
    Unsupported,
    /// <summary>OBS isteği yapamadı (ör. kaynak görünmüyor, görüntüsü alınamadı).</summary>
    Failed,
}

public sealed class ObsException(ObsFailure failure, string? detail = null)
    : Exception(string.IsNullOrWhiteSpace(detail) ? $"OBS: {failure}" : $"OBS: {failure} ({detail})")
{
    public ObsFailure Failure { get; } = failure;
    public string? Detail { get; } = detail;
}

/// <summary>
/// obs-websocket 5 protokolünün CouchSubs'ın kullandığı kısmı: kimlik doğrulama, kaynak listesi ve kaynağın anlık
/// görüntüsü (GetSourceScreenshot). OBS'in yayınına, kaydına ya da sahnelerine hiçbir şey yazılmaz; yalnız okunur.
/// Belge: https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md
/// </summary>
public static class ObsProtocol
{
    public const int RpcVersion = 1;
    public const string SubProtocol = "obswebsocket.json";
    /// <summary>
    /// JPEG: OBS'te PNG'den kat kat hızlı sıkışır ve mesaj küçük kalır. Kalite 90, yazı tanımanın okuduğu beyaz yazıyı
    /// PS5 akışının kendi sıkıştırmasından daha az bozar.
    /// </summary>
    public const string ImageFormat = "jpg";
    public const int ImageQuality = 90;
    /// <summary>4K bir karenin JPEG'i bile birkaç MB'dır; bundan büyük mesaj bozuk sayılır.</summary>
    public const int MaxMessageBytes = 64 * 1024 * 1024;

    private const int OpHello = 0, OpIdentify = 1, OpIdentified = 2, OpRequest = 6, OpRequestResponse = 7;
    private const int StatusResourceNotFound = 600;

    /// <summary>base64(sha256(base64(sha256(parola + tuz)) + meydan okuma)); parola OBS'e hiç düz gönderilmez.</summary>
    public static string Authentication(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }

    public static int Op(JsonElement message) =>
        message.TryGetProperty("op", out var op) && op.ValueKind == JsonValueKind.Number ? op.GetInt32() : -1;

    /// <summary>Hello'ya (op 0) verilecek Identify (op 1). Olay aboneliği yoktur: OBS yalnız isteklerin yanıtını gönderir.</summary>
    public static string Identify(JsonElement hello, string? password)
    {
        var data = new JsonObject { ["rpcVersion"] = RpcVersion, ["eventSubscriptions"] = 0 };
        if (hello.TryGetProperty("authentication", out var auth) && auth.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrEmpty(password)) throw new ObsException(ObsFailure.PasswordNeeded);
            data["authentication"] = Authentication(password, auth.GetProperty("salt").GetString() ?? "",
                                                    auth.GetProperty("challenge").GetString() ?? "");
        }
        return Message(OpIdentify, data);
    }

    public static string Request(string requestType, string requestId, JsonObject? requestData = null)
    {
        var data = new JsonObject { ["requestType"] = requestType, ["requestId"] = requestId };
        if (requestData is not null) data["requestData"] = requestData;
        return Message(OpRequest, data);
    }

    /// <summary>Kaynağın kendi çözünürlüğündeki görüntüsü (genişlik/yükseklik verilmez: yazı tanıma küçültülmemiş kareyi okur).</summary>
    public static string ScreenshotRequest(string requestId, string sourceName) =>
        Request("GetSourceScreenshot", requestId, new JsonObject
        {
            ["sourceName"] = sourceName,
            ["imageFormat"] = ImageFormat,
            ["imageCompressionQuality"] = ImageQuality,
        });

    public static string InputListRequest(string requestId) => Request("GetInputList", requestId);

    public static bool IsHello(JsonElement message) => Op(message) == OpHello;
    public static bool IsIdentified(JsonElement message) => Op(message) == OpIdentified;

    /// <summary>Mesaj bu isteğin yanıtıysa (op 7, aynı requestId) "d" alanı.</summary>
    public static bool TryResponse(JsonElement message, string requestId, out JsonElement response)
    {
        response = default;
        if (Op(message) != OpRequestResponse || !message.TryGetProperty("d", out var data)) return false;
        if (!data.TryGetProperty("requestId", out var id) || id.GetString() != requestId) return false;
        response = data;
        return true;
    }

    /// <summary>Başarılı yanıtın responseData'sı; başarısızsa <see cref="ObsException"/>.</summary>
    public static JsonElement ResponseData(JsonElement response)
    {
        var status = response.GetProperty("requestStatus");
        if (status.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.True)
            return response.TryGetProperty("responseData", out var data) ? data : default;
        var code = status.TryGetProperty("code", out var number) && number.ValueKind == JsonValueKind.Number ? number.GetInt32() : 0;
        var comment = status.TryGetProperty("comment", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null;
        throw new ObsException(code == StatusResourceNotFound ? ObsFailure.SourceMissing : ObsFailure.Failed,
                               comment ?? (code == 0 ? null : code.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>"data:image/jpg;base64,…" biçimindeki görüntünün baytları.</summary>
    public static byte[] ImageBytes(JsonElement responseData)
    {
        if (responseData.ValueKind != JsonValueKind.Object || !responseData.TryGetProperty("imageData", out var image)
            || image.GetString() is not { Length: > 0 } text)
            throw new ObsException(ObsFailure.Failed, "OBS returned no image.");
        var comma = text.IndexOf(',');
        try { return Convert.FromBase64String(comma >= 0 ? text[(comma + 1)..] : text); }
        catch (FormatException) { throw new ObsException(ObsFailure.Failed, "OBS returned an unreadable image."); }
    }

    /// <summary>Görüntüsü olan kaynaklar: önce capture card ve kameralar, sonra diğerleri, adlarına göre.</summary>
    public static IReadOnlyList<ObsInput> Inputs(JsonElement responseData)
    {
        if (responseData.ValueKind != JsonValueKind.Object || !responseData.TryGetProperty("inputs", out var inputs)
            || inputs.ValueKind != JsonValueKind.Array) return [];
        var list = new List<ObsInput>();
        foreach (var input in inputs.EnumerateArray())
        {
            var name = input.TryGetProperty("inputName", out var n) ? n.GetString() : null;
            if (string.IsNullOrEmpty(name)) continue;
            var kind = input.TryGetProperty("unversionedInputKind", out var u) && u.GetString() is { Length: > 0 } unversioned ? unversioned
                     : input.TryGetProperty("inputKind", out var k) ? k.GetString() ?? "" : "";
            var item = new ObsInput(name, kind);
            if (!item.IsAudioOnly) list.Add(item);
        }
        return [.. list.OrderByDescending(i => i.IsVideoCapture).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>OBS'in kapanış kodları: 4009 parola yanlış, 4010 desteklenmeyen sürüm.</summary>
    public static ObsFailure CloseFailure(WebSocketCloseStatus? status) => (int?)status switch
    {
        4009 => ObsFailure.PasswordRejected,
        4010 => ObsFailure.Unsupported,
        _ => ObsFailure.Unreachable,
    };

    private static string Message(int op, JsonObject data) => new JsonObject { ["op"] = op, ["d"] = data }.ToJsonString();
}

/// <summary>OBS'e açık bir bağlantı. Testlerde sahtesi kullanılır.</summary>
public interface IObsConnection : IAsyncDisposable
{
    Task<IReadOnlyList<ObsInput>> InputsAsync(CancellationToken cancellationToken);
    /// <summary>Kaynağın o anki görüntüsü (JPEG baytları).</summary>
    Task<byte[]> ScreenshotAsync(string sourceName, CancellationToken cancellationToken);
}

/// <summary>obs-websocket 5 istemcisi. İstekler sırayla gider; aynı anda tek istek açıktır.</summary>
public sealed class ObsWebSocketClient : IObsConnection
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly ClientWebSocket socket;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly byte[] buffer = new byte[64 * 1024];
    private int nextId;

    private ObsWebSocketClient(ClientWebSocket socket) => this.socket = socket;

    public static async Task<IObsConnection> ConnectAsync(ObsEndpoint endpoint, string? password, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(ObsProtocol.SubProtocol);
        var client = new ObsWebSocketClient(socket);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);
        try
        {
            await socket.ConnectAsync(endpoint.Uri, timeout.Token).ConfigureAwait(false);
            var hello = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            if (!ObsProtocol.IsHello(hello)) throw new ObsException(ObsFailure.Unsupported, "No Hello from OBS.");
            await client.SendAsync(ObsProtocol.Identify(hello.GetProperty("d"), password), timeout.Token).ConfigureAwait(false);
            var identified = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            if (!ObsProtocol.IsIdentified(identified)) throw new ObsException(ObsFailure.Unsupported, "OBS did not confirm the connection.");
            return client;
        }
        catch (Exception error)
        {
            // Kapanış kodu (ör. 4009 parola yanlış) soket kapatılmadan okunur.
            var failure = error is OperationCanceledException && cancellationToken.IsCancellationRequested ? null : Translate(error, socket);
            await client.DisposeAsync().ConfigureAwait(false);
            if (failure is null) throw;
            throw failure;
        }
    }

    public async Task<IReadOnlyList<ObsInput>> InputsAsync(CancellationToken cancellationToken) =>
        ObsProtocol.Inputs(await RequestAsync(ObsProtocol.InputListRequest, cancellationToken).ConfigureAwait(false));

    public async Task<byte[]> ScreenshotAsync(string sourceName, CancellationToken cancellationToken) =>
        ObsProtocol.ImageBytes(await RequestAsync(id => ObsProtocol.ScreenshotRequest(id, sourceName), cancellationToken).ConfigureAwait(false));

    private async Task<JsonElement> RequestAsync(Func<string, string> build, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            var id = Interlocked.Increment(ref nextId).ToString(CultureInfo.InvariantCulture);
            await SendAsync(build(id), timeout.Token).ConfigureAwait(false);
            while (true)
            {
                var message = await ReceiveAsync(timeout.Token).ConfigureAwait(false);
                if (ObsProtocol.TryResponse(message, id, out var response)) return ObsProtocol.ResponseData(response);
            }
        }
        catch (Exception error) when (error is not ObsException && !(error is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            throw Translate(error, socket);
        }
        finally { gate.Release(); }
    }

    private Task SendAsync(string json, CancellationToken cancellationToken) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    /// <summary>Parçalı gelen mesajı birleştirir; OBS bağlantıyı kapattıysa kapanış koduna göre hata verir.</summary>
    private async Task<JsonElement> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new ObsException(ObsProtocol.CloseFailure(socket.CloseStatus), socket.CloseStatusDescription);
            message.Write(buffer, 0, result.Count);
            if (message.Length > ObsProtocol.MaxMessageBytes) throw new ObsException(ObsFailure.Failed, "Message from OBS is too large.");
            if (result.EndOfMessage) break;
        }
        message.Position = 0;
        using var document = await JsonDocument.ParseAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        return document.RootElement.Clone();
    }

    private static ObsException Translate(Exception error, ClientWebSocket socket) => error switch
    {
        ObsException obs => obs,
        _ when socket.CloseStatus is { } status && ObsProtocol.CloseFailure(status) != ObsFailure.Unreachable =>
            new ObsException(ObsProtocol.CloseFailure(status), socket.CloseStatusDescription),
        OperationCanceledException => new ObsException(ObsFailure.Unreachable, "OBS did not answer in time."),
        JsonException => new ObsException(ObsFailure.Failed, "OBS sent an unreadable message."),
        _ => new ObsException(ObsFailure.Unreachable, error.GetBaseException().Message),
    };

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception) { /* OBS zaten kapanmış olabilir. */ }
        socket.Dispose();
        gate.Dispose();
    }
}
