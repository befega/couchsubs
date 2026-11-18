// SPDX-License-Identifier: AGPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using CouchSubs.Core;
using CouchSubs.Shell;

namespace CouchSubs.App.Views.Steps;

/// <summary>
/// OBS'e bağlanıp görüntülü kaynakları listeler (capture card'lar başta); seçilen kaynak adresiyle hemen kaydedilir.
/// Parola şifreli depoda tutulur, geri gösterilmez (Ayarlar › Görüntü kaynağı ve sihirbaz).
/// </summary>
public partial class ObsSourceSetup : UserControl
{
    private bool filling;

    public ObsSourceSetup()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private AppController? Controller => DataContext as AppController;

    /// <summary>Kayıtlı adres ve kaynak gösterilir; OBS'e bağlanmadan (liste "Bağlan" ile gelir).</summary>
    public void Refresh()
    {
        if (Controller is not { } controller) return;
        var saved = controller.Settings.ObsSource;
        if (AddressBox.Text.Length == 0) AddressBox.Text = saved?.Address ?? ObsEndpoint.DefaultAddress;
        ShowPassword();
        if (SourceChoice.Items.Count == 0 && saved is { SourceName.Length: > 0 }) Fill([], saved);
    }

    private void ShowPassword()
    {
        var saved = Controller?.HasObsPassword == true;
        PasswordBox.Visibility = saved ? Visibility.Collapsed : Visibility.Visible;
        PasswordSaved.Visibility = PasswordRemove.Visibility = saved ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPasswordRemove(object sender, RoutedEventArgs e)
    {
        Controller?.RemoveObsPassword();
        ShowPassword();
    }

    /// <summary>
    /// Yazılan parola kaydedilir, OBS'e bağlanılır ve görüntülü kaynaklar listelenir. Kayıtlı kaynak OBS'te varsa o kalır;
    /// yoksa ilk kaynak (capture card'lar önce gelir) seçilir.
    /// </summary>
    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        if (Controller is not { } controller) return;
        var address = AddressBox.Text.Trim();
        if (PasswordBox.Visibility == Visibility.Visible && PasswordBox.Password.Length > 0)
        {
            controller.SetObsPassword(PasswordBox.Password);
            PasswordBox.Clear();
            ShowPassword();
        }
        ConnectButton.IsEnabled = false;
        ShowStatus(Localizer.Current["Connecting to OBS…"], error: false);
        try
        {
            var inputs = await controller.ObsInputsAsync(address);
            var saved = controller.Settings.ObsSource;
            var keep = inputs.FirstOrDefault(input => input.Name == saved?.SourceName) ?? inputs.FirstOrDefault();
            var target = keep is null ? saved : new ObsSourceTarget(address, keep.Name);
            Fill(inputs, target);
            ShowStatus(inputs.Count == 0 ? Localizer.Current["OBS has no sources with a picture. Add the capture card as a Video Capture Device first."]
                                         : Localizer.Current["Connected. Choose the capture card source."], error: inputs.Count == 0);
            if (keep is not null && target != saved) controller.Settings = controller.Settings with { ObsSource = target };
        }
        catch (ObsException error)
        {
            ShowStatus(ObsSourceStream.Describe(error.Failure, new ObsSourceTarget(address, ""), Localizer.Current), error: true);
        }
        catch (Exception error)
        {
            ShowStatus(error.GetBaseException().Message, error: true);
        }
        finally { ConnectButton.IsEnabled = true; }
    }

    private void Fill(IReadOnlyList<ObsInput> inputs, ObsSourceTarget? saved)
    {
        filling = true;
        try
        {
            SourceChoice.Items.Clear();
            ComboBoxItem? selected = null;
            foreach (var input in inputs)
            {
                var option = new ComboBoxItem { Content = input.IsVideoCapture ? $"{input.Name} — {Localizer.Current["capture device"]}" : input.Name, Tag = input.Name };
                SourceChoice.Items.Add(option);
                if (input.Name == saved?.SourceName) selected = option;
            }
            if (saved is { SourceName.Length: > 0 } && selected is null)
            {
                var suffix = inputs.Count == 0 ? "" : $" — {Localizer.Current["Not found in OBS"]}";
                selected = new ComboBoxItem { Content = saved.SourceName + suffix, Tag = saved.SourceName };
                SourceChoice.Items.Insert(0, selected);
            }
            SourceChoice.SelectedItem = selected;
        }
        finally { filling = false; }
    }

    private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (filling || Controller is not { } controller) return;
        if (SourceChoice.SelectedItem is ComboBoxItem { Tag: string name } && name.Length > 0)
        {
            var address = ObsEndpoint.Parse(AddressBox.Text) is not null ? AddressBox.Text.Trim() : ObsEndpoint.DefaultAddress;
            controller.Settings = controller.Settings with { ObsSource = new ObsSourceTarget(address, name) };
        }
    }

    private void ShowStatus(string message, bool error)
    {
        Status.Text = message;
        Status.Style = (Style)FindResource(error ? "ErrorText" : "HintText");
        Status.Visibility = Visibility.Visible;
    }
}
