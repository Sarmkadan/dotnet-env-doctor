# dotnet-env-doctor

CLI tool + library: validates appsettings and environment variables against a schema, diffs environments, detects missing secrets. Target: net8.0.

## Commands

```bash
dotnet build                                   # build everything (warnings are errors)
dotnet test                                    # run xunit tests
dotnet test --filter "FullyQualifiedName~ConfigValidator"   # single test class
dotnet pack src/dotnet-env-doctor -c Release   # produce NuGet package
```

## Architecture

Pipeline: `IConfigSource` -> `ConfigSnapshot` (flat `Section:Key` dictionary) -> `IConfigValidator` / `IEnvironmentDiffer` / `ISecretDetector` -> findings.

- `src/dotnet-env-doctor/IConfigSchema.cs` - schema model (`ConfigKeySpec`, `ConfigValueKind`) and `IConfigSchemaLoader`
- `src/dotnet-env-doctor/IConfigSource.cs` - sources: `JsonFileConfigSource`, `EnvironmentConfigSource`
- `src/dotnet-env-doctor/IConfigValidator.cs` - `ConfigValidator`, `Finding`, `Severity`, `ValidationOptions`
- `src/dotnet-env-doctor/IEnvironmentDiffer.cs` - `EnvironmentDiffer`, `DiffResult`
- `src/dotnet-env-doctor/ISecretDetector.cs` - `SecretDetector`, `SecretIssue`
- `tests/dotnet-env-doctor.Tests/` - xunit tests, one file per production type

Key/merge semantics must match `Microsoft.Extensions.Configuration`: JSON nesting flattens to `A:B`, env vars map `A__B` -> `A:B`, keys are case-insensitive, later sources override earlier ones.

## Conventions

- `Directory.Build.props`: Nullable on, LangVersion latest, TreatWarningsAsErrors, analyzers on. Do not disable warnings per project; fix them.
- Public types have XML docs (`GenerateDocumentationFile` is on, missing docs fail the build).
- Immutable results: `record` types for `Finding`, `DiffEntry`, `SecretIssue`, snapshots.
- Never include secret values in findings, diff output or logs. Redact when the schema marks a key `Secret` or `ConnectionString`.
- Finding codes are stable (`ED001`, `ED002`...); document new codes in README before using them.
- Tests: xunit, `Method_Scenario_Expected` naming, no filesystem access except via temp dirs cleaned in `Dispose`.
- Commits: conventional commits (`feat:`, `fix:`, `test:`, `docs:`). No `Co-Authored-By` trailers.
