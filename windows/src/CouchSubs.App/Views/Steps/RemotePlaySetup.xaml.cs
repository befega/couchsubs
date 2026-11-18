// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CouchSubs.Shell;

namespace CouchSubs.App.Views.Steps;

/// <summary>Remote Play penceresinden okunurken hangi uygulama ve kurulu mu (Ayarlar › Görüntü kaynağı ve sihirbaz).</summary>
public partial class RemotePlaySetup : UserControl
{
    private bool filling;

    public RemotePlaySetup()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private AppController? Controller => DataContext as AppController;

    /// <summary>Kayıtlı seçim ve kurulu uygulamalar yeniden gösterilir (ör. kaynak değişince).</summary>
    public void Refresh()
    {
        if (Controller is not { } controller) return;
        filling = true;
        try
        {
            AppChoice.SelectedItem = AppChoice.Items.Cast<ComboBoxItem>().First(item => (string)item.Tag == controller.Settings.RemotePlayApp.ToString());
        }
        finally { filling = false; }
        var sony = controller.RemotePlayInstalled(RemotePlayApp.Sony);
        var chiaki = controller.RemotePlayInstalled(RemotePlayApp.Chiaki);
        SonyInstalledText.Visibility = sony ? Visibility.Visible : Visibility.Collapsed;
        SonyDownloadButton.Visibility = sony ? Visibility.Collapsed : Visibility.Visible;
        ChiakiInstalledText.Visibility = chiaki ? Visibility.Visible : Visibility.Collapsed;
        ChiakiDownloadButton.Visibility = chiaki ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnApp(object sender, SelectionChangedEventArgs e)
    {
        if (filling || Controller is not { } controller) return;
        if (AppChoice.SelectedItem is ComboBoxItem { Tag: string tag })
            controller.Settings = controller.Settings with { RemotePlayApp = Enum.Parse<RemotePlayApp>(tag) };
    }

    private void OnDownloadSony(object sender, RoutedEventArgs e) => Open(AppController.SonyRemotePlayDownload);
    private void OnDownloadChiaki(object sender, RoutedEventArgs e) => Open(AppController.ChiakiDownload);

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
}
