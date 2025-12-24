# WPF Development Guidelines

Guidelines for developing WPF applications with C# and .NET.

## Data Binding

### INotifyPropertyChanged
```csharp
public class ViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

### XAML Binding
```xml
<!-- Simple binding -->
<TextBlock Text="{Binding Name}" />

<!-- With update trigger -->
<Rectangle Fill="{Binding Color, UpdateSourceTrigger=PropertyChanged}" />

<!-- Mode options -->
<TextBox Text="{Binding Name, Mode=TwoWay}" />
```

## Threading

### UI Thread Updates
```csharp
// From background thread, update UI via Dispatcher
await Application.Current.Dispatcher.InvokeAsync(() =>
{
    // UI updates here
    MyProperty = newValue;
});

// Or use BeginInvoke for fire-and-forget
Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
{
    _speakers.Update();
});
```

### Background Work
```csharp
// Use Task.Run for background work
await Task.Run(() =>
{
    // Long-running work here
    for (;;)
    {
        Thread.Sleep(200);
        Dispatcher.BeginInvoke(() => UpdateUI());
    }
});
```

## Window Management

### Positioning
```csharp
// Use system parameters for screen dimensions
double screenWidth = SystemParameters.PrimaryScreenWidth;
double screenHeight = SystemParameters.PrimaryScreenHeight;

// Position window
Left = 0;
Top = 0;
Width = 50;
Height = screenHeight;
```

### Window Properties
```csharp
// In constructor before InitializeComponent()
Topmost = true;           // Always on top
WindowStyle = None;       // No title bar
ResizeMode = NoResize;    // Fixed size
```

### XAML Window Settings
```xml
<Window
    WindowStyle="None"
    ResizeMode="NoResize"
    Topmost="True"
    ShowInTaskbar="False">
```

## Layout

### Grid (Flexible)
```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*" />      <!-- Proportional -->
        <RowDefinition Height="2.5*" />   <!-- 2.5x the first -->
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <Rectangle Grid.Row="0" Fill="{Binding TopColor}" />
    <Rectangle Grid.Row="1" Fill="{Binding CenterColor}" />
    <Rectangle Grid.Row="2" Fill="{Binding BottomColor}" />
</Grid>
```

### StackPanel (Simple)
```xml
<StackPanel Orientation="Vertical">
    <Rectangle Height="100" />
    <Rectangle Height="200" />
</StackPanel>
```

## Resource Management

### IDisposable Pattern
```csharp
public class AudioManager : IDisposable
{
    private MMDevice? _device;
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _device?.Dispose();
            _disposed = true;
        }
    }
}
```

### Event Unsubscription
```csharp
// Subscribe
_device.PropertyChanged += OnDevicePropertyChanged;

// Unsubscribe in cleanup
_device.PropertyChanged -= OnDevicePropertyChanged;
```

## Common Patterns

### Application Lifecycle
```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Setup after InitializeComponent
        ShowScreen();
        StartMonitoring();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Cleanup here
        base.OnClosed(e);
    }
}
```

### Color/Brush Handling
```csharp
// Create SolidColorBrush from Color
public Brush ColorBrush => new SolidColorBrush(Color.FromRgb(r, g, b));

// Predefined brushes (more efficient)
public Brush Background => Brushes.White;
```

## Performance Tips

1. **Freeze Brushes** when not changing: `brush.Freeze()`
2. **Virtualize large lists**: `VirtualizingStackPanel.IsVirtualizing="True"`
3. **Avoid frequent property changes**: Batch updates when possible
4. **Use compiled bindings** for performance-critical paths

## Debugging

### Binding Errors
Add to App.xaml.cs:
```csharp
PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
```

### Dispatcher Issues
If UI not updating, ensure you're on the UI thread:
```csharp
Debug.Assert(Dispatcher.CheckAccess(), "Must be on UI thread");
```

## Keep It Simple

- Don't use MVVM framework unless you need it
- Direct code-behind is fine for simple apps
- Only add complexity when it solves a real problem
- Test manually - automated UI tests are often more trouble than worth for small projects
