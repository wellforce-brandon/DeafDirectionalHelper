# C# Development Guidelines

Guidelines for writing clean, maintainable C# code.

## Naming Conventions

```csharp
// PascalCase for: classes, methods, properties, public fields
public class AudioManager { }
public void UpdateValues() { }
public string DeviceName { get; set; }

// camelCase for: parameters, local variables
public void Process(string deviceName) { var localValue = 0; }

// _camelCase for: private fields
private readonly MMDevice _device;
private int _updateCount;

// UPPER_CASE for: constants (optional, PascalCase also acceptable)
public const int MAX_CHANNELS = 8;
public const int MaxChannels = 8; // Also valid
```

## Null Safety

### Nullable Reference Types
```csharp
// Enable in .csproj
<Nullable>enable</Nullable>

// Nullable types use ?
public string? OptionalName { get; set; }

// Non-nullable must be initialized
public string RequiredName { get; set; } = "";

// Null checks
if (device?.AudioMeter is not null)
{
    // Safe to use
}

// Null-forgiving operator (when you KNOW it's not null)
_device = devices.First()!;
```

### Defensive Coding
```csharp
public void Process(string? input)
{
    if (string.IsNullOrEmpty(input)) return;

    // Or throw
    if (input is null)
        throw new ArgumentNullException(nameof(input));
}
```

## Error Handling

### Try-Catch for External Operations
```csharp
public void ConnectToDevice()
{
    try
    {
        _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to connect: {ex.Message}");
        throw; // Re-throw if can't recover
    }
}
```

### Custom Exceptions (Only When Needed)
```csharp
// Simple custom exception
public class AudioDeviceException : Exception
{
    public AudioDeviceException(string message) : base(message) { }
}

// Usage
throw new AudioDeviceException($"Device has {count} channels, need 8");
```

## Properties

### Auto-Properties
```csharp
// Simple auto-property
public string Name { get; set; } = "";

// Read-only
public string Id { get; }

// Init-only (C# 9+)
public string Config { get; init; }
```

### With Backing Field (for INotifyPropertyChanged)
```csharp
private float _value;
public float Value
{
    get => _value;
    set
    {
        if (Math.Abs(_value - value) > 0.001f)
        {
            _value = value;
            OnPropertyChanged();
        }
    }
}
```

## LINQ

### Keep It Readable
```csharp
// Good - readable
var device = devices
    .Where(d => d.FriendlyName.Contains("CABLE-C"))
    .FirstOrDefault();

// Avoid - too complex in one line
var result = items.Where(x => x.Active).Select(x => x.Name).Distinct().OrderBy(x => x).Take(10).ToList();

// Better - break it up or use regular loops for complex logic
```

### Common Operations
```csharp
// Find first match
var first = items.FirstOrDefault(x => x.IsActive);

// Filter
var filtered = items.Where(x => x.Value > 0);

// Any/All checks
bool hasAny = items.Any(x => x.IsActive);
bool allActive = items.All(x => x.IsActive);

// Count with condition
int count = items.Count(x => x.Value > threshold);
```

## Async/Await

### Basic Pattern
```csharp
public async Task<string> LoadDataAsync()
{
    var result = await _client.GetStringAsync(url);
    return result;
}

// Async void only for event handlers
private async void Button_Click(object sender, EventArgs e)
{
    await DoWorkAsync();
}
```

### Fire-and-Forget (Use Carefully)
```csharp
// When you truly don't care about the result
_ = Task.Run(() => BackgroundWork());
```

## Resource Management

### Using Statement
```csharp
// Preferred for disposables
using var stream = File.OpenRead(path);
// stream disposed at end of scope

// Or traditional
using (var stream = File.OpenRead(path))
{
    // use stream
} // disposed here
```

### IDisposable Pattern
```csharp
public class ResourceManager : IDisposable
{
    private bool _disposed;
    private MMDevice? _device;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _device?.Dispose();
            }
            _disposed = true;
        }
    }
}
```

## Code Organization

### File Structure
```csharp
// 1. Usings
using System;
using System.Linq;

// 2. Namespace
namespace MyApp.Audio;

// 3. Class
public class Speaker
{
    // 4. Constants/static fields
    private const int MaxRetries = 3;

    // 5. Instance fields
    private readonly MMDevice _device;
    private float _value;

    // 6. Properties
    public float Value { get; set; }

    // 7. Constructor
    public Speaker() { }

    // 8. Public methods
    public void Update() { }

    // 9. Private methods
    private void ProcessInternal() { }
}
```

### Early Returns
```csharp
// Good - clear flow
public void Process(Data? data)
{
    if (data is null) return;
    if (!data.IsValid) return;

    // Main logic here, not deeply nested
    DoWork(data);
}

// Avoid - deep nesting
public void Process(Data? data)
{
    if (data != null)
    {
        if (data.IsValid)
        {
            DoWork(data);
        }
    }
}
```

## Keep It Simple

- Use auto-properties unless you need custom logic
- Prefer composition over inheritance
- Don't create interfaces for single implementations
- Use `var` for obvious types, explicit types when it aids clarity
- Comments for "why", not "what"
