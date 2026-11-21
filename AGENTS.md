# AGENTS

Instructions for AI agents working on this repository. Read this before touching anything.

## Ground rules

- Read [README.md](README.md), [TASKS.md](TASKS.md) and the relevant [Design/](Design/) docs first.
- **Never copy C++ code** from the OpenGothic reference project. It is read-only research material
  for behaviour. See [Design/Porting.md](Design/Porting.md).
- Follow the project policies: stock MonoGame effects, BepuPhysics2, Myra, xUnit, frustum culling
  only. Details in [README.md](README.md) and the design docs.
- Keep documentation where it belongs: README stays short, everything explanatory goes to
  `Design/`.
- If a task is ambiguous, ask the developer before writing a lot of code.

## Workflow

### 1. Get the next task

Open [TASKS.md](TASKS.md) and pick the **first unchecked task** (`- [ ]`). Do not skip ahead
unless the developer tells you to. If the task is blocked, note why in the task and either fix the
blocker or ask.

Read the task's `Reference:` pointers in the C++ project and the linked design docs before coding.

### 2. Do it

- Work on the smallest change that finishes the task. Keep unrelated refactors out.
- Match existing structure, naming and style (tabs, `var`, file-scoped namespaces, one partial
  `Assets` file per domain).
- Engine code goes in `OpenGothic.Common`, host/input code in `OpenGothic.Console`.
- Add or update tests as you go (see step 3).
- Do not leave dead code, `TODO` stubs you don't intend to finish, or debug prints behind.

### 3. Test thoroughly

This is not optional.

- `dotnet build OpenGothic.MonoGame.sln -c Release` must pass with no errors and no new warnings.
- `dotnet test OpenGothic.MonoGame.sln` must pass.
- New logic needs xUnit tests. If something cannot be unit-tested (e.g. rendering), say so
  explicitly and describe how you verified it manually.
- If the change affects the world viewer, **run it** against a real Gothic II installation and
  confirm the behaviour. Report what you observed.
- Check for regressions in the parts you touched, not just the happy path.

### 4. Update docs

- If the change alters architecture, behaviour, formats, controls or policy, update the matching
  file in `Design/`.
- If it adds a new subsystem, add or extend a design document and link it from `Design/README.md`.
- Keep README.md short; put explanations in `Design/`.
- Tick the task in TASKS.md only in step 6.

### 5. Ask the developer whether the task is finished

Summarise what changed, what you tested, what you could not test, and any known limitations.
Then **explicitly ask the developer to confirm the task is finished.** Do not continue to step 6
without an explicit confirmation.

If the developer asks for changes, make them and repeat steps 2–5.

### 6. Mark finished, tidy the roadmap, commit

Only after explicit confirmation:

1. Mark the task `- [x]` in [TASKS.md](TASKS.md) and move it to the **Completed** section with a
   one-line note (and the commit hash once known).
2. Review the whole of TASKS.md. You are free to reorganise the remaining tasks — add, remove,
   split, reorder, re-prioritise — as long as the result still follows the project's priorities
   (visible → active → parity). Explain non-trivial roadmap changes in your summary.
3. Commit and push:
   ```bash
   git add -A
   git commit -m "<short imperative summary> (T-xxx)"
   git push
   ```
   Commit message should name the task ID. Keep the working tree clean.
4. Report the commit hash and the next task.

## When to stop and ask

- The task requires a design decision not covered by the docs.
- Font/asset/format behaviour is unclear and the reference gives conflicting signals.
- The change would break an existing feature or require a large refactor.
- Tests cannot be written for a non-trivial piece of logic.
- You are about to add a shader, a new physics backend, or a new UI toolkit.
