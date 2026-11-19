// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CouchSubs.Core;
using CouchSubs.Shell;

namespace CouchSubs.App.Views;

/// <summary>
/// Ayarlar › OBS katmanı: yayındaki altyazının görünümü (<see cref="UserSettings.ObsStyle"/>). "TV ile aynı" açıkken ayar
/// boştur ve OBS TV'yi izler; kapatınca TV'nin o anki görünümü kopyalanır, sonra ayrı değişir.
/// </summary>
public partial class ObsAppearanceCard : UserControl
{
    private AppController? controller;
    private bool loading = true;

    public ObsAppearanceCard() => InitializeComponent();

    internal void Attach(AppController controller)
    {
        this.controller = controller;
        Show(controller.Settings);
        controller.PropertyChanged += OnChanged;
        Unloaded += (_, _) => controller.PropertyChanged -= OnChanged;
    }

    /// <summary>TV görünümü değişince "TV ile aynı" seçiliyse kaydırıcılar onu gösterir.</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppController.Settings) && controller is { } c) Dispatcher.Invoke(() => Show(c.Settings));
    }

    private void Show(UserSettings settings)
    {
        loading = true;
        var style = settings.StreamStyle;
        SameAsTv.IsChecked = settings.ObsStyle is null;
        Controls.IsEnabled = settings.ObsStyle is not null;
        Controls.Opacity = settings.ObsStyle is null ? 0.5 : 1;
        TextSize.Value = Math.Clamp(style.FontSize, TextSize.Minimum, TextSize.Maximum);
        TextSizeValue.Text = ((int)TextSize.Value).ToString();
        Select(Placement, style.Placement.ToString());
        FixedPanel.Visibility = style.Placement == SubtitlePlacement.Fixed ? Visibility.Visible : Visibility.Collapsed;
        Position.Value = Math.Clamp(style.BottomPercent, Position.Minimum, Position.Maximum);
        BackgroundOpacity.Value = Math.Clamp(style.Background, 0, 100);
        Select(BackgroundShape, style.BackgroundShape.ToString());
        Bold.IsChecked = style.Bold;
        Select(TextColor, style.TextColor.ToString());
        loading = false;
    }

    private static void Select(ComboBox box, string tag) =>
        box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == tag) ?? box.Items[0];

    /// <summary>Yayın görünümünü değiştirir; ayrı görünüm henüz yoksa TV'ninkinden başlar.</summary>
    private void Change(Func<SubtitleStyle, SubtitleStyle> change)
    {
        if (loading || controller is null) return;
        controller.Settings = controller.Settings with { ObsStyle = change(controller.Settings.StreamStyle) };
    }

    private void OnSameAsTv(object sender, RoutedEventArgs e)
    {
        if (loading || controller is null) return;
        var same = SameAsTv.IsChecked == true;
        controller.Settings = controller.Settings with { ObsStyle = same ? null : controller.Settings.Style };
    }

    private void OnTextSize(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (loading) return;
        var size = (int)Math.Round(e.NewValue);
        TextSizeValue.Text = size.ToString();
        Change(s => s with { FontSize = size });
    }

    private void OnPlacement(object sender, SelectionChangedEventArgs e)
    {
        if (Placement.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        var placement = Enum.Parse<SubtitlePlacement>(tag);
        FixedPanel.Visibility = placement == SubtitlePlacement.Fixed ? Visibility.Visible : Visibility.Collapsed;
        Change(s => s with { Placement = placement });
    }

    private void OnPosition(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        Change(s => s with { BottomPercent = (int)Math.Round(e.NewValue) });

    private void OnBackground(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        Change(s => s with { Background = (int)Math.Round(e.NewValue) });

    private void OnBackgroundShape(object sender, SelectionChangedEventArgs e)
    {
        if (BackgroundShape.SelectedItem is ComboBoxItem { Tag: string tag })
            Change(s => s with { BackgroundShape = Enum.Parse<SubtitleBackgroundShape>(tag) });
    }

    private void OnBold(object sender, RoutedEventArgs e) => Change(s => s with { Bold = Bold.IsChecked == true });

    private void OnTextColor(object sender, SelectionChangedEventArgs e)
    {
        if (TextColor.SelectedItem is ComboBoxItem { Tag: string tag })
            Change(s => s with { TextColor = Enum.Parse<SubtitleTextColor>(tag) });
    }

    /// <summary>Kaydırıcıyı tutunca örnek altyazı gider; ayar OBS'te doğrudan görülür.</summary>
    private void OnPreview(object sender, System.Windows.Input.MouseButtonEventArgs e) => controller?.SendTestSubtitle();

    private void OnTest(object sender, RoutedEventArgs e) => controller?.SendTestSubtitle();
}
