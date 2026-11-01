# LPB contributor guidance

- Read the current `docs/architecture.md` and applicable approved specialized specifications before changing project structure or behavior. Surface material conflicts; a newer draft alone does not supersede an approved decision.
- Design with the intended full system in mind; implement only complexity justified by the current stage. Preserve useful simplified models and regression tests, and evolve contracts deliberately without speculative fields, interfaces, or dependencies.
- When implementing approved specialized decisions, update the architecture's summary, links, and superseded guidance. Keep detailed specifications in their workstream documents and distinguish current guidance from historical context.
- Stay within the milestone and changes authorized by the current task.
- M0 established infrastructure only; physical state, forces, and numerical simulation are introduced progressively in M1.
- Keep `Lpb.Physics` independent of Unity, rendering, persistence, the CLI, and external services.
- Keep references directed from CLI/tests to Physics, never the reverse.
- Use the exact .NET SDK selected by `global.json` on every development machine.
- Prefer ordinary C#/.NET conventions and small, maintainable changes.
- Add dependencies or abstractions only for active requirements.
- Preserve user changes, untracked files, and local IDE settings, including `.idea/`.
- Keep generated build/test artifacts and machine-specific paths out of source control.
- Use Git/GitHub between independent clones; do not sync active working trees through cloud folders.
- Update the README when developer commands, dependencies, or SDK requirements change.
- Report validation performed and limitations; do not claim CI passed before GitHub runs it.

Run from the repository root:

```sh
dotnet restore LPB.slnx --locked-mode
dotnet build LPB.slnx --configuration Release --no-restore
dotnet test LPB.slnx --configuration Release --no-build --no-restore -- RunConfiguration.TreatNoTestsAsError=true
dotnet run --project src/Lpb.Cli/Lpb.Cli.csproj --configuration Release --no-build --no-restore
```
