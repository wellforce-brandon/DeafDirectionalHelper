---
description: Create strategic implementation plan (3 files: plan, context, tasks)
argument-hint: [task description]
---

# Dev Docs - Strategic Planning Command

Create a comprehensive strategic implementation plan for the current task.

## Instructions

Generate three documentation files in `.claude/dev-docs/`:

### 1. `[task-name]-plan.md`
Create a detailed implementation plan with:
- **Executive Summary**: Brief overview of the feature/change
- **Implementation Phases**: Break down into logical phases with dependencies
- **Detailed Task Breakdown**: Specific, actionable tasks
- **Risk Assessment**: Potential issues and mitigation strategies
- **Success Metrics**: How to verify the implementation works
- **Rollback Strategy**: How to undo changes if needed

### 2. `[task-name]-context.md`
Document key architectural decisions and references:
- **Key Files**: List all files that will be modified or created
- **Architectural Decisions**: Why this approach over alternatives
- **Dependencies**: NuGet packages, Windows APIs, or services involved
- **Configuration Changes**: App.config or settings modifications
- **Integration Points**: How this connects with existing systems

### 3. `[task-name]-tasks.md`
Create a checklist for tracking progress:
```markdown
# [Task Name] - Implementation Checklist

## Phase 1: [Phase Name]
- [ ] Task 1
- [ ] Task 2
- [ ] Task 3

## Phase 2: [Phase Name]
- [ ] Task 1
- [ ] Task 2

## Testing
- [ ] Manual testing completed
- [ ] Edge cases verified
- [ ] Build passes in Release mode

## Deployment
- [ ] Configuration updated
- [ ] Documentation updated
- [ ] Ready for use
```

## Important Notes

- Keep plan concise but comprehensive
- Focus on MVP principles - simplest solution that works
- Break complex tasks into small, verifiable steps
- Consider edge cases but don't over-engineer
- Document WHY decisions are made, not just WHAT

## After Generation

1. Review all three files thoroughly
2. Verify alignment with MVP principles
3. Check for over-engineering or unnecessary complexity
4. Get user approval before proceeding with implementation

Remember: The plan is your roadmap. Update it as you learn, but don't let it become stale.
