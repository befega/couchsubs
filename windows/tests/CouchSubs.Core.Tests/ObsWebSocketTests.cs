// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CouchSubs.Core;

namespace CouchSubs.Core.Tests;

public class ObsWebSocketTests
{
    /// <summary>obs-websocket belgesindeki algoritma; beklenen değer aynı girdilerle Python'da (hashlib, base64) hesaplandı.</summary>
    [Fact]
    public void AuthenticationFollowsTheObsAlgorithm() =>
        Assert.Equal("1Ct943GAT+6YQUUX47Ia/ncufilbe6+oD6lY+5kaCu4=",
            ObsProtocol.Authentication("supersecretpassword", "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=",
                                       "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY="));

    [Theory]
    [InlineData("localhost", "localhost", 4455)]
    [InlineData(" localhost:4455 ", "localhost", 4455)]
    [InlineData("192.168.1.20:4460", "192.168.1.20", 4460)]
    [InlineData("ws://obs-pc:4455", "obs-pc", 4455)]
    [InlineData("ws://obs-pc:4455/", "obs-pc", 4455)]
    [InlineData("[::1]:4455", "::1", 4455)]
    public void AddressesAreParsed(string address, string host, int port)
    {
        var endpoint = ObsEndpoint.Parse(address);
        Assert.NotNull(endpoint);
        Assert.Equal(host, endpoint.Host);
        Assert.Equal(port, endpoint.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wss://localhost:4455")]
    [InlineData("http://localhost:4455")]
    [InlineData("localhost:4455/path")]
    [InlineData("user:pw@localhost:4455")]
    [InlineData("localhost:notaport")]
    public void InvalidAddressesAreRejected(string? address) => Assert.Null(ObsEndpoint.Parse(address));

    [Fact]
    public void EndpointPrintsAsHostAndPort()
    {
        Assert.Equal("localhost:4455", ObsEndpoint.Parse("localhost")!.ToString());
        Assert.Equal("[::1]:4455", ObsEndpoint.Parse("[::1]")!.ToString());
        Assert.Equal(new Uri("ws://[::1]:4455/"), ObsEndpoint.Parse("[::1]")!.Uri);
    }

    [Fact]
    public void IdentifySendsAuthenticationOnlyWhenObsAsks()
    {
        using var open = JsonDocument.Parse("""{"rpcVersion":1}""");
        var plain = JsonNode.Parse(ObsProtocol.Identify(open.RootElement, "ignored"))!;
        Assert.Equal(1, (int)plain["op"]!);
        Assert.Null(plain["d"]!["authentication"]);
        Assert.Equal(0, (int)plain["d"]!["eventSubscriptions"]!);

        using var locked = JsonDocument.Parse("""{"rpcVersion":1,"authentication":{"salt":"s","challenge":"c"}}""");
        var signed = JsonNode.Parse(ObsProtocol.Identify(locked.RootElement, "pw"))!;
        Assert.Equal(ObsProtocol.Authentication("pw", "s", "c"), (string)signed["d"]!["authentication"]!);
        Assert.Equal(ObsFailure.PasswordNeeded, Assert.Throws<ObsException>(() => ObsProtocol.Identify(locked.RootElement, null)).Failure);
    }

    [Fact]
    public void ScreenshotRequestAsksForJpegAtFullSize()
    {
        var request = JsonNode.Parse(ObsProtocol.ScreenshotRequest("7", "Capture Card"))!;
        Assert.Equal(6, (int)request["op"]!);
        Assert.Equal("GetSourceScreenshot", (string)request["d"]!["requestType"]!);
        Assert.Equal("7", (string)request["d"]!["requestId"]!);
        var data = request["d"]!["requestData"]!;
        Assert.Equal("Capture Card", (string)data["sourceName"]!);
        Assert.Equal("jpg", (string)data["imageFormat"]!);
        Assert.Null(data["imageWidth"]);
    }

    [Fact]
    public void FailedRequestsMapToFailures()
    {
        using var missing = JsonDocument.Parse("""{"requestStatus":{"result":false,"code":600,"comment":"No source was found by the name of `x`."}}""");
        var error = Assert.Throws<ObsException>(() => ObsProtocol.ResponseData(missing.RootElement));
        Assert.Equal(ObsFailure.SourceMissing, error.Failure);
        Assert.Contains("No source", error.Detail);

        using var failed = JsonDocument.Parse("""{"requestStatus":{"result":false,"code":702}}""");
        Assert.Equal(ObsFailure.Failed, Assert.Throws<ObsException>(() => ObsProtocol.ResponseData(failed.RootElement)).Failure);
    }

    [Fact]
    public void ImageDataUriIsDecoded()
    {
        using var data = JsonDocument.Parse("""{"imageData":"data:image/jpg;base64,AQID"}""");
        Assert.Equal([1, 2, 3], ObsProtocol.ImageBytes(data.RootElement));
        using var broken = JsonDocument.Parse("""{"imageData":"data:image/jpg;base64,@@@"}""");
        Assert.Equal(ObsFailure.Failed, Assert.Throws<ObsException>(() => ObsProtocol.ImageBytes(broken.RootElement)).Failure);
    }

    [Fact]
    public void InputsListCaptureDevicesFirstAndHideAudio()
    {
        using var data = JsonDocument.Parse("""
            {"inputs":[
              {"inputName":"Mic/Aux","inputKind":"wasapi_input_capture","unversionedInputKind":"wasapi_input_capture"},
              {"inputName":"Webcam Overlay","inputKind":"browser_source","unversionedInputKind":"browser_source"},
              {"inputName":"Elgato 4K","inputKind":"dshow_input","unversionedInputKind":"dshow_input"},
              {"inputName":"Altyazı","inputKind":"browser_source"},
              {"inputName":"Desktop Audio","inputKind":"wasapi_output_capture"}
            ]}
            """);
        var inputs = ObsProtocol.Inputs(data.RootElement);
        Assert.Equal(["Elgato 4K", "Altyazı", "Webcam Overlay"], inputs.Select(i => i.Name));
        Assert.True(inputs[0].IsVideoCapture);
    }

    [Fact]
    public async Task ConnectsWithPasswordAndReadsScreenshot()
    {
        await using var server = new FakeObsServer { Password = "secret" };
        server.Handle = request => (string)request["requestType"]! switch
        {
            "GetSourceScreenshot" => Success(new JsonObject { ["imageData"] = "data:image/jpg;base64,AQID" }),
            _ => Failure(204),
        };
        await using var client = await ObsWebSocketClient.ConnectAsync(server.Endpoint, "secret", CancellationToken.None);

        Assert.Equal([1, 2, 3], await client.ScreenshotAsync("Capture Card", CancellationToken.None));
        Assert.Equal([1, 2, 3], await client.ScreenshotAsync("Capture Card", CancellationToken.None));
        Assert.Equal(2, server.Requests.Count);
        Assert.Equal("Capture Card", (string)server.Requests[0]["requestData"]!["sourceName"]!);
        Assert.NotEqual((string)server.Requests[0]["requestId"]!, (string)server.Requests[1]["requestId"]!);
    }

    [Fact]
    public async Task ListsInputsWithoutPasswordWhenObsHasNone()
    {
        await using var server = new FakeObsServer();
        server.Handle = _ => Success(JsonNode.Parse("""{"inputs":[{"inputName":"Capture","inputKind":"dshow_input"}]}""")!.AsObject());
        await using var client = await ObsWebSocketClient.ConnectAsync(server.Endpoint, null, CancellationToken.None);

        var inputs = await client.InputsAsync(CancellationToken.None);
        Assert.Equal("Capture", Assert.Single(inputs).Name);
        Assert.Equal("GetInputList", (string)server.Requests[0]["requestType"]!);
    }

    [Fact]
    public async Task WrongPasswordIsReported()
    {
        await using var server = new FakeObsServer { Password = "secret" };
        var error = await Assert.ThrowsAsync<ObsException>(() => ObsWebSocketClient.ConnectAsync(server.Endpoint, "wrong", CancellationToken.None));
        Assert.Equal(ObsFailure.PasswordRejected, error.Failure);
    }

    [Fact]
    public async Task MissingPasswordIsReported()
    {
        await using var server = new FakeObsServer { Password = "secret" };
        var error = await Assert.ThrowsAsync<ObsException>(() => ObsWebSocketClient.ConnectAsync(server.Endpoint, null, CancellationToken.None));
        Assert.Equal(ObsFailure.PasswordNeeded, error.Failure);
    }

    [Fact]
    public async Task UnknownSourceIsReported()
    {
        await using var server = new FakeObsServer();
        server.Handle = _ => Failure(600, "No source was found by the name of `Capture`.");
        await using var client = await ObsWebSocketClient.ConnectAsync(server.Endpoint, null, CancellationToken.None);
        var error = await Assert.ThrowsAsync<ObsException>(() => client.ScreenshotAsync("Capture", CancellationToken.None));
        Assert.Equal(ObsFailure.SourceMissing, error.Failure);
    }

    [Fact]
    public async Task ClosedPortIsUnreachable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var error = await Assert.ThrowsAsync<ObsException>(() =>
            ObsWebSocketClient.ConnectAsync(new ObsEndpoint("127.0.0.1", port), null, CancellationToken.None));
        Assert.Equal(ObsFailure.Unreachable, error.Failure);
    }

    private static JsonObject Success(JsonObject data) =>
        new() { ["requestStatus"] = new JsonObject { ["result"] = true, ["code"] = 100 }, ["responseData"] = data };

    private static JsonObject Failure(int code, string? comment = null) =>
        new() { ["requestStatus"] = new JsonObject { ["result"] = false, ["code"] = code, ["comment"] = comment } };
}

/// <summary>
/// obs-websocket 5 gibi davranan küçük sunucu: HTTP yükseltmesi, Hello, kimlik doğrulama (yanlışsa 4009 ile kapanış),
/// Identified ve istek/yanıt. Gerçek OBS olmadan istemcinin tel üstündeki davranışını sınar.
/// </summary>
internal sealed class FakeObsServer : IAsyncDisposable
{
    private const string Salt = "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=";
    private const string Challenge = "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY=";

    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;

