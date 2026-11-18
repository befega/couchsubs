// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows.Controls;

namespace CouchSubs.App.Views.Steps;

/// <summary>Görüntü OBS'teki capture card kaynağından okunurken: OBS'e bağlanılır ve kaynak seçilir. OBS henüz açık değilse adım geçilebilir; kaynak sonra Ayarlar'da seçilir.</summary>
public partial class ObsStep : UserControl
{
    public ObsStep() => InitializeComponent();
}
