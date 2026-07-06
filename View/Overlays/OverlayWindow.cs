using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Helpers;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// The single overlay host: a borderless, transparent, topmost, click-through
/// window spanning the target monitor's work area. Runs the level tick at the
/// configured frame rate (Display.OverlayFps, 30-240) and renders the active
/// style (optionally paired with side bars). Move mode
/// (Ctrl+Shift+E) disables click-through for dragging/keyboard positioning.
/// Cannot render over exclusive-fullscreen games - use borderless windowed.
/// </summary>
public sealed class OverlayWindow : Window
{
    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly LevelEngine _engine;
    private readonly DispatcherTimer _tick;
    private readonly Canvas _canvas = new();

    private readonly List<IOverlayStyle> _activeStyles = new();
    private SideBarsStyle? _sideBars;
    private OverlayStyle _builtStyle;
    private bool _builtPaired = true; // force initial build
    private Rect _workArea;

    // Move mode
    private bool _moveMode;
    private int _selectedBar; // 0 left, 1 right
    private bool _dragging;
    private int _dragBar = -1;
    private double _revertLeft, _revertRight, _revertSize;
    private readonly List<UIElement> _moveModeChrome = new();
    private TextBlock? _leftChipText, _rightChipText;

    private double _currentOpacityTarget = -1;

    public event EventHandler? PositionChanged;

    public bool IsInMoveMode => _moveMode;

    public OverlayWindow(Speakers speakers)
    {
        _engine = new LevelEngine(speakers);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Content = _canvas;

        SourceInitialized += (_, _) => WindowHelper.SetClickThrough(this, true);

        // Any foreground change (another app, the Start menu, a game alt-tab)
        // can knock this window out of the Win32 topmost band while WPF's
        // Topmost property still reads true. Re-assert on every change.
        _foregroundCallback = (_, _, _, _, _, _, _) =>
        {
            if (IsVisible && !_moveMode)
                WindowHelper.ReassertTopmost(this);
        };
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundCallback, 0, 0, WINEVENT_OUTOFCONTEXT);
        Closed += (_, _) =>
        {
            if (_foregroundHook != IntPtr.Zero)
                UnhookWinEvent(_foregroundHook);
            SetHighResTimer(false);
        };

        _tick = new DispatcherTimer(DispatcherPriority.Render);
        _tick.Tick += (_, _) => RenderFrame();

        _canvas.MouseLeftButtonDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeftButtonUp += Canvas_MouseUp;
        PreviewKeyDown += OnMoveModeKey;

