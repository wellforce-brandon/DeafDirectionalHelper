---
description: Perform architecture review focused on C#/WPF best practices
---

# Code Review - C#/WPF Architecture & Best Practices

Perform a comprehensive code review focused on architecture, production principles, and best practices.

## Instructions

Review the recently modified code for adherence to project standards.

### Review Checklist

#### 1. Production Principles Compliance
- [ ] Following "simplest reliable solution" principle
- [ ] No unjustified enterprise patterns
- [ ] Using preferred patterns (direct code, simple event handling)
- [ ] Building for actual use case (not hypothetical scale)
- [ ] Abstraction only after 3rd duplicate (Rule of Three)

#### 2. WPF Best Practices
- [ ] Proper data binding (INotifyPropertyChanged implemented correctly)
- [ ] UI updates on Dispatcher thread
- [ ] Resources properly disposed (IDisposable pattern)
- [ ] No blocking calls on UI thread
- [ ] Reasonable use of MVVM (don't over-architect)

#### 3. Code Quality
- [ ] Functions are focused and single-purpose
- [ ] Minimal nesting (prefer early returns)
- [ ] Comments for complex logic
- [ ] No dead/commented code
- [ ] Consistent naming (PascalCase for public, _camelCase for private fields)

#### 4. C# Patterns
- [ ] Using `using` statements or `IDisposable` for resources
- [ ] Null safety (nullable reference types handled)
- [ ] Exception handling on external calls
- [ ] Async/await used correctly (not blocking)
- [ ] LINQ used appropriately (readable, not over-complex)

#### 5. Architecture
- [ ] Simple file structure (not over-modularized)
- [ ] Config for runtime values, constants for fixed values
- [ ] Reasonable separation (View/ViewModel/Model if needed)
- [ ] No unnecessary interfaces or abstractions

#### 6. Performance (WPF Specific)
- [ ] No UI updates in tight loops
- [ ] Background work on separate threads
- [ ] Virtualization for large lists (if applicable)
- [ ] No memory leaks (event handlers unsubscribed)

## Review Output

Provide feedback in this format:

### Good Practices Found
- [Specific example of well-written code]
- [Another example]

### Concerns
1. **[Issue Type]**: [Specific concern]
   - **Location**: [file:line]
   - **Current**: [What it does now]
   - **Suggestion**: [Simple fix]
   - **Priority**: [High/Medium/Low]

### Production Principles Violations
1. **[Violation]**: [Over-engineered or missing requirement]
   - **Location**: [file:line]
   - **Why it's a problem**: [Brief explanation]
   - **Fix**: [Simpler approach]

### Quick Wins
- [Easy improvements that add value]

### Technical Debt (Document but Accept)
- [Reasonable shortcuts that work]
- [Things to revisit if scope grows]

## Review Guidelines

### Be Pragmatic
- This is a personal/small project
- Simple + Working > Complex + Perfect
- Don't suggest patterns for hypothetical futures

### Focus On (High Priority)
- Bugs or logic errors
- Resource leaks (memory, handles)
- Thread safety issues
- Missing error handling

### Don't Nitpick
- Minor style inconsistencies
- Lack of comments (unless logic is complex)
- "Better" ways that add complexity

## Example Feedback

```markdown
### Concerns

1. **Missing Disposal**: MMDevice not disposed
   - **Location**: Audio/Speakers.cs:8
   - **Current**: MMDevice stored but never disposed
   - **Suggestion**: Implement IDisposable and dispose in cleanup
   - **Priority**: Medium

2. **Thread Safety**: Speaker.Value accessed from multiple threads
   - **Location**: Audio/Speaker.cs:15-30
   - **Current**: Value property has no synchronization
   - **Suggestion**: Use lock or Interlocked for thread safety
   - **Priority**: Low (200ms polling reduces risk)
```

## After Review

1. Discuss findings with user
2. Prioritize fixes (bugs > resource leaks > style)
3. Make changes or document accepted shortcuts
4. Re-verify critical changes

Remember: The goal is working, maintainable code - not perfect code.
