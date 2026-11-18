// SPDX-License-Identifier: AGPL-3.0-only
// OBS'teki bir kaynağın görüntüsünü sürekli okuyan döngü (Windows'taki CouchSubs.Shell/ObsSourceStream.cs ile aynı davranış).

import CoreGraphics
import Foundation
import ImageIO

/// Ayarlar'da seçilen OBS kaynağı: OBS'in adresi ve kaynağın (ör. capture card'ın Video Yakalama Aygıtı) adı.
public struct ObsSourceTarget: Codable, Equatable, Sendable {
    public var address: String
    public var sourceName: String

    public init(address: String = ObsEndpoint.defaultAddress, sourceName: String = "") {
        self.address = address
        self.sourceName = sourceName
    }

    /// Çeviri başlatılabilir mi: adres geçerli ve kaynak seçili.
    public var isComplete: Bool {
        ObsEndpoint.parse(address) != nil && !sourceName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}

/// Okuyucunun durumu. `failed` bağlantı ya da istek hatasıdır; okuyucu kendisi yeniden dener (geçersiz adres hariç).
public enum ObsSourceState: Equatable, Sendable {
    case connecting
    case streaming
    case failed(ObsFailure)
    case stopped
}

/// OBS'in gönderdiği JPEG'i yazı tanımanın okuyacağı kareye çevirir.
public enum ObsImage {
    public static func decode(_ data: Data) -> CGImage? {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { return nil }
        let image = CGImageSourceCreateImageAtIndex(source, 0, [kCGImageSourceShouldCacheImmediately: true] as CFDictionary)
        guard let image, image.width > 0, image.height > 0 else { return nil }
        return image
    }
}

/// Görüntüyü OBS'teki bir kaynaktan (ör. capture card) obs-websocket ile alır. CouchSubs capture card'ı kendisi açmaz:
/// kartı OBS tutar, CouchSubs yalnız o kaynağın anlık görüntüsünü ister (GetSourceScreenshot). Yayın, kayıt ve sahneler
/// değişmez. Kaynağın kendisi okunduğu için sahnedeki Türkçe altyazı katmanı görüntüye girmez; çeviri kendi üstüne geri
/// beslenmez. Kareler `onImage` ile gelir; okuma hattının geri kalanı aynıdır.
public final class ObsSourceReader: @unchecked Sendable {
    /// Saniyede en çok 8 kare: altyazı saniyelerce ekranda kalır; fazlası yalnız OBS'i ve işlemciyi yorar.
    public static let defaultFrameInterval: Duration = .milliseconds(125)
    public static let defaultRetryDelay: Duration = .seconds(2)

    public typealias Connector = @Sendable (ObsEndpoint, String?) async throws -> ObsConnection

    public var onImage: ((CGImage) -> Void)?
    public var onState: ((ObsSourceState) -> Void)?

    public let target: ObsSourceTarget
    private let password: @Sendable () -> String?
    private let connect: Connector
    private let decode: @Sendable (Data) -> CGImage?
    private let frameInterval: Duration
    private let retryDelay: Duration
    private let lock = NSLock()
    private var loop: Task<Void, Never>?
    private var generation = 0
    private var reported: ObsSourceState?
    private var lastLogged: String?

    /// - Parameter password: Her bağlantıda yeniden okunur: Ayarlar'da parola değişince yeniden bağlanmak yeter.
    public init(target: ObsSourceTarget, password: @escaping @Sendable () -> String?,
                connect: Connector? = nil, decode: @escaping @Sendable (Data) -> CGImage? = { ObsImage.decode($0) },
                frameInterval: Duration = ObsSourceReader.defaultFrameInterval,
                retryDelay: Duration = ObsSourceReader.defaultRetryDelay) {
        self.target = target
        self.password = password
        self.connect = connect ?? { endpoint, secret in try await ObsWebSocketClient.connect(to: endpoint, password: secret) }
        self.decode = decode
        self.frameInterval = frameInterval
        self.retryDelay = retryDelay
    }

    public func start() { restart() }

    /// Çalışıyorsa bağlantıyı hemen yeniler (ör. parola değişti, Mac uykudan uyandı).
    public func reconnectNow() {
        guard lock.withLock({ loop != nil }) else { return }
        restart()
    }

    public func stop() {
        let old: Task<Void, Never>? = lock.withLock {
            let old = loop
            loop = nil
            generation += 1
            reported = nil
            return old
        }
        guard let old else { return }
        old.cancel()
        onState?(.stopped)
    }

    private func restart() {
        let id: Int = lock.withLock {
            loop?.cancel()
            generation += 1
            reported = nil
            return generation
        }
        report(.connecting, generation: id)
        let task = Task.detached { [weak self] in
            guard let self else { return }
            await self.run(generation: id)
        }
        lock.withLock { if generation == id { loop = task } else { task.cancel() } }
    }

