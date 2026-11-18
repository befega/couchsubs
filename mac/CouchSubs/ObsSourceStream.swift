// SPDX-License-Identifier: AGPL-3.0-only

import CoreGraphics
import CouchSubsCore
import RemotePlay

/// Görüntüyü OBS'teki bir kaynaktan (ör. capture card'ın Video Yakalama Aygıtı) obs-websocket ile alır (bkz.
/// `ObsSourceReader`). PS5'e bağlanılmaz, capture card'ı OBS tutmaya devam eder; yayın ve kayıt değişmez. Kareler
/// `StreamSession`'ınkiler gibi `onFrame` ile gelir; okuma hattının geri kalanı aynıdır.
final class ObsSourceStream: StreamControlling {
    var onFrame: ((CGImage) -> Void)?
    var onState: ((SessionState) -> Void)?
    /// Bu kaynakta PS5'in adresi bilinmez; hiç çağrılmaz.
    var onConsoleMoved: ((RegisteredConsole) -> Void)?

    private let reader: ObsSourceReader

    /// - Parameter password: Her bağlantıda Anahtarlık'tan yeniden okunur.
    init(target: ObsSourceTarget, password: @escaping @Sendable () -> String?) {
        reader = ObsSourceReader(target: target, password: password)
        reader.onImage = { [weak self] image in self?.onFrame?(image) }
        reader.onState = { [weak self] state in self?.onState?(Self.sessionState(state, target: target)) }
        reader.onLog = { RemotePlayLog.write($0) }
    }

    func start() { reader.start() }
    func stop() { reader.stop() }
    func reconnectNow() { reader.reconnectNow() }

    /// Okuma hattı yalnız bağlı/bağlı değil ayrımını kullanır; hatanın kullanıcıya söylenişi `.other` ile ana ekrana gider.
    static func sessionState(_ state: ObsSourceState, target: ObsSourceTarget) -> SessionState {
        switch state {
        case .connecting: .connecting
        case .streaming: .streaming
        case .stopped: .disconnected(.stopped)
        case .failed(let failure): .disconnected(.other(ObsSourceReader.describe(failure, target: target)))
        }
    }
}
