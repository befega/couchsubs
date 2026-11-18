// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using CouchSubs.Core;
using CouchSubs.Shell;

namespace CouchSubs.App.Views.Steps;

/// <summary>Okunacak pencerenin seçimi (Ayarlar › Görüntü kaynağı ve sihirbaz). Seçim hemen kaydedilir.</summary>
public partial class CaptureWindowSetup : UserControl
{
    private bool filling;

    public CaptureWindowSetup()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private AppController? Controller => DataContext as AppController;

    /// <summary>Açık pencereler yeniden listelenir; kayıtlı pencere kapalıysa "şu an açık değil" diye başta kalır.</summary>
    public void Refresh()
    {
        if (filling || Controller is not { } controller) return;
        filling = true;
        try
        {
            var saved = controller.Settings.CaptureWindow;
            var windows = controller.AvailableCaptureWindows();
            WindowChoice.Items.Clear();
            ComboBoxItem? selected = null;
            foreach (var target in windows)
            {
                var option = new ComboBoxItem { Content = $"{target.Title} ({target.ProcessName})", Tag = target };
                WindowChoice.Items.Add(option);
                if (target == saved) selected = option;
            }
            if (saved is not null && selected is null)
            {
                selected = new ComboBoxItem { Content = $"{saved.Title} ({saved.ProcessName}) — {Localizer.Current["Not currently open"]}", Tag = saved };
                WindowChoice.Items.Insert(0, selected);
            }
            WindowChoice.SelectedItem = selected;
            EmptyText.Visibility = windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { filling = false; }
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnWindowChanged(object sender, SelectionChangedEventArgs e)
    {
        if (filling || Controller is not { } controller) return;
        if (WindowChoice.SelectedItem is ComboBoxItem { Tag: WindowCaptureTarget target })
            controller.Settings = controller.Settings with { CaptureWindow = target };
    }
}
