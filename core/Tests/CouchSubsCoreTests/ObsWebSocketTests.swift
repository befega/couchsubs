// SPDX-License-Identifier: AGPL-3.0-only

import CoreGraphics
import Foundation
import ImageIO
import Network
import UniformTypeIdentifiers
import XCTest
@testable import CouchSubsCore

/// obs-websocket 5 konuşan sahte OBS: Hello (parola varsa meydan okumayla), Identify denetimi (yanlış parolada 4009 ile
/// kapatır) ve isteklere `handle`'ın yanıtı.
final class FakeObsServer: @unchecked Sendable {
    static let salt = "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI="
    static let challenge = "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY="

    private let listener: NWListener
    private let queue = DispatchQueue(label: "fake-obs")
    private let lock = NSLock()
    private var _requests: [[String: Any]] = []
    private let offered = Recorder<[String]>()
    var password: String?
    /// İsteğin "d" alanı → (başarılı mı, durum kodu, responseData).
    var handle: ([String: Any]) -> (ok: Bool, code: Int, data: [String: Any]) = { _ in (false, 204, [:]) }

    var requests: [[String: Any]] { lock.withLock { _requests } }
    var offeredProtocols: [String] { offered.all.last ?? [] }

    init() throws {
        // İstek işleyicisi seçenekler parametrelere eklenmeden kurulur; sonradan kurulan işleyici çağrılmıyor.
        let options = NWProtocolWebSocket.Options()
        options.setClientRequestHandler(queue) { [offered] protocols, _ in
            offered.add(protocols)
            let chosen = protocols.contains(ObsProtocol.subProtocol) ? ObsProtocol.subProtocol : nil
            return NWProtocolWebSocket.Response(status: .accept, subprotocol: chosen)
        }
        let parameters = NWParameters.tcp
        parameters.defaultProtocolStack.applicationProtocols.insert(options, at: 0)
        listener = try NWListener(using: parameters, on: .any)
    }

    func start() async -> ObsEndpoint {
        await withCheckedContinuation { continuation in
            listener.newConnectionHandler = { [weak self] connection in
                guard let self else { return }
                connection.stateUpdateHandler = { [weak self] state in
                    if case .ready = state { self?.hello(on: connection) }
                }
                connection.start(queue: self.queue)
                self.receive(on: connection)
            }
            listener.stateUpdateHandler = { [listener] state in
                if case .ready = state {
                    continuation.resume(returning: ObsEndpoint(host: "127.0.0.1", port: Int(listener.port!.rawValue)))
                }
            }
            listener.start(queue: queue)
        }
    }

    func stop() { listener.cancel() }

    private func hello(on connection: NWConnection) {
        var data: [String: Any] = ["obsWebSocketVersion": "5.5.0", "rpcVersion": 1]
        if password != nil { data["authentication"] = ["salt": Self.salt, "challenge": Self.challenge] }
        send(["op": 0, "d": data], on: connection)
    }

    private func receive(on connection: NWConnection) {
        connection.receiveMessage { [weak self] data, _, _, error in
            guard let self, error == nil, let data,
                  let message = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }
            let d = message["d"] as? [String: Any] ?? [:]
            switch message["op"] as? Int {
            case 1:
                if let password = self.password,
                   d["authentication"] as? String != ObsProtocol.authentication(password: password, salt: Self.salt,
                                                                                challenge: Self.challenge) {
                    return self.close(connection, code: 4009)
                }
                self.send(["op": 2, "d": ["negotiatedRpcVersion": 1]], on: connection)
            case 6:
                self.lock.withLock { self._requests.append(d) }
                let answer = self.handle(d)
                self.send(["op": 7, "d": ["requestType": d["requestType"] ?? "", "requestId": d["requestId"] ?? "",
                                          "requestStatus": ["result": answer.ok, "code": answer.code],
                                          "responseData": answer.data]], on: connection)
            default:
                break
            }
            self.receive(on: connection)
        }
    }

    private func send(_ object: [String: Any], on connection: NWConnection) {
        let data = try! JSONSerialization.data(withJSONObject: object)
        let context = NWConnection.ContentContext(identifier: "send", metadata: [NWProtocolWebSocket.Metadata(opcode: .text)])
        connection.send(content: data, contentContext: context, isComplete: true, completion: .contentProcessed { _ in })
    }

    private func close(_ connection: NWConnection, code: UInt16) {
        let metadata = NWProtocolWebSocket.Metadata(opcode: .close)
        metadata.closeCode = .privateCode(code)
        let context = NWConnection.ContentContext(identifier: "close", metadata: [metadata])
        connection.send(content: nil, contentContext: context, isComplete: true, completion: .contentProcessed { _ in
            connection.cancel()
        })
    }
}

