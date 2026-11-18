// SPDX-License-Identifier: AGPL-3.0-only
// obs-websocket 5 istemcisi (Windows'taki CouchSubs.Core/ObsWebSocket.cs ile aynı protokol ve davranış).

import CryptoKit
import Foundation

/// OBS'in WebSocket sunucusunun adresi (OBS 28 ve sonrasında yerleşik obs-websocket 5; Araçlar › WebSocket Sunucu
/// Ayarları). Varsayılan bu bilgisayardaki OBS: `localhost:4455`.
public struct ObsEndpoint: Equatable, Sendable, CustomStringConvertible {
    public static let defaultPort = 4455
    public static let defaultAddress = "localhost:4455"

    public let host: String
    public let port: Int

    public init(host: String, port: Int) {
        self.host = host
        self.port = port
    }

    /// IPv6 adresi köşeli parantezle yazılır: `[::1]:4455`.
    public var description: String { host.contains(":") ? "[\(host)]:\(port)" : "\(host):\(port)" }

    public var url: URL { URL(string: "ws://\(description)")! }

    /// "localhost", "localhost:4455", "192.168.1.20:4455" ya da "ws://…" kabul edilir; port yazılmazsa 4455. obs-websocket
    /// TLS konuşmaz; wss, yol, sorgu ve kullanıcı bilgisi olan adresler geçersizdir (nil).
    public static func parse(_ address: String?) -> ObsEndpoint? {
        guard var text = address?.trimmingCharacters(in: .whitespacesAndNewlines), !text.isEmpty else { return nil }
        if !text.contains("://") { text = "ws://" + text }
        guard let parts = URLComponents(string: text), parts.scheme?.lowercased() == "ws",
              var host = parts.host, !host.isEmpty,
              parts.path.isEmpty || parts.path == "/", parts.query == nil, parts.fragment == nil,
              parts.user == nil, parts.password == nil else { return nil }
        if host.hasPrefix("["), host.hasSuffix("]") { host = String(host.dropFirst().dropLast()) }
        let port = parts.port ?? defaultPort
        guard (1...65535).contains(port), URL(string: "ws://\(ObsEndpoint(host: host, port: port))") != nil else { return nil }
        return ObsEndpoint(host: host.lowercased(), port: port)
    }
}

/// OBS'teki bir kaynak (girdi): ad ve türü (ör. "macos-avcapture" = Video Yakalama Aygıtı).
public struct ObsInput: Equatable, Hashable, Sendable {
    public let name: String
    public let kind: String

    public init(name: String, kind: String) {
        self.name = name
        self.kind = kind
    }

    /// Capture card ve kamera türleri (Windows, Mac, Linux, Blackmagic). Listede önce bunlar gösterilir.
    static let videoCaptureKinds: Set<String> = ["dshow_input", "decklink-input", "av_capture_input", "macos-avcapture",
                                                 "macos-avcapture-fast", "v4l2_input"]

    public var isVideoCapture: Bool { Self.videoCaptureKinds.contains(kind) }

    /// Yalnız ses veren kaynaklar (mikrofon, masaüstü sesi); görüntüleri yoktur, listede gösterilmez.
    public var isAudioOnly: Bool {
        kind.range(of: "audio", options: .caseInsensitive) != nil
            || ["wasapi_", "pulse_", "alsa_", "jack_", "coreaudio_"].contains { kind.hasPrefix($0) }
    }
}

public enum ObsFailure: Equatable, Sendable, CaseIterable {
    /// Adres host:port biçiminde değil.
    case invalidAddress
    /// OBS açık değil, WebSocket sunucusu kapalı ya da yanıt vermedi.
    case unreachable
    /// OBS parola istiyor, kayıtlı parola yok.
    case passwordNeeded
    /// OBS parolayı kabul etmedi (kapanış kodu 4009).
    case passwordRejected
    /// Bu adda kaynak yok (istek durumu 600).
    case sourceMissing
    /// OBS bu protokol sürümünü konuşmuyor (4010; OBS 28'den eski).
    case unsupported
    /// OBS isteği yapamadı (ör. kaynak görünmüyor, görüntüsü alınamadı).
    case failed
}

public struct ObsError: Error, Equatable, CustomStringConvertible {
    public let failure: ObsFailure
    public let detail: String?

    public init(_ failure: ObsFailure, _ detail: String? = nil) {
        self.failure = failure
        self.detail = detail
    }

