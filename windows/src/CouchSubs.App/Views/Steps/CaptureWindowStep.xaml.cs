// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows.Controls;

namespace CouchSubs.App.Views.Steps;

/// <summary>Görüntü seçilen pencereden okunurken: hangi pencere. Pencere henüz açık değilse adım geçilebilir; sonra Ayarlar'da seçilir.</summary>
public partial class CaptureWindowStep : UserControl
{
    public CaptureWindowStep() => InitializeComponent();
}
