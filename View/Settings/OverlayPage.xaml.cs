using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Settings;

public partial class OverlayPage : UserControl
{
    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly Speakers _speakers;
    private readonly Action _notifyChanged;
    private readonly DispatcherTimer _previewTimer;
    private bool _isLoading = true;

    public event EventHandler? ResetPositionsRequested;
    public event EventHandler? MoveModeRequested;

    public OverlayPage(Speakers speakers, Action notifyChanged)
    {
        InitializeComponent();
        _speakers = speakers;
        _notifyChanged = notifyChanged;

        BuildScaleSwatches();
        LoadFromSettings();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _previewTimer.Tick += (_, _) => UpdatePreview();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _previewTimer.Start();
            else _previewTimer.Stop();
        };
    }

    // --- Load ---

    public void LoadFromSettings()
    {
        _isLoading = true;
        var s = _settingsManager.Settings;

        EnabledToggle.IsChecked = s.Display.Enabled;
        TransparentToggle.IsChecked = s.Bars.TransparentMode;
        BalancedToggle.IsChecked = s.Bars.IgnoreBalancedSounds;
        PairToggle.IsChecked = s.Bars.PairWithSideBars;
        LinkToggle.IsChecked = s.Bars.LinkIndicators;

        SensitivitySlider.Value = s.Bars.Sensitivity;
        ThresholdSlider.Value = s.Bars.MinThreshold;
        FocusSlider.Value = Math.Round(s.Bars.DirectionalFocus * 100);
        StrengthSlider.Value = s.Bars.MaxOpacity;
        SizeSlider.Value = Math.Round(s.Bars.OverlaySize * 100);
        WidthSlider.Value = s.Bars.Width;
        SpreadSlider.Value = Math.Round((s.Bars.RightIndicatorPercent - s.Bars.LeftIndicatorPercent) * 100);
        LeftSlider.Value = Math.Round(s.Bars.LeftIndicatorPercent * 100);
        RightSlider.Value = Math.Round(s.Bars.RightIndicatorPercent * 100);

        CheckStyleRadio(s.Bars.OverlayStyle);
        CheckScaleSwatch(s.Bars.ColorScale);
        SelectByTag(AnchorSegmented, s.Bars.Anchor.ToString());
        SelectByTag(RingsSegmented, s.Bars.RingCount.ToString());
        SelectByTag(MappingSegmented, s.Bars.RingMapping.ToString());
        SelectByTag(FpsSegmented, s.Display.OverlayFps.ToString());

        UpdateAllChipsAndLabels();
        UpdateContextualRows();
        _isLoading = false;
    }

    public void RefreshPositionSliders()
    {
        _isLoading = true;
        var bars = _settingsManager.Settings.Bars;
        SpreadSlider.Value = Math.Round((bars.RightIndicatorPercent - bars.LeftIndicatorPercent) * 100);
        LeftSlider.Value = Math.Round(bars.LeftIndicatorPercent * 100);
        RightSlider.Value = Math.Round(bars.RightIndicatorPercent * 100);
        UpdateAllChipsAndLabels();
        _isLoading = false;
    }

    // --- Live preview (left/right activity, processed like the overlays) ---

    private void UpdatePreview()
    {
        var bars = _settingsManager.Settings.Bars;
        var left = Process(Math.Max(_speakers.Speaker1.Value, Math.Max(_speakers.Speaker5.Value, _speakers.Speaker7.Value)), bars);
        var right = Process(Math.Max(_speakers.Speaker2.Value, Math.Max(_speakers.Speaker6.Value, _speakers.Speaker8.Value)), bars);

        // Mirror the overlay's directional focus so tuning the slider is visible here
        var focus = Math.Clamp(bars.DirectionalFocus, 0.0, 1.0);
        var focusedLeft = Math.Max(0, left - focus * right);
        var focusedRight = Math.Max(0, right - focus * left);

        SetPreviewMeter(PreviewLeftMeter, focusedLeft, bars.ColorScale);
        SetPreviewMeter(PreviewRightMeter, focusedRight, bars.ColorScale);
    }

    private static double Process(double raw, BarSettings bars)
    {
        if (raw < bars.MinThreshold) return 0;
        return Math.Min(1.0, raw * bars.Sensitivity);
    }

    private static void SetPreviewMeter(Border meter, double level, ColorScale scale)
    {
        const double maxHeight = 64;
        meter.Height = Math.Max(0, level * maxHeight);
        meter.Background = level < ScaleEngine.InvisibleBelow
            ? Brushes.Transparent
            : new SolidColorBrush(ScaleEngine.At(scale, level));
    }

    // --- Color scale swatches ---

    private void BuildScaleSwatches()
    {
        foreach (ColorScale scale in Enum.GetValues<ColorScale>())
        {
            var stops = ScaleEngine.StopsFor(scale);
            var dot = new Ellipse
            {
                Width = 11, Height = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(stops[0], 0),
                        new GradientStop(stops[1], 0.5),
                        new GradientStop(stops[2], 1)
                    }, 0)
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(dot);
            content.Children.Add(new TextBlock
            {
                Text = scale == ColorScale.Thermal ? "Thermal (default)" : scale.ToString(),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var radio = new RadioButton
            {
                Content = content,
                Tag = scale,
                GroupName = "ColorScale",
                Style = (Style)FindResource("RadioPill"),
                Margin = new Thickness(0, 0, 8, 8)
            };
            AutomationProperties.SetName(radio, $"Color scale {scale}");
            radio.Checked += ScaleRadio_Checked;
            ScalePanel.Children.Add(radio);
        }
    }

    private void CheckScaleSwatch(ColorScale scale)
    {
        foreach (RadioButton radio in ScalePanel.Children)
            radio.IsChecked = (ColorScale)radio.Tag == scale;
    }

    private void ScaleRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var scale = (ColorScale)((RadioButton)sender).Tag;
        Update(s => s.Bars.ColorScale = scale);
    }

    // --- Overlay style ---

    private void CheckStyleRadio(OverlayStyle style)
    {
        StyleSideBars.IsChecked = style == OverlayStyle.SideBars;
        StyleRadarRing.IsChecked = style == OverlayStyle.RadarRing;
        StyleRingPing.IsChecked = style == OverlayStyle.RingPing;
        StyleCompass.IsChecked = style == OverlayStyle.CompassStrip;
        StyleEdgeGlow.IsChecked = style == OverlayStyle.EdgeGlow;
    }

    private void StyleRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var style = Enum.Parse<OverlayStyle>((string)((RadioButton)sender).Tag);
        Update(s => s.Bars.OverlayStyle = style);
        UpdateContextualRows();
    }

    private void PairToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.PairWithSideBars = PairToggle.IsChecked == true);
        UpdateContextualRows();
    }

    // --- Toggles ---

    private void EnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Update(s => s.Display.Enabled = EnabledToggle.IsChecked == true);
        UpdateAllChipsAndLabels();
    }

    private void TransparentToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.TransparentMode = TransparentToggle.IsChecked == true);
        UpdateAllChipsAndLabels();
    }

    private void BalancedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.IgnoreBalancedSounds = BalancedToggle.IsChecked == true);
        UpdateAllChipsAndLabels();
    }

    private void LinkToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.LinkIndicators = LinkToggle.IsChecked == true);
        UpdateContextualRows();
        UpdateAllChipsAndLabels();
    }

    // --- Sliders ---

    private void SensitivitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.Sensitivity = Math.Round(SensitivitySlider.Value, 1));
        SensitivityChip.Content = SensitivitySlider.Value.ToString("F1");
    }

    private void ThresholdSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.MinThreshold = Math.Round(ThresholdSlider.Value, 2));
        ThresholdChip.Content = ThresholdSlider.Value.ToString("F2");
    }

    private void FocusSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.DirectionalFocus = FocusSlider.Value / 100.0);
        FocusChip.Content = $"{FocusSlider.Value:F0} %";
    }

    private void StrengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.MaxOpacity = Math.Round(StrengthSlider.Value, 2));
        StrengthChip.Content = $"{StrengthSlider.Value * 100:F0} %";
    }

    private void SizeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.OverlaySize = SizeSlider.Value / 100.0);
        SizeChip.Content = $"{SizeSlider.Value:F0} %";
    }

    private void WidthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.Width = (int)WidthSlider.Value);
        WidthChip.Content = $"{WidthSlider.Value:F0} px";
    }

    private void SpreadSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        var half = SpreadSlider.Value / 200.0;
        Update(s =>
        {
            s.Bars.LeftIndicatorPercent = 0.5 - half;
            s.Bars.RightIndicatorPercent = 0.5 + half;
        });
        SpreadChip.Content = $"{SpreadSlider.Value:F0} %";
    }

    private void LeftSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.LeftIndicatorPercent = LeftSlider.Value / 100.0);
        LeftChip.Content = $"{LeftSlider.Value:F0} %";
    }

    private void RightSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        Update(s => s.Bars.RightIndicatorPercent = RightSlider.Value / 100.0);
        RightChip.Content = $"{RightSlider.Value:F0} %";
    }

    // --- Segmented controls ---

    private void AnchorSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || AnchorSegmented.SelectedItem == null) return;
        var anchor = Enum.Parse<OverlayAnchor>((string)((ListBoxItem)AnchorSegmented.SelectedItem).Tag);
        Update(s => s.Bars.Anchor = anchor);
    }

    private void RingsSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || RingsSegmented.SelectedItem == null) return;
        var count = int.Parse((string)((ListBoxItem)RingsSegmented.SelectedItem).Tag);
        Update(s => s.Bars.RingCount = count);
    }

    private void MappingSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || MappingSegmented.SelectedItem == null) return;
        var mapping = Enum.Parse<RingMapping>((string)((ListBoxItem)MappingSegmented.SelectedItem).Tag);
        Update(s => s.Bars.RingMapping = mapping);
    }

    private void FpsSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || FpsSegmented.SelectedItem == null) return;
        var fps = int.Parse((string)((ListBoxItem)FpsSegmented.SelectedItem).Tag);
        Update(s => s.Display.OverlayFps = fps);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ResetPositionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditPositions_Click(object sender, RoutedEventArgs e)
    {
        MoveModeRequested?.Invoke(this, EventArgs.Empty);
    }

    // --- Contextual visibility (plan 6a/6c: only matching rows render) ---

    private void UpdateContextualRows()
    {
        var style = _settingsManager.Settings.Bars.OverlayStyle;
        var linked = _settingsManager.Settings.Bars.LinkIndicators;

        PairRow.Visibility = style != OverlayStyle.SideBars ? Visibility.Visible : Visibility.Collapsed;
        var showBars = style == OverlayStyle.SideBars ||
                       (_settingsManager.Settings.Bars.PairWithSideBars && style != OverlayStyle.EdgeGlow);
        SideBarsRows.Visibility = showBars ? Visibility.Visible : Visibility.Collapsed;
        SpreadRow.Visibility = linked ? Visibility.Visible : Visibility.Collapsed;
        UnlinkedRows.Visibility = linked ? Visibility.Collapsed : Visibility.Visible;

        AnchorRow.Visibility = style is OverlayStyle.RadarRing or OverlayStyle.RingPing or OverlayStyle.CompassStrip
            ? Visibility.Visible : Visibility.Collapsed;
        RingPingRows.Visibility = style == OverlayStyle.RingPing ? Visibility.Visible : Visibility.Collapsed;
        EdgeGlowNote.Visibility = style == OverlayStyle.EdgeGlow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAllChipsAndLabels()
    {
        SensitivityChip.Content = SensitivitySlider.Value.ToString("F1");
        ThresholdChip.Content = ThresholdSlider.Value.ToString("F2");
        FocusChip.Content = $"{FocusSlider.Value:F0} %";
        StrengthChip.Content = $"{StrengthSlider.Value * 100:F0} %";
        SizeChip.Content = $"{SizeSlider.Value:F0} %";
        WidthChip.Content = $"{WidthSlider.Value:F0} px";
        SpreadChip.Content = $"{SpreadSlider.Value:F0} %";
        LeftChip.Content = $"{LeftSlider.Value:F0} %";
        RightChip.Content = $"{RightSlider.Value:F0} %";

        SetToggleLabel(EnabledStateLabel, EnabledToggle.IsChecked == true);
        SetToggleLabel(TransparentStateLabel, TransparentToggle.IsChecked == true);
        SetToggleLabel(BalancedStateLabel, BalancedToggle.IsChecked == true);
        SetToggleLabel(LinkStateLabel, LinkToggle.IsChecked == true);
    }

    private void SetToggleLabel(TextBlock label, bool on)
    {
        label.Text = on ? "On" : "Off";
        label.Style = (Style)FindResource(on ? "ToggleStateOnText" : "ToggleStateOffText");
    }

    private void Update(Action<AppSettings> change)
    {
        _settingsManager.Update(change);
        _notifyChanged();
    }

    private static void SelectByTag(ListBox listBox, string tag)
    {
        foreach (ListBoxItem item in listBox.Items)
        {
            if ((string)item.Tag == tag)
            {
                listBox.SelectedItem = item;
                return;
            }
        }
    }
}
