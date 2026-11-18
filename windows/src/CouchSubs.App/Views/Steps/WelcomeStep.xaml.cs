// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows.Controls;
using System.Windows;
using CouchSubs.Core;
using CouchSubs.Shell;

namespace CouchSubs.App.Views.Steps;

public partial class WelcomeStep : UserControl
{
    public WelcomeStep()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Ps5.IsChecked = !Controller.IsXbox;
            Xbox.IsChecked = Controller.IsXbox;
            Ps5.IsEnabled = Xbox.IsEnabled = !Controller.ConsoleKindLocked;
            ShowSource();
            ShowRequirements();
        };
    }

    private AppController Controller => (AppController)DataContext;

    private void OnSelected(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        Controller.SelectConsoleKind(Xbox.IsChecked == true ? GameConsoleKind.Xbox : GameConsoleKind.PlayStation);
        ShowSource();
        ShowRequirements();
    }

    /// <summary>Görüntü kaynağı seçimi yalnız PS5'te var (Xbox kendi bağlantısını kullanır).</summary>
    private void ShowSource()
    {
        SourceCard.Visibility = Controller.IsXbox ? Visibility.Collapsed : Visibility.Visible;
        var source = Controller.Settings.FrameSource.ToString();
        foreach (var choice in new[] { SourcePs5, SourceRemotePlay, SourceCaptureWindow, SourceObs })
            choice.IsChecked = (string)choice.Tag == source;
    }

    private void OnSource(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { Tag: string tag }) return;
        Controller.ChooseFrameSource(Enum.Parse<FrameSource>(tag));
        ShowRequirements();
    }

    private void ShowRequirements()
    {
        if (Controller.IsXbox)
        {
            Layout.Message = Localizer.Current["Xbox support is in beta. CouchSubs reads the subtitles in the game picture and shows the translation on your TV or computer. You keep playing with your own controller."];
            Requirements.Text = Localizer.Current["You will need: your home Xbox on the same network, a Microsoft account, and a TV or computer to show the subtitles. Remote play must be turned on on the Xbox."];
            return;
        }
        // PS5'te gerekenler seçilen görüntü kaynağına göre (ortak katalogdan, her dilde).
        var text = Localizer.Current;
        Layout.Message = text["CouchSubs reads the English subtitles in your game and shows a Turkish translation on your TV, on your computer or in your stream. First choose where CouchSubs gets the game picture from."];
        string[] needs = Controller.Settings.FrameSource switch
        {
            FrameSource.RemotePlayWindow => ["PS Remote Play or chiaki-ng on this PC"],
            FrameSource.CaptureWindow => ["A window that shows the capture card picture"],
            FrameSource.ObsSource => ["OBS 28 or later with the capture card as a source", "OBS's WebSocket server turned on"],
            _ => ["A PS5 on the same network as this computer", "A free second PlayStation Network account"],
        };
        Requirements.Text = text["You will need:"] + string.Concat(needs.Select(need => "\n• " + text[need]));
    }
}
