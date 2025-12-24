---
description: Run dotnet build and systematically fix all C#/compilation errors
---

# Build and Fix - .NET Error Resolution

Run the build process and systematically fix all C#/compilation errors.

## Instructions

### Step 1: Run Build
```bash
dotnet build
```

### Step 2: Analyze Errors
- Capture all error output
- Group errors by type and file
- Prioritize errors (syntax > type > reference > nullable)
- Identify root causes vs symptoms

### Step 3: Fix Systematically
Work through errors in this order:

1. **Syntax Errors** - Fix immediately, these block everything
2. **Reference/Using Errors** - Resolve missing or incorrect namespaces
3. **Type Errors** - Fix type mismatches, missing properties
4. **Nullable Warnings** - Address CS8618, CS8625 warnings
5. **Other Warnings** - Fix if quick, otherwise document

### Step 4: Verify Fix
After each fix:
- Re-run build to verify error is resolved
- Ensure no new errors introduced
- Check related files for cascading issues

### Step 5: Final Verification
```bash
# Run full build
dotnet build

# Run in release mode
dotnet build -c Release

# Run tests if available
dotnet test
```

## Fixing Guidelines

### MVP-First Approach
- Fix errors with simplest solution
- Don't refactor while fixing build errors
- Use explicit types if inference causes issues
- Use `!` (null-forgiving) sparingly but don't fear it for MVP

### Common Fixes

**Missing Namespaces**:
```csharp
// Add required using
using System.Linq;
using NAudio.CoreAudioApi;
```

**Nullable Warnings (CS8618, CS8625)**:
```csharp
// Quick fix - make nullable
public string? Name { get; set; }

// Or initialize in constructor
public string Name { get; set; } = string.Empty;

// Or use null-forgiving when you know it's safe
_device = devices.FirstOrDefault()!;
```

**Type Errors**:
```csharp
// Add explicit cast
var height = (int)SystemParameters.PrimaryScreenHeight;

// Use proper type
double screenWidth = SystemParameters.PrimaryScreenWidth;
```

### What NOT to Do
- Don't refactor unrelated code
- Don't add complex type systems
- Don't create abstract types for MVP
- Don't over-engineer the solution
- Don't suppress warnings with `#pragma` unless truly necessary

## Error Reporting

After fixing, provide summary:
```markdown
## Build Fix Summary

### Errors Fixed: [X]
- [Category 1]: [Count] errors
- [Category 2]: [Count] errors

### Files Modified: [Y]
- [file1.cs]: [brief description]
- [file2.cs]: [brief description]

### Approach:
- [Key decision 1]
- [Key decision 2]

### Remaining Warnings:
- [ ] [Warning or non-blocking issue]

### Build Status: SUCCESS / FAILED
```

## Important Notes

- Fix errors, don't hide them with `#pragma warning disable`
- Document any temporary/MVP shortcuts
- If error is complex, ask user before major refactor
- Keep fixes aligned with MVP principles

Remember: Working code beats perfect types. Ship it.
