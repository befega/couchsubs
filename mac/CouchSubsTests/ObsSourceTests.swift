// SPDX-License-Identifier: AGPL-3.0-only

import CouchSubsCore
import RemotePlay
import XCTest
@testable import CouchSubs

/// Görüntünün OBS kaynağından (capture card) okunması: PS5 eşleşmesi gerekmez, parola Anahtarlık'tan okunur, kaynak
/// değişince çeviri yeni kaynakla sürer.
@MainActor
final class ObsSourceTests: XCTestCase {
    private let target = ObsSourceTarget(address: "localhost:4455", sourceName: "Capture Card")

    private func model(_ services: FakeServices = FakeServices(), store: MemoryStore = MemoryStore(),
                       defaults: UserDefaults = .isolated(), target: ObsSourceTarget? = nil) -> AppModel {
        var settings = UserSettings()
        settings.frameSource = .obsSource
        settings.output = .computer
        settings.obsSource = target
        defaults.set(try? JSONEncoder().encode(settings), forKey: StoreKey.settings)
        return AppModel(services: services, store: store, defaults: defaults)
    }

    /// Karşılamada OBS seçilince PSN girişi ve PS5 adımları gösterilmez: OBS adımında kaynak seçilir (OBS kapalıysa adım
    /// geçilir), sonra TV adımı gelir. Yeniden açılınca sihirbaz çıkmaz.
    func testWizardWithObsSourceAsksForObsInsteadOfPSN() {
        let store = MemoryStore()
        let defaults = UserDefaults.isolated()
        let model = AppModel(services: FakeServices(), store: store, defaults: defaults)
        model.chooseFrameSource(.obsSource)
        XCTAssertFalse(model.setupSteps.contains(.psnLogin))
        XCTAssertFalse(model.setupSteps.contains(.secondAccount))
        model.advance()
        XCTAssertEqual(model.step, .obs)
        XCTAssertFalse(model.canAdvance)
        model.advance()
        XCTAssertEqual(model.step, .obs) // kaynak seçilmeden "Devam" geçmez
        model.skipStep()
        XCTAssertEqual(model.step, .tv) // "Şimdilik geç"
        model.back()
        model.settings.obsSource = target
        XCTAssertTrue(model.canAdvance)
        model.advance()
        model.useComputerScreen()
        XCTAssertNil(model.accountId)
        XCTAssertFalse(model.needsPS5Link)
        XCTAssertTrue(model.canStart)
        model.advance()
        model.advance()
        XCTAssertEqual(model.step, .done)
        XCTAssertEqual(AppModel(services: FakeServices(), store: store, defaults: defaults).step, .done)
    }

    /// PS5 giriş şifresi bölümü yalnız PS5'e bağlanırken gerekir.
    func testSettingsHidePS5OnlyPages() {
        XCTAssertTrue(FrameSource.ps5.needsPS5)
        XCTAssertFalse(FrameSource.obsSource.needsPS5)
        XCTAssertFalse(FrameSource.remotePlayWindow.needsPS5)
    }

    func testObsSourceStartsWithoutPS5AndReadsTheSavedPassword() async {
        let services = FakeServices()
        let store = MemoryStore()
        let model = model(services, store: store)
        XCTAssertEqual(model.step, .done) // PSN girişi ve PS5 eşleşmesi gerekmez
        XCTAssertFalse(model.needsPS5Link)
        XCTAssertFalse(model.canStart) // kaynak seçilmeden başlamaz
        XCTAssertEqual(model.ps5StatusText, String(localized: "Choose an OBS source in Settings › Picture source."))

        model.settings.obsSource = target
        XCTAssertTrue(model.canStart)
        XCTAssertEqual(model.sourceTitle, "OBS")
        model.setObsPassword(" secret ")
        XCTAssertTrue(model.hasObsPassword)

        await model.start()
        XCTAssertTrue(model.isRunning)
        XCTAssertEqual(services.obsStreams.count, 1)
        XCTAssertEqual(services.obsStreams[0].target, target)
        XCTAssertEqual(services.obsStreams[0].password(), "secret")
        XCTAssertTrue(services.streamPasscodes.isEmpty) // PS5'e bağlanılmadı
        XCTAssertTrue(services.streams[0].started)

        // Parola değişince yeni oturum açılmaz, bağlantı yenilenir; parola her bağlantıda Anahtarlık'tan okunur.
        model.setObsPassword("changed")
        XCTAssertEqual(services.streams[0].reconnects, 1)
        XCTAssertEqual(services.obsStreams[0].password(), "changed")
        model.removeObsPassword()
        XCTAssertFalse(model.hasObsPassword)
        XCTAssertNil(services.obsStreams[0].password())
        model.stop()
    }