final class ObsProtocolTests: XCTestCase {
    /// obs-websocket belgesindeki algoritma; beklenen değer aynı girdilerle Python'da (hashlib, base64) hesaplandı
    /// (Windows testindekiyle aynı).
    func testAuthenticationFollowsTheObsAlgorithm() {
        XCTAssertEqual(ObsProtocol.authentication(password: "supersecretpassword", salt: FakeObsServer.salt,
                                                  challenge: FakeObsServer.challenge),
                       "1Ct943GAT+6YQUUX47Ia/ncufilbe6+oD6lY+5kaCu4=")
    }

    func testAddressesAreParsed() {
        let cases: [(String, String, Int)] = [
            ("localhost", "localhost", 4455), (" localhost:4455 ", "localhost", 4455),
            ("192.168.1.20:4460", "192.168.1.20", 4460), ("ws://obs-pc:4455", "obs-pc", 4455),
            ("ws://obs-pc:4455/", "obs-pc", 4455), ("[::1]:4455", "::1", 4455), ("OBS-PC", "obs-pc", 4455),
        ]
        for (address, host, port) in cases {
            let endpoint = ObsEndpoint.parse(address)
            XCTAssertEqual(endpoint?.host, host, address)
            XCTAssertEqual(endpoint?.port, port, address)
        }
    }

    func testInvalidAddressesAreRejected() {
        for address in [nil, "", "  ", "wss://localhost:4455", "http://localhost:4455", "localhost:4455/path",
                        "user:pw@localhost:4455", "localhost:notaport", "localhost:0", "localhost:70000", "not a host:x",
                        "localhost:4455?x=1"] {
            XCTAssertNil(ObsEndpoint.parse(address), address ?? "nil")
        }
    }

    func testEndpointPrintsAsHostAndPort() {
        XCTAssertEqual(ObsEndpoint.parse("localhost")?.description, "localhost:4455")
        XCTAssertEqual(ObsEndpoint.parse("[::1]")?.description, "[::1]:4455")
        XCTAssertEqual(ObsEndpoint.parse("[::1]")?.url.absoluteString, "ws://[::1]:4455")
    }

    func testIdentifySendsAuthenticationOnlyWhenObsAsks() throws {
        let plain = try ObsProtocol.decode(try ObsProtocol.identify(hello: ["rpcVersion": 1], password: "ignored"))
        XCTAssertEqual(plain["op"] as? Int, 1)
        let data = try XCTUnwrap(plain["d"] as? [String: Any])
        XCTAssertNil(data["authentication"])
        XCTAssertEqual(data["eventSubscriptions"] as? Int, 0)
        XCTAssertEqual(data["rpcVersion"] as? Int, 1)

        let locked: [String: Any] = ["rpcVersion": 1, "authentication": ["salt": "s", "challenge": "c"]]
        let signed = try ObsProtocol.decode(try ObsProtocol.identify(hello: locked, password: "pw"))
        XCTAssertEqual((signed["d"] as? [String: Any])?["authentication"] as? String,
                       ObsProtocol.authentication(password: "pw", salt: "s", challenge: "c"))
        for missing in [nil, ""] {
            XCTAssertThrowsError(try ObsProtocol.identify(hello: locked, password: missing)) {
                XCTAssertEqual(($0 as? ObsError)?.failure, .passwordNeeded)
            }
        }
    }