        ApplySettings();
    }

    // --- Lifecycle ---

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            Show();
            WindowHelper.ReassertTopmost(this);
            _tick.Start();
        }
        else
        {
            if (_moveMode) ExitMoveMode(commit: true);
            _tick.Stop();
            Hide();
        }
    }

    /// <summary>Puts the overlay back above everything without activating it.</summary>
    public void ReassertTopmost() => WindowHelper.ReassertTopmost(this);

    // --- Foreground-change hook (see ctor) ---

    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd,
        int objectId, int childId, uint threadId, uint timestamp);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
        WinEventDelegate callback, uint processId, uint threadId, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    // Kept as fields so the GC never collects the native callback
    private WinEventDelegate? _foregroundCallback;
    private IntPtr _foregroundHook;

    public void StopAndClose()
    {
        _tick.Stop();
        Close();
    }

    /// <summary>Re-reads settings: monitor, active style set, layout.</summary>
    public void ApplySettings()
    {
        var settings = _settingsManager.Settings;

        var fps = Math.Clamp(settings.Display.OverlayFps, 30, 240);
        var intervalMs = 1000.0 / fps;
        _tick.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _engine.SetTickInterval(intervalMs);
        _reassertEveryNFrames = Math.Max(1, (int)(2000 / intervalMs));
        // Windows timers only resolve ~15.6 ms by default, which caps a
        // DispatcherTimer near 64 Hz. Above 60 fps, request 1 ms resolution.
        SetHighResTimer(fps > 60);

        var screen = MainWindow.TargetScreen;
        _workArea = new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top,
            screen.WorkingArea.Width, screen.WorkingArea.Height);

        Left = _workArea.Left;
        Top = _workArea.Top;
        Width = _workArea.Width;
        Height = _workArea.Height;

        var style = settings.Bars.OverlayStyle;
        var paired = style == OverlayStyle.SideBars || settings.Bars.PairWithSideBars;

        if (style != _builtStyle || paired != _builtPaired)
            RebuildStyles(style, paired);

        var localArea = new Rect(0, 0, _workArea.Width, _workArea.Height);
        foreach (var active in _activeStyles)
            active.ApplyLayout(settings, localArea);

        if (_moveMode)
            LayoutMoveModeChrome();
    }

    private void RebuildStyles(OverlayStyle style, bool paired)
    {
        foreach (var active in _activeStyles)
            active.Detach(_canvas);
        _activeStyles.Clear();
        _sideBars = null;

        if (paired)
        {
            _sideBars = new SideBarsStyle();
            _activeStyles.Add(_sideBars);
        }

        IOverlayStyle? main = style switch
        {
            OverlayStyle.RadarRing => new RadarRingStyle(),
            OverlayStyle.RingPing => new RingPingStyle(),
            OverlayStyle.CompassStrip => new CompassStripStyle(),
            OverlayStyle.EdgeGlow => new EdgeGlowStyle(),
            _ => null // SideBars is already the paired style
        };
        if (main != null)
            _activeStyles.Add(main);

        foreach (var active in _activeStyles)
            active.Attach(_canvas);

        _builtStyle = style;
        _builtPaired = paired;
    }

    private bool _wasIdle;
    private int _frameCount;
    private int _reassertEveryNFrames = 60;
    private bool _highResTimer;

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint ms);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint ms);

    private void SetHighResTimer(bool on)
    {
        if (on == _highResTimer) return;
        _highResTimer = on;
        if (on) timeBeginPeriod(1);
        else timeEndPeriod(1);
    }

    private void RenderFrame()
    {
        // Belt-and-braces: the Win32 topmost band can be silently lost when
        // other app windows are shown/activated; re-assert every 2 s (no-op
        // SetWindowPos when already topmost, never activates).
        if (++_frameCount % _reassertEveryNFrames == 0)
            WindowHelper.ReassertTopmost(this);
        var bars = _settingsManager.Settings.Bars;
        _engine.Tick(bars);

        // Perf: once everything is silent and the peak trails have decayed,
        // stop touching the visual tree until sound returns.
        var maxTrail = 0.0;
        for (int i = 0; i < 8; i++)
            maxTrail = Math.Max(maxTrail, _engine.Frame.Trails[i]);
        var idle = !_engine.Frame.AnyActive && maxTrail <= 0.015;

        if (!(idle && _wasIdle))
        {
            foreach (var active in _activeStyles)
                active.Render(_engine.Frame, bars);
        }
        _wasIdle = idle;

        UpdateWindowOpacity(bars);
    }

    private void UpdateWindowOpacity(BarSettings bars)
    {
        var target = !bars.TransparentMode || _engine.Frame.AnyActive || _moveMode
            ? bars.MaxOpacity
            : 0.0;

        if (Math.Abs(target - _currentOpacityTarget) < 0.001)
            return;

        _currentOpacityTarget = target;
        var rising = target > Opacity;
        WindowHelper.AnimateOpacity(this, target, rising ? bars.FadeInMs : bars.FadeOutMs);
    }

    // --- Move mode (plan Phase 4 shared behaviors) ---

    public void EnterMoveMode()
    {
        if (_moveMode) return;
        _moveMode = true;

        var bars = _settingsManager.Settings.Bars;
        _revertLeft = bars.LeftIndicatorPercent;
        _revertRight = bars.RightIndicatorPercent;
        _revertSize = bars.OverlaySize;
        _selectedBar = 0;

        WindowHelper.SetClickThrough(this, false);
        Focusable = true;
        Activate();
        Focus();

        BuildMoveModeChrome();
    }

    public void ExitMoveMode(bool commit)
    {
        if (!_moveMode) return;
        _moveMode = false;

        if (commit)
        {
            _settingsManager.Save();
            _settingsManager.NotifyChanged();
        }
        else
        {
            _settingsManager.UpdateSilent(s =>
            {
                s.Bars.LeftIndicatorPercent = _revertLeft;
                s.Bars.RightIndicatorPercent = _revertRight;
                s.Bars.OverlaySize = _revertSize;
            });
            ApplySettings();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        foreach (var element in _moveModeChrome)
            _canvas.Children.Remove(element);
        _moveModeChrome.Clear();
        _leftChipText = null;
        _rightChipText = null;

        Focusable = false;
        WindowHelper.SetClickThrough(this, true);
    }

    private void BuildMoveModeChrome()
    {
        // Dashed vertical center guide
        var guide = new Line
        {
            X1 = _workArea.Width / 2, X2 = _workArea.Width / 2,
            Y1 = 0, Y2 = _workArea.Height,
            Stroke = new SolidColorBrush(Color.FromArgb(178, 0x56, 0xB4, 0xE9)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 7, 14 }
        };
        _canvas.Children.Add(guide);
        _moveModeChrome.Add(guide);

        // Top-center pill
        var pillText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            TextAlignment = TextAlignment.Center
        };
        pillText.Inlines.Add(new System.Windows.Documents.Run(
            "MOVE MODE — drag · snaps every 5 % · Tab selects · arrows nudge · +/− size  ")
        { FontWeight = FontWeights.Bold });
        pillText.Inlines.Add(new System.Windows.Documents.Run("Enter save · Esc cancel · Ctrl+Shift+P resets")
        {
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
        });

        var pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(217, 8, 10, 13)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0x56, 0xB4, 0xE9)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(26),
            Padding = new Thickness(20, 10, 20, 10),
            Child = pillText
        };
        pill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(pill, (_workArea.Width - pill.DesiredSize.Width) / 2);
        Canvas.SetTop(pill, 24);
        _canvas.Children.Add(pill);
        _moveModeChrome.Add(pill);

        // Per-indicator % readout chips (side bars only)
        if (_sideBars != null)
        {
            _leftChipText = MakeChip(out var leftChip);
            _rightChipText = MakeChip(out var rightChip);
            _canvas.Children.Add(leftChip);
            _canvas.Children.Add(rightChip);
            _moveModeChrome.Add(leftChip);
            _moveModeChrome.Add(rightChip);
        }

        LayoutMoveModeChrome();
    }

    private TextBlock MakeChip(out Border chip)
    {
        var text = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        chip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(217, 8, 10, 13)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0x56, 0xB4, 0xE9)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5, 10, 5),
            Child = text
        };
        return text;
    }

    private void LayoutMoveModeChrome()
    {
        if (_leftChipText == null || _rightChipText == null) return;

        var bars = _settingsManager.Settings.Bars;
        _leftChipText.Text = $"{bars.LeftIndicatorPercent * 100:F0} %" + (_selectedBar == 0 ? "  ◀" : "");
        _rightChipText.Text = $"{bars.RightIndicatorPercent * 100:F0} %" + (_selectedBar == 1 ? "  ◀" : "");

        var leftChip = (Border)_leftChipText.Parent;
        var rightChip = (Border)_rightChipText.Parent;
        leftChip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        rightChip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(leftChip, bars.LeftIndicatorPercent * _workArea.Width - leftChip.DesiredSize.Width / 2);
        Canvas.SetTop(leftChip, _workArea.Height / 2 - 14);
        Canvas.SetLeft(rightChip, bars.RightIndicatorPercent * _workArea.Width - rightChip.DesiredSize.Width / 2);
        Canvas.SetTop(rightChip, _workArea.Height / 2 - 14);
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_moveMode || _sideBars == null) return;
        var hit = _sideBars.HitTestBar(e.GetPosition(_canvas), _settingsManager.Settings);
        if (hit >= 0)
        {
            _dragging = true;
            _dragBar = hit;
            _selectedBar = hit;
            _canvas.CaptureMouse();
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _dragBar < 0) return;
        var pct = SnapPercent(e.GetPosition(_canvas).X / _workArea.Width);
        SetBarPercent(_dragBar, pct);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _dragBar = -1;
        _canvas.ReleaseMouseCapture();
    }

    private void OnMoveModeKey(object sender, KeyEventArgs e)
    {
        if (!_moveMode) return;

        var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.05 : 0.01;
        var bars = _settingsManager.Settings.Bars;

        switch (e.Key)
        {
            case Key.Tab:
                _selectedBar = 1 - _selectedBar;
                LayoutMoveModeChrome();
                break;
            case Key.Left:
                NudgeBar(_selectedBar, -step);
                break;
            case Key.Right:
                NudgeBar(_selectedBar, step);
                break;
            case Key.OemPlus or Key.Add:
                SetOverlaySize(bars.OverlaySize + 0.05);
                break;
            case Key.OemMinus or Key.Subtract:
                SetOverlaySize(bars.OverlaySize - 0.05);
                break;
            case Key.Enter:
                ExitMoveMode(commit: true);
                break;
            case Key.Escape:
                ExitMoveMode(commit: false);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void NudgeBar(int bar, double delta)
    {
        var bars = _settingsManager.Settings.Bars;
        var current = bar == 0 ? bars.LeftIndicatorPercent : bars.RightIndicatorPercent;
        SetBarPercent(bar, current + delta);
    }

    private void SetBarPercent(int bar, double pct)
    {
        _settingsManager.UpdateSilent(s =>
        {
            var bars = s.Bars;
            if (bar == 0)
            {
                bars.LeftIndicatorPercent = Math.Clamp(pct, 0.0, 0.45);
                if (bars.LinkIndicators)
                    bars.RightIndicatorPercent = 1.0 - bars.LeftIndicatorPercent;
            }
            else
            {
                bars.RightIndicatorPercent = Math.Clamp(pct, 0.55, 1.0);
                if (bars.LinkIndicators)
                    bars.LeftIndicatorPercent = 1.0 - bars.RightIndicatorPercent;
            }
        });

        var settings = _settingsManager.Settings;
        var localArea = new Rect(0, 0, _workArea.Width, _workArea.Height);
        foreach (var active in _activeStyles)
            active.ApplyLayout(settings, localArea);
        LayoutMoveModeChrome();
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetOverlaySize(double size)
    {
        _settingsManager.UpdateSilent(s => s.Bars.OverlaySize = Math.Clamp(size, 0.5, 2.0));
        var settings = _settingsManager.Settings;
        var localArea = new Rect(0, 0, _workArea.Width, _workArea.Height);
        foreach (var active in _activeStyles)
            active.ApplyLayout(settings, localArea);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static double SnapPercent(double pct)
    {
        return Math.Round(pct * 20) / 20; // 5 % grid
    }
}
