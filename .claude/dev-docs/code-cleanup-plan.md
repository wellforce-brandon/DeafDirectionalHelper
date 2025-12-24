# Code Cleanup & Deduplication Plan

## Executive Summary
Comprehensive code review and cleanup of DeafDirectionalHelper to remove dead code, consolidate duplicate patterns, and improve maintainability. Primary focus: removing ~500 lines of obsolete code and extracting ~150 lines of duplicated window behavior into a shared utility.

## Implementation Phases

### Phase 1: Remove Dead Code (Low Risk, High Impact)
**Dependencies:** None
**Goal:** Delete unused files with zero functional impact

1. Delete obsolete LeftScreen/RightScreen files (replaced by DualBarsView)
2. Delete Annotations.cs (JetBrains ReSharper annotations - unused)
3. Clean up placeholder content in MainWindow.xaml

**Estimated lines removed:** ~700

### Phase 2: Extract Click-Through Helper (Medium Risk, High Impact)
**Dependencies:** Phase 1 complete
**Goal:** Consolidate duplicate Win32 interop code

The SetClickThrough pattern appears in 5 files with identical implementation:
- DualBarsView.xaml.cs
- HorizontalDualView.xaml.cs
- Full7Point1View.xaml.cs
- LeftScreen.xaml.cs (will be deleted in Phase 1)
- RightScreen.xaml.cs (will be deleted in Phase 1)

Create `Helpers/WindowHelper.cs` with static method.

### Phase 3: Extract Animation Helper (Low Risk, Medium Impact)
**Dependencies:** Phase 1 complete
**Goal:** Consolidate duplicate animation code

The AnimateOpacity pattern appears in 3 active files:
- DualBarsView.xaml.cs
- HorizontalDualView.xaml.cs
- (LeftScreen/RightScreen - deleted in Phase 1)

Add to `Helpers/WindowHelper.cs`.

### Phase 4: Standardize Code Style (Low Risk, Low Impact)
**Dependencies:** Phases 1-3 complete
**Goal:** Consistent coding conventions

1. Convert all namespace declarations to C# 10 file-scoped style
2. Standardize event handler naming (use `On` prefix consistently)
3. Extract magic numbers in Full7Point1View to constants

## Detailed Task Breakdown

### Phase 1 Tasks
| Task | Files Affected | Risk |
|------|---------------|------|
| Delete LeftScreen.xaml | View/LeftScreen.xaml | None |
| Delete LeftScreen.xaml.cs | View/LeftScreen.xaml.cs | None |
| Delete RightScreen.xaml | View/RightScreen.xaml | None |
| Delete RightScreen.xaml.cs | View/RightScreen.xaml.cs | None |
| Delete Annotations.cs | Annotations.cs | None |
| Remove placeholder label | MainWindow.xaml | None |

### Phase 2 Tasks
| Task | Files Affected | Risk |
|------|---------------|------|
| Create WindowHelper.cs | New: Helpers/WindowHelper.cs | Low |
| Add SetClickThrough method | Helpers/WindowHelper.cs | Low |
| Update DualBarsView | View/DualBarsView.xaml.cs | Low |
| Update HorizontalDualView | View/HorizontalDualView.xaml.cs | Low |
| Update Full7Point1View | View/Full7Point1View.xaml.cs | Low |

### Phase 3 Tasks
| Task | Files Affected | Risk |
|------|---------------|------|
| Add AnimateOpacity method | Helpers/WindowHelper.cs | Low |
| Update DualBarsView | View/DualBarsView.xaml.cs | Low |
| Update HorizontalDualView | View/HorizontalDualView.xaml.cs | Low |

### Phase 4 Tasks
| Task | Files Affected | Risk |
|------|---------------|------|
| Convert namespace in App.xaml.cs | App.xaml.cs | Low |
| Convert namespace in MainWindow.xaml.cs | MainWindow.xaml.cs | Low |
| Convert namespace in View files | View/*.cs | Low |
| Convert namespace in Audio files | Audio/*.cs | Low |
| Extract Full7Point1View constants | View/Full7Point1View.xaml.cs | Low |

## Risk Assessment

### Low Risk Items
- **Dead code deletion**: LeftScreen/RightScreen have zero references in MainWindow
- **Annotations.cs deletion**: Only self-referential, no external usage
- **Helper extraction**: Simple refactoring with identical logic

### Medium Risk Items
- **Namespace changes**: Could affect XAML x:Class references (verify builds after)
- **Animation timing**: Ensure identical behavior after extraction

### Mitigations
1. Build and run after each phase
2. Test all display modes (Dual/7.1/Both) after each phase
3. Test transparent mode fade-in/fade-out behavior
4. Verify hotkeys still work

## Success Metrics

| Metric | Before | After |
|--------|--------|-------|
| Total .cs files | 15 | 11 |
| Lines of code (view layer) | ~1,500 | ~1,100 |
| Duplicate patterns | 5+ | 0 |
| Dead code files | 5 | 0 |

## Rollback Strategy

Since all changes are deletions or extractions:
1. **Phase 1**: Restore deleted files from git
2. **Phase 2-3**: Inline the helper methods back into each file
3. **Phase 4**: Revert namespace changes from git

All phases are independently reversible via `git checkout`.

## NOT Doing (Intentional Scope Limits)

Per CLAUDE.md "Simple > Complex" principle:
- **No base class creation**: Would require significant refactoring for marginal benefit
- **No DI framework**: Overkill for this utility app
- **No view model layer**: Current data binding pattern works well
- **No unit tests**: Manual testing sufficient for this focused tool