    func testScreenshotRequestAsksForJpegAtFullSize() throws {
        let request = try ObsProtocol.decode(ObsProtocol.screenshotRequest(id: "7", source: "Capture Card"))
        XCTAssertEqual(request["op"] as? Int, 6)
        let d = try XCTUnwrap(request["d"] as? [String: Any])
        XCTAssertEqual(d["requestType"] as? String, "GetSourceScreenshot")
        XCTAssertEqual(d["requestId"] as? String, "7")
        let data = try XCTUnwrap(d["requestData"] as? [String: Any])
        XCTAssertEqual(data["sourceName"] as? String, "Capture Card")
        XCTAssertEqual(data["imageFormat"] as? String, "jpg")
        XCTAssertEqual(data["imageCompressionQuality"] as? Int, 90)
        XCTAssertNil(data["imageWidth"])
    }

    func testOnlyTheMatchingResponseIsTaken() {
        let message: [String: Any] = ["op": 7, "d": ["requestId": "2", "requestStatus": ["result": true]]]
        XCTAssertNil(ObsProtocol.response(message, id: "1"))
        XCTAssertNotNil(ObsProtocol.response(message, id: "2"))
        XCTAssertNil(ObsProtocol.response(["op": 5, "d": ["requestId": "2"]], id: "2"))
    }

    func testFailedRequestsMapToFailures() {
        let missing: [String: Any] = ["requestStatus": ["result": false, "code": 600, "comment": "No source was found by the name of `x`."]]
        XCTAssertThrowsError(try ObsProtocol.responseData(missing)) {
            XCTAssertEqual(($0 as? ObsError)?.failure, .sourceMissing)
            XCTAssertTrue(($0 as? ObsError)?.detail?.contains("No source") == true)
        }
        XCTAssertThrowsError(try ObsProtocol.responseData(["requestStatus": ["result": false, "code": 702]])) {
            XCTAssertEqual(($0 as? ObsError)?.failure, .failed)
        }
        XCTAssertEqual(try ObsProtocol.responseData(["requestStatus": ["result": true], "responseData": ["a": 1]])["a"] as? Int, 1)
    }

    func testImageDataUriIsDecoded() throws {
        XCTAssertEqual(try ObsProtocol.imageBytes(["imageData": "data:image/jpg;base64,AQID"]), Data([1, 2, 3]))
        for broken in ["data:image/jpg;base64,@@@", ""] {
            XCTAssertThrowsError(try ObsProtocol.imageBytes(["imageData": broken])) {
                XCTAssertEqual(($0 as? ObsError)?.failure, .failed)
            }
        }
        XCTAssertThrowsError(try ObsProtocol.imageBytes([:]))
    }

    func testInputsListCaptureDevicesFirstAndHideAudio() {
        let data: [String: Any] = ["inputs": [
            ["inputName": "Mic/Aux", "inputKind": "coreaudio_input_capture", "unversionedInputKind": "coreaudio_input_capture"],
            ["inputName": "Webcam Overlay", "inputKind": "browser_source", "unversionedInputKind": "browser_source"],
            ["inputName": "Elgato 4K", "inputKind": "macos-avcapture", "unversionedInputKind": "macos-avcapture"],
            ["inputName": "Altyazı", "inputKind": "browser_source"],
            ["inputName": "Desktop Audio", "inputKind": "wasapi_output_capture"],
            ["inputName": "", "inputKind": "browser_source"],
        ]]
        let inputs = ObsProtocol.inputs(data)
        XCTAssertEqual(inputs.map(\.name), ["Elgato 4K", "Altyazı", "Webcam Overlay"])
        XCTAssertTrue(inputs[0].isVideoCapture)
        XCTAssertEqual(ObsProtocol.inputs([:]), [])
    }

    func testCloseCodes() {
        XCTAssertEqual(ObsProtocol.closeFailure(4009), .passwordRejected)
        XCTAssertEqual(ObsProtocol.closeFailure(4010), .unsupported)
        XCTAssertEqual(ObsProtocol.closeFailure(1000), .unreachable)
        XCTAssertEqual(ObsProtocol.closeFailure(nil), .unreachable)
    }
}

