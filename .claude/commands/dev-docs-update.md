---
description: Update dev-docs with current progress and learnings
argument-hint: [task name]
---

# Dev Docs Update - Context Refresh Command

Update the development documentation files to reflect current progress and learnings.

## Instructions

Review and update all three dev-docs files for the current task:

### 1. Update `[task-name]-plan.md`
- Mark completed phases
- Adjust remaining phases based on learnings
- Update risk assessment with discovered issues
- Add any new insights or approach changes
- Document any pivots or scope changes

### 2. Update `[task-name]-context.md`
- Add newly discovered key files
- Document any architectural changes made
- Update integration points if they changed
- Add new dependencies
- Record important implementation details

### 3. Update `[task-name]-tasks.md`
- Check off completed tasks
- Add new tasks discovered during implementation
- Remove tasks that became irrelevant
- Reorder tasks if priorities changed
- Add notes on any blockers or issues

## When to Use

Run this command:
- Before context is about to compact (long conversation)
- After completing major phases
- When pivoting approach or discovering new requirements
- Before taking a break from the task

## What to Include

### Completed Work
- What was implemented
- How it differs from the original plan
- Any shortcuts or trade-offs made

### Learnings
- Unexpected complexity discovered
- Better approaches identified
- Things that were easier/harder than expected

### Remaining Work
- Updated task list
- Changed priorities
- New blockers or dependencies

### Important Decisions
- Why certain approaches were chosen
- What alternatives were rejected
- Trade-offs made for MVP speed

## Output Format

Provide a summary after updating:
```markdown
## Dev Docs Updated

### Progress Summary
- Completed: [X] tasks
- Remaining: [Y] tasks
- Blockers: [Z issues]

### Key Changes
- [Change 1]
- [Change 2]

### Next Steps
1. [Next priority task]
2. [Second priority]
```

Remember: Keep docs up-to-date but don't spend more time documenting than coding.