    func testChoosingAnotherSourceRestartsTheTranslation() async throws {
        let services = FakeServices()
        let model = model(services, target: target)
        await model.start()
        let other = ObsSourceTarget(address: target.address, sourceName: "Second Card")
        model.settings.obsSource = other
        for _ in 0..<200 where services.obsStreams.count < 2 { try await Task.sleep(for: .milliseconds(10)) }
        XCTAssertEqual(services.obsStreams.map { $0.target }, [target, other])
        XCTAssertFalse(services.streams[0].started)
        XCTAssertTrue(model.isRunning)
        model.stop()
    }

    func testIncompleteSourceCannotStart() async {
        let services = FakeServices()
        let model = model(services, target: ObsSourceTarget(address: "not a host:x", sourceName: "Card"))
        XCTAssertFalse(model.canStart)
        await model.start()
        XCTAssertFalse(model.isRunning)
        XCTAssertTrue(services.obsStreams.isEmpty)
    }

    func testListingSourcesUsesTheSavedPasswordAndRejectsBadAddresses() async throws {
        let services = FakeServices()
        services.obsInputsResult = .success([ObsInput(name: "Capture Card", kind: "macos-avcapture")])
        let model = model(services)
        model.setObsPassword("secret")
        let inputs = try await model.obsInputs(address: "192.168.1.20")
        XCTAssertEqual(inputs.map(\.name), ["Capture Card"])
        XCTAssertEqual(services.obsInputRequests.first?.endpoint, ObsEndpoint(host: "192.168.1.20", port: 4455))
        XCTAssertEqual(services.obsInputRequests.first?.password, "secret")

        do {
            _ = try await model.obsInputs(address: "wss://example")
            XCTFail("Geçersiz adres kabul edilmemeli")
        } catch {
            XCTAssertEqual((error as? ObsError)?.failure, .invalidAddress)
        }
    }

    func testStatusTextFollowsTheStream() {
        XCTAssertEqual(AppModel.obsStatusText(.disconnected(.stopped), running: false, target: target),
                       String(format: String(localized: "Ready to read %@ from OBS when translation starts."), "Capture Card"))
        XCTAssertEqual(AppModel.obsStatusText(.streaming, running: true, target: target),
                       String(format: String(localized: "Reading %@ from OBS"), "Capture Card"))
        XCTAssertEqual(AppModel.obsStatusText(.connecting, running: true, target: target), String(localized: "Connecting to OBS…"))
        XCTAssertEqual(AppModel.obsStatusText(.disconnected(.other("detail")), running: true, target: target), "detail")
    }

    /// Okuyucunun hatası ana ekrana ne yapılacağını söyleyen metinle gider.
    func testReaderFailuresBecomeReadableStates() {
        XCTAssertEqual(ObsSourceStream.sessionState(.streaming, target: target), .streaming)
        XCTAssertEqual(ObsSourceStream.sessionState(.stopped, target: target), .disconnected(.stopped))
        XCTAssertEqual(ObsSourceStream.sessionState(.failed(.passwordRejected), target: target),
                       .disconnected(.other(ObsSourceReader.describe(.passwordRejected, target: target))))
    }
}