final class ObsWebSocketClientTests: XCTestCase {
    private func connectError(_ server: FakeObsServer, password: String?) async -> ObsError? {
        let endpoint = await server.start()
        do {
            let connection = try await ObsWebSocketClient.connect(to: endpoint, password: password)
            connection.close()
            return nil
        } catch {
            return error as? ObsError
        }
    }

    func testConnectsWithPasswordAndReadsScreenshots() async throws {
        let server = try FakeObsServer()
        server.password = "secret"
        server.handle = { d in
            d["requestType"] as? String == "GetSourceScreenshot" ? (true, 100, ["imageData": "data:image/jpg;base64,AQID"]) : (false, 204, [:])
        }
        let endpoint = await server.start()
        defer { server.stop() }

        let client = try await ObsWebSocketClient.connect(to: endpoint, password: "secret")
        defer { client.close() }
        let first = try await client.screenshot(source: "Capture Card")
        let second = try await client.screenshot(source: "Capture Card")
        XCTAssertEqual(first, Data([1, 2, 3]))
        XCTAssertEqual(second, Data([1, 2, 3]))
        XCTAssertEqual(server.offeredProtocols, [ObsProtocol.subProtocol])
        let requests = server.requests
        XCTAssertEqual(requests.count, 2)
        XCTAssertEqual((requests[0]["requestData"] as? [String: Any])?["sourceName"] as? String, "Capture Card")
        XCTAssertNotEqual(requests[0]["requestId"] as? String, requests[1]["requestId"] as? String)
    }

    func testConnectsWithoutPasswordWhenObsHasNone() async throws {
        let server = try FakeObsServer()
        server.handle = { _ in (true, 100, ["inputs": [["inputName": "Capture", "unversionedInputKind": "macos-avcapture"]]]) }
        let endpoint = await server.start()
        defer { server.stop() }
        let inputs = try await ObsSourceReader.listInputs(endpoint: endpoint, password: nil)
        XCTAssertEqual(inputs, [ObsInput(name: "Capture", kind: "macos-avcapture")])
    }

    func testWrongPasswordIsRejected() async throws {
        let server = try FakeObsServer()
        server.password = "secret"
        defer { server.stop() }
        let error = await connectError(server, password: "wrong")
        XCTAssertEqual(error?.failure, .passwordRejected)
    }

    func testMissingPasswordIsAskedFor() async throws {
        let server = try FakeObsServer()
        server.password = "secret"
        defer { server.stop() }
        let error = await connectError(server, password: nil)
        XCTAssertEqual(error?.failure, .passwordNeeded)
    }

    func testUnknownSourceIsReported() async throws {
        let server = try FakeObsServer()
        server.handle = { _ in (false, 600, [:]) }
        let endpoint = await server.start()
        defer { server.stop() }
        let client = try await ObsWebSocketClient.connect(to: endpoint, password: nil)
        defer { client.close() }
        do {
            _ = try await client.screenshot(source: "Nope")
            XCTFail("Bilinmeyen kaynak görüntü vermemeli")
        } catch {
            XCTAssertEqual((error as? ObsError)?.failure, .sourceMissing)
        }
    }

    func testClosedPortIsUnreachable() async throws {
        // Boş bir port bul ve kapat: orada dinleyen yok.
        let server = try FakeObsServer()
        let endpoint = await server.start()
        server.stop()
        try await Task.sleep(for: .milliseconds(100))
        let begin = ContinuousClock.now
        do {
            let client = try await ObsWebSocketClient.connect(to: endpoint, password: nil)
            client.close()
            XCTFail("Kapalı port bağlanmamalı")
        } catch {
            XCTAssertEqual((error as? ObsError)?.failure, .unreachable)
        }
        XCTAssertLessThan(ContinuousClock.now - begin, .seconds(6))
    }