    private func run(generation id: Int) async {
        guard let endpoint = ObsEndpoint.parse(target.address) else {
            report(.failed(.invalidAddress), generation: id)
            return
        }
        while !Task.isCancelled {
            var connection: ObsConnection?
            do {
                let open = try await connect(endpoint, password())
                connection = open
                while !Task.isCancelled {
                    let started = ContinuousClock.now
                    let bytes = try await open.screenshot(source: target.sourceName)
                    if let image = decode(bytes) {
                        report(.streaming, generation: id)
                        if isCurrent(id) { onImage?(image) }
                    }
                    let wait = frameInterval - (ContinuousClock.now - started)
                    if wait > .zero { try await Task.sleep(for: wait) }
                }
            } catch is CancellationError {
                connection?.close()
                break
            } catch {
                if Task.isCancelled {
                    connection?.close()
                    break
                }
                let failure = (error as? ObsError)?.failure ?? .failed
                log(String(describing: error))
                report(.failed(failure), generation: id)
            }
            connection?.close()
            do { try await Task.sleep(for: retryDelay) } catch { break }
        }
    }

    private func isCurrent(_ id: Int) -> Bool { lock.withLock { generation == id } }

    /// Durdurulan ya da yeniden başlatılan döngü artık bildirim yapmaz; aynı durum iki kez bildirilmez.
    private func report(_ state: ObsSourceState, generation id: Int) {
        let changed: Bool = lock.withLock {
            guard generation == id, reported != state else { return false }
            reported = state
            return true
        }
        if changed { onState?(state) }
    }

    /// Aynı hata her denemede günlüğü doldurmasın.
    private func log(_ message: String) {
        let fresh: Bool = lock.withLock {
            guard lastLogged != message else { return false }
            lastLogged = message
            return true
        }
        if fresh { onLog?("OBS kaynağı (\(target.sourceName)): \(message)") }
    }

    /// Hata ayrıntısının yazılacağı günlük (Mac'te remoteplay.log).
    public var onLog: ((String) -> Void)?

    /// Ayarlar'daki kaynak listesi için kısa bir bağlantı açar.
    public static func listInputs(endpoint: ObsEndpoint, password: String?) async throws -> [ObsInput] {
        let connection = try await ObsWebSocketClient.connect(to: endpoint, password: password)
        defer { connection.close() }
        return try await connection.inputs()
    }

    /// Hatanın kullanıcıya söylenişi: ne yapacağı yazılır (ana ekran ve Ayarlar aynı metni kullanır).
    public static func describe(_ failure: ObsFailure, target: ObsSourceTarget?) -> String {
        let address = ObsEndpoint.parse(target?.address)?.description ?? target?.address ?? ObsEndpoint.defaultAddress
        let source = target?.sourceName ?? ""
        switch failure {
        case .invalidAddress:
            return String(localized: "Enter the OBS address as host:port, for example localhost:4455.")
        case .passwordNeeded:
            return String(localized: "OBS asks for the WebSocket password. Enter it in Settings › Picture source.")
        case .passwordRejected:
            return String(localized: "OBS did not accept the WebSocket password. Copy it again from Tools › WebSocket Server Settings › Show Connect Info.")
        case .sourceMissing:
            return String(format: String(localized: "OBS has no source named %@. Choose the capture card source again in Settings › Picture source."), source)
        case .unsupported:
            return String(localized: "This OBS version is too old. Use OBS 28 or later.")
        case .failed where !source.isEmpty:
            return String(format: String(localized: "OBS could not take a picture of %@. Make sure the source is shown in a scene."), source)
        case .failed:
            return String(localized: "OBS could not complete the request. Try again.")
        case .unreachable:
            return String(format: String(localized: "OBS is not reachable at %@. Open OBS and turn on Tools › WebSocket Server Settings › Enable WebSocket server."), address)
        }
    }

    /// Ana ekrandaki görüntü kaynağı durumu.
    public static func statusText(_ state: ObsSourceState, running: Bool, target: ObsSourceTarget?) -> String {
        guard let target, target.isComplete else { return String(localized: "Choose an OBS source in Settings › Picture source.") }
        switch state {
        case .streaming where running:
            return String(format: String(localized: "Reading %@ from OBS"), target.sourceName)
        case .connecting where running:
            return String(localized: "Connecting to OBS…")
        case .failed(let failure) where running:
            return describe(failure, target: target)
        default:
            return String(format: String(localized: "Ready to read %@ from OBS when translation starts."), target.sourceName)
        }
    }
}