    public string? Password { get; init; }
    public Func<JsonObject, JsonObject> Handle { get; set; } = _ => new JsonObject { ["requestStatus"] = new JsonObject { ["result"] = false, ["code"] = 204 } };
    public List<JsonObject> Requests { get; } = [];
    public ObsEndpoint Endpoint => new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);

    public FakeObsServer()
    {
        listener.Start();
        loop = Task.Run(AcceptAsync);
    }

    private async Task AcceptAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(stop.Token); }
            catch (Exception) { return; }
            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        try
        {
            var key = await ReadUpgradeKeyAsync(stream);
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                           + $"Sec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Protocol: {ObsProtocol.SubProtocol}\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
            using var socket = WebSocket.CreateFromStream(stream, isServer: true, ObsProtocol.SubProtocol, TimeSpan.FromSeconds(30));

            var hello = new JsonObject { ["obsWebSocketVersion"] = "5.5.0", ["rpcVersion"] = 1 };
            if (Password is not null) hello["authentication"] = new JsonObject { ["challenge"] = Challenge, ["salt"] = Salt };
            await SendAsync(socket, new JsonObject { ["op"] = 0, ["d"] = hello });

            var identify = await ReceiveAsync(socket);
            if (identify is null || (int)identify["op"]! != 1) return;
            if (Password is not null && (string?)identify["d"]!["authentication"] != ObsProtocol.Authentication(Password, Salt, Challenge))
            {
                await socket.CloseAsync((WebSocketCloseStatus)4009, "Authentication failed.", stop.Token);
                return;
            }
            await SendAsync(socket, new JsonObject { ["op"] = 2, ["d"] = new JsonObject { ["negotiatedRpcVersion"] = 1 } });

            while (await ReceiveAsync(socket) is { } message)
            {
                var request = message["d"]!.AsObject();
                lock (Requests) Requests.Add(request);
                var answer = Handle(request);
                answer["requestType"] = (string)request["requestType"]!;
                answer["requestId"] = (string)request["requestId"]!;
                await SendAsync(socket, new JsonObject { ["op"] = 7, ["d"] = answer });
            }
        }
        catch (Exception) { /* İstemci kapandı ya da sunucu durduruldu. */ }
    }

    private static async Task<string> ReadUpgradeKeyAsync(NetworkStream stream)
    {
        var header = new StringBuilder();
        var one = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(one) == 0) throw new IOException("closed");
            header.Append((char)one[0]);
        }
        var line = header.ToString().Split("\r\n").First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
        return line["Sec-WebSocket-Key:".Length..].Trim();
    }

    private async Task SendAsync(WebSocket socket, JsonObject message) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(message.ToJsonString()), WebSocketMessageType.Text, true, stop.Token);

    private async Task<JsonObject?> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, stop.Token);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return JsonNode.Parse(message.ToArray())!.AsObject();
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        listener.Stop();
        try { await loop; } catch (Exception) { }
        stop.Dispose();
    }
}