    func testSilentServerTimesOut() async throws {
        // TCP kabul edilip hiç yanıt gelmezse bağlantı süresinde bırakılır.
        let silent = try NWListener(using: .tcp, on: .any)
        let queue = DispatchQueue(label: "silent-obs")
        let held = NSMutableArray()
        silent.newConnectionHandler = { connection in queue.async { held.add(connection); connection.start(queue: queue) } }
        let ready = expectation(description: "dinliyor")
        silent.stateUpdateHandler = { if case .ready = $0 { ready.fulfill() } }
        silent.start(queue: queue)
        await fulfillment(of: [ready], timeout: 3)
        defer { silent.cancel() }

        let begin = ContinuousClock.now
        do {
            let client = try await ObsWebSocketClient.connect(to: ObsEndpoint(host: "127.0.0.1", port: Int(silent.port!.rawValue)),
                                                              password: nil)
            client.close()
            XCTFail("Yanıtsız sunucu bağlanmamalı")
        } catch {
            XCTAssertEqual((error as? ObsError)?.failure, .unreachable)
        }
        XCTAssertLessThan(ContinuousClock.now - begin, .seconds(8))
    }
}

/// Sırayla verilen görüntüleri ya da hataları döndüren sahte OBS bağlantısı.
final class FakeObsConnection: ObsConnection, @unchecked Sendable {
    private let lock = NSLock()
    private let screenshot: @Sendable (String) throws -> Data
    private var _screenshots = 0
    private var _closed = false
    var screenshots: Int { lock.withLock { _screenshots } }
    var closed: Bool { lock.withLock { _closed } }

    init(_ screenshot: @escaping @Sendable (String) throws -> Data) { self.screenshot = screenshot }

    func inputs() async throws -> [ObsInput] { [] }
    func screenshot(source: String) async throws -> Data {
        lock.withLock { _screenshots += 1 }
        return try screenshot(source)
    }
    func close() { lock.withLock { _closed = true } }
}

/// İş parçacıkları arasında paylaşılan kayıt.
final class Recorder<T>: @unchecked Sendable {
    private let lock = NSLock()
    private var items: [T] = []
    func add(_ item: T) { lock.withLock { items.append(item) } }
    var all: [T] { lock.withLock { items } }
}

final class ObsSourceReaderTests: XCTestCase {
    private let target = ObsSourceTarget(address: "localhost:4455", sourceName: "Capture Card")

    /// Testte görüntü çözülmez: ilk bayt genişlik olur, boş veri okunamaz sayılır.
    private static let decode: @Sendable (Data) -> CGImage? = { data in
        guard let width = data.first else { return nil }
        return CGContext(data: nil, width: Int(width), height: 1, bitsPerComponent: 8, bytesPerRow: 0,
                         space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)?.makeImage()
    }

    private func until(_ condition: () -> Bool) async {
        for _ in 0..<300 where !condition() { try? await Task.sleep(for: .milliseconds(10)) }
        XCTAssertTrue(condition())
    }

    private func reader(_ target: ObsSourceTarget? = nil, password: @escaping @Sendable () -> String? = { nil },
                        retry: Duration = .milliseconds(20), connect: @escaping ObsSourceReader.Connector) -> ObsSourceReader {
        ObsSourceReader(target: target ?? self.target, password: password, connect: connect, decode: Self.decode,
                        frameInterval: .milliseconds(5), retryDelay: retry)
    }

    func testDeliversImagesFromTheSelectedSource() async {
        let sources = Recorder<String>()
        let connection = FakeObsConnection { sources.add($0); return Data([16]) }
        let states = Recorder<ObsSourceState>()
        let images = Recorder<CGImage>()
        let reader = reader { _, _ in connection }
        reader.onState = states.add
        reader.onImage = images.add
        reader.start()
        await until { images.all.count >= 3 }
        reader.stop()
        XCTAssertEqual(states.all.first, .connecting)
        XCTAssertEqual(states.all.filter { $0 == .streaming }.count, 1) // aynı durum tekrar bildirilmez
        XCTAssertTrue(sources.all.allSatisfy { $0 == "Capture Card" })
        XCTAssertEqual(images.all.first?.width, 16)
    }

