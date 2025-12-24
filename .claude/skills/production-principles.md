# Production Development Principles

> **Philosophy**: Simple, working, maintainable. Not over-engineered, not hacky.

## Golden Rules (ALWAYS Follow)

1. **If it works reliably, ship it** - Perfect is the enemy of done
2. **YAGNI until you need it** - Don't build for hypothetical futures
3. **Simple files > Complex architecture** - Start simple, extract when needed
4. **Direct > Abstract** - Prefer direct solutions, abstract when patterns emerge
5. **Rule of Three** - Wait for 3rd duplicate before extracting

## Reality Check

- **Personal/small project**: Optimize for your actual use case
- **Working software**: Real usage beats theoretical perfection
- **Speed + Reliability**: Ship fast, but don't break things

## Patterns to Avoid (Unless Justified)

### Avoid Unless You Have:

- **Factory patterns**: 5+ different implementations
- **Dependency injection frameworks**: Team of 5+ developers
- **Abstract base classes**: 3+ concrete implementations
- **Repository patterns**: 5+ data sources
- **Custom frameworks**: Same thing done 10+ times

### Always Banned

- **Premature optimization**: Never optimize before measuring
- **Speculative generality**: Never build for "what if" scenarios
- **Gold plating**: Never add features "because it's cool"

## Patterns to Use

**Strongly Encouraged**:
- Simple functions with clear names
- Direct code when <3 uses, extract when 3+ uses
- Configuration for runtime values, constants for fixed values
- Defensive coding (null checks, validation)
- Error handling on external calls

## C#/WPF Specific

### Good Patterns
```csharp
// Direct and simple
public void Update()
{
    if (condition) return; // Early return

    // Do the thing directly
    Speaker1.Value = _device.AudioMeterInformation.PeakValues[0];
}

// Simple event handling
PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
```

### Avoid Over-Engineering
```csharp
// DON'T do this for a simple project:
public interface ISpeakerRepository { }
public class SpeakerRepositoryFactory { }
public abstract class BaseSpeakerService { }

// DO this instead:
public class Speakers { /* direct implementation */ }
```

## Decision Framework

Before any architectural decision, ask:

1. **Is this the simplest solution?** (If no, simplify)
2. **Does it work reliably?** (If yes, ship it)
3. **Can I understand this in 6 months?** (If no, add comments or simplify)
4. **Is abstraction justified?** (3+ duplicates OR clear benefit?)

## Mantras

- "Simple + Reliable beats Complex + Perfect"
- "Abstract after 3rd duplicate, not before"
- "Make it work, make it right, make it fast - IN THAT ORDER"
- "Working code beats perfect architecture"

## The Prime Directive

> **Build the simplest thing that works reliably. Then ship it.**

If you find yourself:
- Creating many files for a simple feature
- Adding abstraction before 3rd use
- Building generic frameworks
- Thinking about "scalability" for a personal tool

**STOP and ask**:

> **"What's the simplest way to make this work?"**
