// SPDX-License-Identifier: AGPL-3.0-only

import SwiftUI

/// Görüntü OBS'teki capture card kaynağından okunurken: OBS'e bağlanılır ve kaynak seçilir. OBS henüz açık değilse
/// adım geçilebilir; kaynak sonra Ayarlar › Görüntü kaynağı'nda seçilir.
struct ObsStep: View {
    var body: some View {
        StepLayout(title: "Connect OBS",
                   message: "CouchSubs reads the game picture from the capture card source in OBS. OBS keeps the capture card; your stream and recording go on as before.") {
            Card { ObsSourcePanel() }
        }
    }
}