    func testUnreadableImagesAreSkipped() async {
        let calls = Recorder<Int>()
        let connection = FakeObsConnection { _ in
            calls.add(1)
            return calls.all.count % 2 == 1 ? Data() : Data([8])
        }
        let images = Recorder<CGImage>()
        let reader = reader { _, _ in connection }
        reader.onImage = images.add
        reader.start()
        await until { images.all.count >= 2 }
        reader.stop()
        XCTAssertGreaterThanOrEqual(calls.all.count, 4)
    }

    func testMissingSourceIsReportedAndRetriedWithTheCurrentPassword() async {
        let passwords = Recorder<String?>()
        let current = Recorder<String>()
        current.add("old")
        let attempts = Recorder<Int>()
        let states = Recorder<ObsSourceState>()
        let images = Recorder<CGImage>()
        let reader = reader(password: { current.all.last }) { _, secret in
            passwords.add(secret)
            attempts.add(1)
            return attempts.all.count == 1
                ? FakeObsConnection { _ in throw ObsError(.sourceMissing) }
                : FakeObsConnection { _ in Data([4]) }
        }
        reader.onState = states.add
        reader.onImage = images.add
        reader.start()
        await until { states.all.contains(.failed(.sourceMissing)) }
        current.add("new")
        await until { !images.all.isEmpty }
        reader.stop()
        XCTAssertEqual(Array(passwords.all.prefix(2)), ["old", "new"])
    }

    func testUnreachableObsIsRetriedUntilItOpens() async {
        let attempts = Recorder<Int>()
        let states = Recorder<ObsSourceState>()
        let images = Recorder<CGImage>()
        let reader = reader(retry: .milliseconds(10)) { _, _ in
            attempts.add(1)
            if attempts.all.count < 3 { throw ObsError(.unreachable) }
            return FakeObsConnection { _ in Data([2]) }
        }
        reader.onState = states.add
        reader.onImage = images.add
        reader.start()
        await until { !images.all.isEmpty }
        reader.stop()
        XCTAssertTrue(states.all.contains(.failed(.unreachable)))
        XCTAssertTrue(states.all.contains(.streaming))
    }

    func testStopEndsTheLoopAndClosesTheConnection() async throws {
        let connection = FakeObsConnection { _ in Data([2]) }
        let states = Recorder<ObsSourceState>()
        let reader = reader { _, _ in connection }
        reader.onState = states.add
        reader.start()
        await until { connection.screenshots >= 2 }
        reader.stop()
        await until { connection.closed }
        let after = connection.screenshots
        try await Task.sleep(for: .milliseconds(50))
        XCTAssertLessThanOrEqual(connection.screenshots, after + 1)
        XCTAssertEqual(states.all.last, .stopped)
    }

    func testReconnectNowOpensANewConnection() async {
        let connections = Recorder<FakeObsConnection>()
        let reader = reader { _, _ in
            let connection = FakeObsConnection { _ in Data([2]) }
            connections.add(connection)
            return connection
        }
        reader.reconnectNow() // çalışmıyorken bir şey yapmaz
        try? await Task.sleep(for: .milliseconds(30))
        XCTAssertTrue(connections.all.isEmpty)
        reader.start()
        await until { connections.all.count == 1 }
        reader.reconnectNow()
        await until { connections.all.count == 2 && connections.all[0].closed }
        reader.stop()
    }

    func testInvalidAddressIsReportedWithoutConnecting() async {
        let connects = Recorder<Int>()
        let states = Recorder<ObsSourceState>()
        let reader = reader(ObsSourceTarget(address: "wss://nope", sourceName: "Capture")) { _, _ in
            connects.add(1)
            return FakeObsConnection { _ in Data([1]) }
        }
        reader.onState = states.add
        reader.start()
        await until { states.all.contains(.failed(.invalidAddress)) }
        reader.stop()
        XCTAssertTrue(connects.all.isEmpty)
    }

    func testEveryFailureTellsTheUserWhatToDo() {
        let texts = ObsFailure.allCases.map { ObsSourceReader.describe($0, target: target) }
        XCTAssertTrue(texts.allSatisfy { !$0.isEmpty })
        XCTAssertEqual(Set(texts).count, texts.count)
        XCTAssertTrue(ObsSourceReader.describe(.unreachable, target: target).contains("localhost:4455"))
        XCTAssertTrue(ObsSourceReader.describe(.sourceMissing, target: target).contains("Capture Card"))
        XCTAssertTrue(ObsSourceReader.describe(.unreachable, target: nil).contains(ObsEndpoint.defaultAddress))
    }