    public var description: String {
        guard let detail, !detail.isEmpty else { return "OBS: \(failure)" }
        return "OBS: \(failure) (\(detail))"
    }
}

/// obs-websocket 5 protokolünün CouchSubs'ın kullandığı kısmı: kimlik doğrulama, kaynak listesi ve kaynağın anlık
/// görüntüsü (GetSourceScreenshot). OBS'in yayınına, kaydına ya da sahnelerine hiçbir şey yazılmaz; yalnız okunur.
/// Belge: https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md
public enum ObsProtocol {
    public static let rpcVersion = 1
    public static let subProtocol = "obswebsocket.json"
    /// JPEG: OBS'te PNG'den kat kat hızlı sıkışır ve mesaj küçük kalır. Kalite 90, yazı tanımanın okuduğu beyaz yazıyı
    /// PS5 akışının kendi sıkıştırmasından daha az bozar.
    public static let imageFormat = "jpg"
    public static let imageQuality = 90
    /// 4K bir karenin JPEG'i bile birkaç MB'dır; bundan büyük mesaj bozuk sayılır.
    public static let maxMessageBytes = 64 * 1024 * 1024

    static let opHello = 0, opIdentify = 1, opIdentified = 2, opRequest = 6, opRequestResponse = 7
    static let statusResourceNotFound = 600

    /// base64(sha256(base64(sha256(parola + tuz)) + meydan okuma)); parola OBS'e hiç düz gönderilmez.
    public static func authentication(password: String, salt: String, challenge: String) -> String {
        let secret = Data(SHA256.hash(data: Data((password + salt).utf8))).base64EncodedString()
        return Data(SHA256.hash(data: Data((secret + challenge).utf8))).base64EncodedString()
    }

    public static func op(_ message: [String: Any]) -> Int { (message["op"] as? NSNumber)?.intValue ?? -1 }

    /// Hello'nun (op 0) "d" alanına verilecek Identify (op 1). Olay aboneliği yoktur: OBS yalnız isteklerin yanıtını gönderir.
    public static func identify(hello: [String: Any], password: String?) throws -> Data {
        var data: [String: Any] = ["rpcVersion": rpcVersion, "eventSubscriptions": 0]
        if let auth = hello["authentication"] as? [String: Any] {
            guard let password, !password.isEmpty else { throw ObsError(.passwordNeeded) }
            data["authentication"] = authentication(password: password, salt: auth["salt"] as? String ?? "",
                                                    challenge: auth["challenge"] as? String ?? "")
        }
        return message(op: opIdentify, data: data)
    }

    public static func request(_ requestType: String, id: String, data requestData: [String: Any]? = nil) -> Data {
        var data: [String: Any] = ["requestType": requestType, "requestId": id]
        if let requestData { data["requestData"] = requestData }
        return message(op: opRequest, data: data)
    }

    /// Kaynağın kendi çözünürlüğündeki görüntüsü (genişlik/yükseklik verilmez: yazı tanıma küçültülmemiş kareyi okur).
    public static func screenshotRequest(id: String, source: String) -> Data {
        request("GetSourceScreenshot", id: id, data: ["sourceName": source, "imageFormat": imageFormat,
                                                      "imageCompressionQuality": imageQuality])
    }

    public static func inputListRequest(id: String) -> Data { request("GetInputList", id: id) }

    public static func isHello(_ message: [String: Any]) -> Bool { op(message) == opHello }
    public static func isIdentified(_ message: [String: Any]) -> Bool { op(message) == opIdentified }

    /// Mesaj bu isteğin yanıtıysa (op 7, aynı requestId) "d" alanı.
    public static func response(_ message: [String: Any], id: String) -> [String: Any]? {
        guard op(message) == opRequestResponse, let data = message["d"] as? [String: Any],
              data["requestId"] as? String == id else { return nil }
        return data
    }

    /// Başarılı yanıtın responseData'sı; başarısızsa `ObsError`.
    public static func responseData(_ response: [String: Any]) throws -> [String: Any] {
        let status = response["requestStatus"] as? [String: Any] ?? [:]
        if status["result"] as? Bool == true { return response["responseData"] as? [String: Any] ?? [:] }
        let code = (status["code"] as? NSNumber)?.intValue ?? 0
        let comment = status["comment"] as? String
        throw ObsError(code == statusResourceNotFound ? .sourceMissing : .failed, comment ?? (code == 0 ? nil : String(code)))
    }

