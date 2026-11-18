// SPDX-License-Identifier: AGPL-3.0-only

import SwiftUI

/// Görüntü bu Mac'teki Remote Play penceresinden okunurken: hangi uygulamanın penceresi okunacak, kurulu mu.
struct RemotePlayStep: View {
    var body: some View {
        StepLayout(title: "Remote Play on this Mac",
                   message: "Play in PS Remote Play or chiaki-ng on this Mac; CouchSubs reads only that window, so no second account is needed. Show the subtitles on the computer screen, and allow Screen Recording for CouchSubs when macOS asks.") {
            Card { RemotePlayAppChoice() }
        }
    }
}