    func testStatusTextFollowsTheReader() {
        XCTAssertEqual(ObsSourceReader.statusText(.stopped, running: false, target: target),
                       "Ready to read Capture Card from OBS when translation starts.")
        XCTAssertEqual(ObsSourceReader.statusText(.streaming, running: true, target: target), "Reading Capture Card from OBS")
        XCTAssertEqual(ObsSourceReader.statusText(.connecting, running: true, target: target), "Connecting to OBS…")
        XCTAssertEqual(ObsSourceReader.statusText(.failed(.unsupported), running: true, target: target),
                       ObsSourceReader.describe(.unsupported, target: target))
        XCTAssertEqual(ObsSourceReader.statusText(.streaming, running: true, target: nil),
                       "Choose an OBS source in Settings › Picture source.")
    }

    /// OBS'in gönderdiği gibi gerçek bir JPEG çözülür.
    func testJpegIsDecoded() throws {
        let context = try XCTUnwrap(CGContext(data: nil, width: 64, height: 36, bitsPerComponent: 8, bytesPerRow: 0,
                                              space: CGColorSpaceCreateDeviceRGB(),
                                              bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue))
        context.setFillColor(CGColor(red: 1, green: 1, blue: 1, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: 64, height: 36))
        let jpeg = NSMutableData()
        let destination = try XCTUnwrap(CGImageDestinationCreateWithData(jpeg, UTType.jpeg.identifier as CFString, 1, nil))
        CGImageDestinationAddImage(destination, try XCTUnwrap(context.makeImage()), nil)
        XCTAssertTrue(CGImageDestinationFinalize(destination))

        let image = try XCTUnwrap(ObsImage.decode(jpeg as Data))
        XCTAssertEqual(image.width, 64)
        XCTAssertEqual(image.height, 36)
        XCTAssertNil(ObsImage.decode(Data([1, 2, 3])))
    }
}

/// OBS kaynağı ayarları: eski kayıtlarda yoktur, kaydedilip geri okunur.
final class ObsSourceSettingsTests: XCTestCase {
    func testOldSettingsHaveNoObsSource() throws {
        let old = try JSONDecoder().decode(UserSettings.self, from: Data(#"{"frameSource":"remotePlayWindow"}"#.utf8))
        XCTAssertNil(old.obsSource)
        XCTAssertEqual(old.frameSource, .remotePlayWindow)
    }

    func testObsSourceRoundTrips() throws {
        var settings = UserSettings()
        settings.frameSource = .obsSource
        settings.obsSource = ObsSourceTarget(address: "192.168.1.20:4455", sourceName: "Elgato")
        let saved = try JSONDecoder().decode(UserSettings.self, from: JSONEncoder().encode(settings))
        XCTAssertEqual(saved.frameSource, .obsSource)
        XCTAssertEqual(saved.obsSource, settings.obsSource)
    }

    func testBrokenObsSourceKeepsOtherSettings() throws {
        let saved = try JSONDecoder().decode(UserSettings.self, from: Data(#"{"obsSource":7,"targetLanguage":"de"}"#.utf8))
        XCTAssertNil(saved.obsSource)
        XCTAssertEqual(saved.targetLanguage, "de")
    }

    func testOnlyThePS5SourceNeedsThePS5() {
        XCTAssertEqual(FrameSource.allCases.filter(\.needsPS5), [.ps5])
    }

    func testTargetNeedsAnAddressAndASource() {
        XCTAssertTrue(ObsSourceTarget(address: "localhost", sourceName: "Card").isComplete)
        XCTAssertFalse(ObsSourceTarget(address: "localhost", sourceName: "  ").isComplete)
        XCTAssertFalse(ObsSourceTarget(address: "not a host:x", sourceName: "Card").isComplete)
        XCTAssertEqual(ObsSourceTarget().address, ObsEndpoint.defaultAddress)
    }
}