    /// "data:image/jpg;base64,…" biçimindeki görüntünün baytları.
    public static func imageBytes(_ responseData: [String: Any]) throws -> Data {
        guard let text = responseData["imageData"] as? String, !text.isEmpty else { throw ObsError(.failed, "OBS returned no image.") }
        let payload = text.firstIndex(of: ",").map { text[text.index(after: $0)...] } ?? text[...]
        guard let bytes = Data(base64Encoded: String(payload)), !bytes.isEmpty else {
            throw ObsError(.failed, "OBS returned an unreadable image.")
        }
        return bytes
    }

    /// Görüntüsü olan kaynaklar: önce capture card ve kameralar, sonra diğerleri, adlarına göre.
    public static func inputs(_ responseData: [String: Any]) -> [ObsInput] {
        let list = (responseData["inputs"] as? [[String: Any]] ?? []).compactMap { input -> ObsInput? in
            guard let name = input["inputName"] as? String, !name.isEmpty else { return nil }
            let unversioned = input["unversionedInputKind"] as? String ?? ""
            let item = ObsInput(name: name, kind: unversioned.isEmpty ? input["inputKind"] as? String ?? "" : unversioned)
            return item.isAudioOnly ? nil : item
        }
        return list.sorted {
            if $0.isVideoCapture != $1.isVideoCapture { return $0.isVideoCapture }
            return $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending
        }
    }

    /// OBS'in kapanış kodları: 4009 parola yanlış, 4010 desteklenmeyen sürüm.
    public static func closeFailure(_ code: Int?) -> ObsFailure {
        switch code {
        case 4009: .passwordRejected
        case 4010: .unsupported
        default: .unreachable
        }
    }

    public static func decode(_ data: Data) throws -> [String: Any] {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw ObsError(.failed, "OBS sent an unreadable message.")
        }
        return object
    }

    private static func message(op: Int, data: [String: Any]) -> Data {
        // Yalnız dize, sayı ve sözlükler: serileştirme başarısız olamaz.
        (try? JSONSerialization.data(withJSONObject: ["op": op, "d": data], options: [.sortedKeys])) ?? Data()
    }
}

/// OBS'e açık bir bağlantı. Testlerde sahtesi kullanılır.
public protocol ObsConnection: AnyObject, Sendable {
    func inputs() async throws -> [ObsInput]
    /// Kaynağın o anki görüntüsü (JPEG baytları).
    func screenshot(source: String) async throws -> Data
    func close()
}

/// obs-websocket 5 istemcisi. İstekler sırayla gider; aynı anda tek istek açıktır (görüntü akışı her kareyi bir öncekinin
/// yanıtından sonra ister; Ayarlar'daki kaynak listesi kendi kısa bağlantısını açar).
public final class ObsWebSocketClient: NSObject, ObsConnection, URLSessionWebSocketDelegate, @unchecked Sendable {
    public static let connectTimeout: Duration = .seconds(5)
    public static let requestTimeout: Duration = .seconds(5)

    private var session: URLSession!
    private var task: URLSessionWebSocketTask!
    private let lock = NSLock()
    private var receivedCloseCode: Int?
    private var nextId = 0
    private var busy = false

    private init(endpoint: ObsEndpoint) {
        super.init()
        session = URLSession(configuration: .ephemeral, delegate: self, delegateQueue: nil)
        task = session.webSocketTask(with: endpoint.url, protocols: [ObsProtocol.subProtocol])
        task.maximumMessageSize = ObsProtocol.maxMessageBytes
    }

    /// Bağlanır, Hello'yu bekler, gerekiyorsa parolayla kimliğini doğrular. Hatalar `ObsError`'dır.
    public static func connect(to endpoint: ObsEndpoint, password: String?) async throws -> ObsConnection {
        let client = ObsWebSocketClient(endpoint: endpoint)
        client.task.resume()
        do {
            try await client.withTimeout(connectTimeout) {
                let hello = try await client.receive()
                guard ObsProtocol.isHello(hello) else { throw ObsError(.unsupported, "No Hello from OBS.") }
                try await client.send(ObsProtocol.identify(hello: hello["d"] as? [String: Any] ?? [:], password: password))
                guard ObsProtocol.isIdentified(try await client.receive()) else {
                    throw ObsError(.unsupported, "OBS did not confirm the connection.")
                }
            }
            return client
        } catch {
            // Kapanış kodu (ör. 4009 parola yanlış) bağlantı bırakılmadan okunur.
            let failure = await client.translate(error)
            client.close()
            if Task.isCancelled { throw CancellationError() }
            throw failure
        }
    }

