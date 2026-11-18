// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CouchSubs.Core;
using CouchSubs.Shell;

namespace CouchSubs.App.Views.Steps;

/// <summary>
/// Seçicinin ayarı: açık/kapalı ve yarışan motorlar. Kurulu olmayan motor (anahtarı yok, ücretsiz model inmemiş) soluk
/// görünür; altta şu an hangi motorların yarışacağı yazar.
/// </summary>
public partial class ConductorSetup : UserControl
{
    /// <summary>Listede görünen sıra (seçicideki öncelikle aynı).</summary>
    private static readonly TranslationEngine[] Order =
        [TranslationEngine.DeepL, TranslationEngine.Claude, TranslationEngine.OpenAi, TranslationEngine.Gemini, TranslationEngine.Local, TranslationEngine.Free];

    private AppController? controller;
    private bool filling;

    public ConductorSetup()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            controller = DataContext as AppController;
            if (controller is null) return;
            controller.PropertyChanged += OnControllerChanged;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            if (controller is not null) controller.PropertyChanged -= OnControllerChanged;
        };
    }

    private void OnControllerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppController.Settings)) Dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        if (controller is null) return;
        var settings = controller.Settings;
        var text = Localizer.Current;
        filling = true;
        try
        {
            Enabled.IsChecked = settings.Conductor;
            EngineList.Children.Clear();
            foreach (var engine in Order)
            {
                var usable = controller.ConductorCanUse(engine);
                var name = engine == TranslationEngine.Free ? text["Free (on this computer)"]
                    : engine == TranslationEngine.Local ? $"{text["Local model (Ollama, LM Studio)"]} · {controller.ConductorName(engine)}"
                    : engine.DisplayName();
                var box = new CheckBox
                {
                    Content = usable ? name : $"{name} — {text["not set up"]}",
                    IsChecked = settings.ConductorEngines.Contains(engine),
                    IsEnabled = usable && settings.Conductor,
                    Tag = engine,
                    Margin = new Thickness(0, 0, 0, 6),
                };
                box.SetResourceReference(ForegroundProperty, "textPrimaryBrush");
                box.Checked += OnEngine;
                box.Unchecked += OnEngine;
                EngineList.Children.Add(box);
            }
            var ready = controller.ConductorReadyEngines;
            Status.Text = !settings.Conductor ? ""
                : settings.SourceLanguage != "en" || settings.TargetLanguage != "tr" ? text["The conductor works only for English to Turkish."]
                : ready.Count < 2 ? text["Choose at least two engines that are set up."]
                : text.Format("Competing: %@", string.Join(" + ", ready.Select(controller.ConductorName)));
            Status.Visibility = Status.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { filling = false; }
    }

    private void OnEnabled(object sender, RoutedEventArgs e)
    {
        if (filling || controller is null) return;
        controller.Settings = controller.Settings with { Conductor = Enabled.IsChecked == true };
        Refresh();
    }

    private void OnEngine(object sender, RoutedEventArgs e)
    {
        if (filling || controller is null) return;
        var chosen = EngineList.Children.OfType<CheckBox>().Where(box => box.IsChecked == true).Select(box => (TranslationEngine)box.Tag).ToArray();
        controller.Settings = controller.Settings with { ConductorEngines = chosen };
        Refresh();
    }
}
