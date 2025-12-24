# Code Cleanup - Architectural Context

## Key Files

### Files to DELETE (Phase 1)
| File | Lines | Reason |
|------|-------|--------|
| `View/LeftScreen.xaml` | 41 | Replaced by DualBarsView - zero references |
| `View/LeftScreen.xaml.cs` | 235 | Replaced by DualBarsView - zero references |
| `View/RightScreen.xaml` | 41 | Replaced by DualBarsView - zero references |
| `View/RightScreen.xaml.cs` | 236 | Replaced by DualBarsView - zero references |
| `Annotations.cs` | 1,219 | JetBrains ReSharper annotations - never used |

**Total: 1,772 lines removed**

### Files to CREATE
| File | Purpose |
|------|---------|
| `Helpers/WindowHelper.cs` | Static helper for click-through and animation |

### Files to MODIFY
| File | Changes |
|------|---------|
| `View/DualBarsView.xaml.cs` | Use WindowHelper, remove inline P/Invoke |
| `View/HorizontalDualView.xaml.cs` | Use WindowHelper, remove inline P/Invoke |
| `View/Full7Point1View.xaml.cs` | Use WindowHelper, extract constants |
| `MainWindow.xaml` | Remove placeholder label |
| Multiple files | Convert to file-scoped namespaces |

## Architectural Decisions

### Why Delete LeftScreen/RightScreen?
**Evidence of obsolescence:**
```bash
# Search for usage in codebase
grep -r "LeftScreen\|RightScreen" *.cs --include="MainWindow.xaml.cs"
# Result: No matches
```

The DualBarsView was created to replace the two separate windows with a single full-screen canvas containing both bars. This:
- Reduces window management complexity
- Enables linked dragging without cross-window references
- Simplifies transparent mode (one window to animate)

### Why Delete Annotations.cs?
**Evidence:**
- File is JetBrains ReSharper copy-paste boilerplate (1,219 lines)
- Uses `[CanBeNull]`, `[NotNull]`, `[Pure]` etc. for IDE hints
- Grep shows NO usage outside the Annotations.cs file itself
- Modern C# has nullable reference types (`?`) built-in
- Generates 14 compiler warnings about nullable properties

### Why Extract WindowHelper vs. Create Base Class?
**WindowHelper (chosen):**
- Static methods, no inheritance hierarchy
- Each window remains independent
- Minimal code change required
- Follows CLAUDE.md "Simple > Complex"

**Base class (rejected):**
- Would require changing all window inheritance
- XAML partial classes complicate inheritance
- Different windows have different drag behaviors
- Over-engineering for 3 windows

### Why Not Extract More?
Following CLAUDE.md principles:
- **"Rule of Three"**: Only extract patterns appearing 3+ times
- **"YAGNI"**: Don't build abstractions for hypothetical futures
- **"Working > Perfect"**: Current architecture works well

## Dependencies

### Windows API (P/Invoke)
Used for click-through window behavior:
```csharp
[DllImport("user32.dll")]
private static extern int GetWindowLong(IntPtr hwnd, int index);

[DllImport("user32.dll")]
private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
```

Constants:
- `GWL_EXSTYLE = -20` (extended window style)
- `WS_EX_TRANSPARENT = 0x00000020` (click-through flag)

### WPF Dependencies
- `System.Windows.Interop.WindowInteropHelper` - Get window handle
- `System.Windows.Media.Animation.DoubleAnimation` - Opacity animation
- `System.Windows.Media.Animation.QuadraticEase` - Easing function

## Configuration Changes
None required - this is pure code refactoring.

## Integration Points

### WindowHelper Integration
```csharp
// Before (in each view file):
private void SetClickThrough(bool clickThrough)
{
    var hwnd = new WindowInteropHelper(this).Handle;
    // ... 10 lines of P/Invoke code
}

// After:
private void SetClickThrough(bool clickThrough)
{
    WindowHelper.SetClickThrough(this, clickThrough);
}
```

### Animation Helper Integration
```csharp
// Before (in each view file):
private void AnimateOpacity(double targetOpacity, int durationMs)
{
    var animation = new DoubleAnimation { ... };
    BeginAnimation(OpacityProperty, animation);
}

// After:
private void AnimateOpacity(double targetOpacity, int durationMs)
{
    WindowHelper.AnimateOpacity(this, targetOpacity, durationMs);
}
```

## Duplicate Code Analysis

### SetClickThrough Pattern
**Files:** DualBarsView, HorizontalDualView, Full7Point1View, LeftScreen*, RightScreen*
**Lines each:** 11
**Total duplicate:** 55 lines → 11 lines (in helper)
*Files marked with * will be deleted

### AnimateOpacity Pattern
**Files:** DualBarsView, HorizontalDualView, LeftScreen*, RightScreen*
**Lines each:** 9
**Total duplicate:** 36 lines → 9 lines (in helper)

### OnSpeakersPropertyChanged Pattern
**Files:** DualBarsView, HorizontalDualView
**Similar but not identical** - different property checks (LeftActivity vs RightActivity)
**Decision:** Keep inline - logic differs enough to warrant separate implementations

## File-Scoped Namespace Conversion

### Current State (Mixed)
```csharp
// Old style (block)
namespace DeafDirectionalHelper.View
{
    public partial class DualBarsView : Window
    {
        // ...
    }
}

// New style (file-scoped)
namespace DeafDirectionalHelper.Settings;

public class SettingsManager
{
    // ...
}
```

### Target State
All files use file-scoped namespaces (C# 10+):
```csharp
namespace DeafDirectionalHelper.View;

public partial class DualBarsView : Window
{
    // ...
}
```
