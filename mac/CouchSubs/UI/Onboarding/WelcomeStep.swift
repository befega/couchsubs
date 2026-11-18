// SPDX-License-Identifier: AGPL-3.0-only

import CouchSubsCore
import SwiftUI

/// Karşılama ve görüntü kaynağı seçimi: CouchSubs görüntüyü PS5'e bağlanarak mı (ikinci hesap), bu Mac'teki Remote
/// Play penceresinden mi, OBS'teki capture card kaynağından mı okur. Sihirbazın kalan adımları ve Ayarlar buna göre
/// değişir; seçim sonra Ayarlar › Görüntü kaynağı'ndan da değiştirilebilir.
struct WelcomeStep: View {
    @EnvironmentObject private var model: AppModel

    var body: some View {
        StepLayout(title: "Welcome to CouchSubs",
                   message: "CouchSubs reads the English subtitles in your game and shows a Turkish translation on your TV, on your computer or in your stream. First choose where CouchSubs gets the game picture from.") {
            VStack(alignment: .leading, spacing: Theme.space("m")) {
                ForEach(FrameSource.allCases, id: \.self) { source in
                    SourceChoice(source: source, selected: model.settings.frameSource == source) {
                        model.chooseFrameSource(source)
                    }
                }
            }
        }
    }
}

/// Seçilebilir kaynak karosu: ad, ne işe yaradığı ve neler gerektiği.
private struct SourceChoice: View {
    let source: FrameSource
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(alignment: .top, spacing: Theme.space("m")) {
                Image(systemName: selected ? "largecircle.fill.circle" : "circle")
                    .font(.csTitle)
                    .foregroundStyle(selected ? Color.csAccent : Color.csSecondary)
                VStack(alignment: .leading, spacing: Theme.space("s")) {
                    Text(title).font(.csBody.weight(.semibold)).foregroundStyle(Color.csText)
                    Text(detail).font(.csCallout).foregroundStyle(Color.csSecondary)
                        .fixedSize(horizontal: false, vertical: true)
                    ForEach(needs, id: \.self) { need in
                        Label(LocalizedStringKey(need), systemImage: "checkmark").font(.csCallout).foregroundStyle(Color.csSecondary)
                    }
                }
                Spacer(minLength: 0)
            }
            .padding(Theme.space("l"))
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(selected ? Color.csSurface2 : Color.clear,
                        in: RoundedRectangle(cornerRadius: Theme.radius("control"), style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Theme.radius("control"), style: .continuous)
                .strokeBorder(selected ? Color.csAccent : Color.csBorder))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(selected ? .isSelected : [])
    }

    private var title: LocalizedStringKey {
        switch source {
        case .ps5: "Connect to the PS5"
        case .remotePlayWindow: "Remote Play window on this Mac"
        case .obsSource: "Capture card in OBS (obs-websocket)"
        }
    }

    private var detail: LocalizedStringKey {
        switch source {
        case .ps5: "CouchSubs connects to the PS5 by itself with a second PSN account. You play on the TV as usual."
        case .remotePlayWindow: "You play in PS Remote Play or chiaki-ng on this Mac; CouchSubs reads only that window."
        case .obsSource: "The PS5 comes in through a capture card that OBS already shows. CouchSubs asks OBS for pictures of that source; the stream and the recording are not touched."
        }
    }

    /// Katalog anahtarları (gösterilirken çevrilir).
    private var needs: [String] {
        switch source {
        case .ps5: ["A PS5 on the same Wi-Fi network as this Mac", "A free second PlayStation Network account"]
        case .remotePlayWindow: ["PS Remote Play or chiaki-ng on this Mac", "Screen Recording permission for CouchSubs"]
        case .obsSource: ["OBS 28 or later with the capture card as a source", "OBS's WebSocket server turned on"]
        }
    }
}