    public func inputs() async throws -> [ObsInput] {
        ObsProtocol.inputs(try await request { ObsProtocol.inputListRequest(id: $0) })
    }

    public func screenshot(source: String) async throws -> Data {
        try ObsProtocol.imageBytes(try await request { ObsProtocol.screenshotRequest(id: $0, source: source) })
    }

    public func close() {
        task.cancel(with: .normalClosure, reason: nil)
        // URLSession temsilcisini (self) güçlü tutar; kapatılmazsa istemci hiç bırakılmaz.
        session.invalidateAndCancel()
    }

    // MARK: - İç işleyiş

    private func request(_ build: (String) -> Data) async throws -> [String: Any] {
        let id: String = try lock.withLock {
            guard !busy else { throw ObsError(.failed, "Another OBS request is still running.") }
            busy = true
            nextId += 1
            return String(nextId)
        }
        defer { lock.withLock { busy = false } }
        let message = build(id)
        do {
            return try await withTimeout(Self.requestTimeout) { [self] in
                try await send(message)
                while true {
                    if let response = ObsProtocol.response(try await receive(), id: id) {
                        return try ObsProtocol.responseData(response)
                    }
                }
            }
        } catch let error as ObsError {
            throw error
        } catch {
            if Task.isCancelled { throw CancellationError() }
            throw await translate(error)
        }
    }

    private func send(_ data: Data) async throws {
        try await task.send(.string(String(decoding: data, as: UTF8.self)))
    }

    private func receive() async throws -> [String: Any] {
        switch try await task.receive() {
        case .string(let text): return try ObsProtocol.decode(Data(text.utf8))
        case .data(let data): return try ObsProtocol.decode(data)
        @unknown default: throw ObsError(.failed, "OBS sent an unreadable message.")
        }
    }

    /// `receive()` iptale yanıt vermez; süre dolunca ya da görev iptal edilince bağlantı kesilir, o da sonlanır.
    private func withTimeout<T>(_ limit: Duration, _ body: @escaping @Sendable () async throws -> T) async throws -> T {
        let task = self.task!
        let timedOut = ManagedFlag()
        return try await withTaskCancellationHandler {
            try await withThrowingTaskGroup(of: Optional<T>.self) { group in
                group.addTask { try await body() }
                group.addTask {
                    try await Task.sleep(for: limit)
                    timedOut.set()
                    task.cancel()
                    return nil
                }
                defer { group.cancelAll() }
                do {
                    while let next = try await group.next() {
                        if let value = next { return value }
                        if timedOut.isSet { break }
                    }
                } catch {
                    if timedOut.isSet { throw ObsError(.unreachable, "OBS did not answer in time.") }
                    throw error
                }
                throw ObsError(.unreachable, "OBS did not answer in time.")
            }
        } onCancel: {
            task.cancel()
        }
    }

    /// Soket hatasını kullanıcıya söylenecek hataya çevirir. OBS kapanış çerçevesini hatadan hemen sonra bildirebilir;
    /// kod kısa bir süre beklenir.
    private func translate(_ error: Error) async -> ObsError {
        if let error = error as? ObsError, error.failure != .unreachable { return error }
        for _ in 0..<10 {
            if let code = closeCode, ObsProtocol.closeFailure(code) != .unreachable {
                return ObsError(ObsProtocol.closeFailure(code), "OBS closed the connection (\(code)).")
            }
            if Task.isCancelled { break }
            try? await Task.sleep(for: .milliseconds(20))
        }
        if let error = error as? ObsError { return error }
        return ObsError(.unreachable, (error as NSError).localizedDescription)
    }

    private var closeCode: Int? {
        let seen = lock.withLock { receivedCloseCode }
        if let seen { return seen }
        let code = task.closeCode.rawValue
        return code == URLSessionWebSocketTask.CloseCode.invalid.rawValue ? nil : code
    }

    public func urlSession(_ session: URLSession, webSocketTask: URLSessionWebSocketTask,
                           didCloseWith closeCode: URLSessionWebSocketTask.CloseCode, reason: Data?) {
        lock.withLock { receivedCloseCode = closeCode.rawValue }
    }
}

/// İş parçacıkları arasında paylaşılan tek seferlik bayrak.
final class ManagedFlag: @unchecked Sendable {
    private let lock = NSLock()
    private var value = false
    func set() { lock.withLock { value = true } }
    var isSet: Bool { lock.withLock { value } }
}
