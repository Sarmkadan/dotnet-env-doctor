# dotnet-env-doctor

[![CI](https://github.com/Sarmkadan/dotnet-env-doctor/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/dotnet-env-doctor/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DotnetEnvDoctor.svg)](https://www.nuget.org/packages/DotnetEnvDoctor)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

CLI tool that validates `appsettings*.json` and environment variables against a schema, diffs environments, and detects missing or leaked secrets.

Configuration bugs are found in production because nobody checks that `appsettings.Production.json` actually declares `ConnectionStrings:Default`, or that the `SMTP__PASSWORD` variable is set on the new host. `dotnet-env-doctor` makes those checks a build step.

## Features

- **Schema validation** - declare expected keys, types, required flags and regex patterns in `envdoctor.schema.json`; fail the build when they are missing or malformed.
- **Environment diff** - compare `appsettings.Staging.json` against `appsettings.Production.json` (or a live process environment) key by key, with secrets redacted.
- **Secret detection** - find secrets that are missing, still set to `CHANGEME`, or committed to a JSON file that should never contain them.
- **Same merge semantics as ASP.NET Core** - JSON sections flatten to `Section:Key`, environment variables use `Section__Key`, later sources override earlier ones.
- **CI-friendly** - stable finding codes (`ED001`...), non-zero exit codes, JSON output.

## Quick start

```bash
dotnet tool install -g DotnetEnvDoctor

# validate a project's configuration against its schema
env-doctor validate --schema envdoctor.schema.json \
  --json appsettings.json --json appsettings.Production.json --env

# diff two environments
env-doctor diff appsettings.Staging.json appsettings.Production.json

# hunt for missing / leaked secrets
env-doctor secrets --schema envdoctor.schema.json --json appsettings.json --env
```

## Library usage

The CLI is a thin layer over the library. Each example below is a complete top-level program that compiles against `DotnetEnvDoctor` (net8.0) and needs `using DotnetEnvDoctor;`.

Sources are read in override order: the first source is the lowest priority and the last one wins, as in `Microsoft.Extensions.Configuration`.

### Validate

Merge several sources, validate the merged view against the schema and fail on warnings too:

```csharp
using DotnetEnvDoctor;

var schema = await new JsonSchemaLoader().LoadAsync("envdoctor.schema.json");

var merged = SnapshotMerger.Merge(
    await new JsonFileConfigSource("appsettings.json").ReadAsync(),
    await new JsonFileConfigSource("appsettings.Production.json").ReadAsync(),
    await new EnvironmentConfigSource("MYAPP_").ReadAsync());

var options = new ValidationOptions { TreatWarningsAsErrors = true };
var result = new ConfigValidator().Validate(schema, merged.Snapshot, options);

foreach (var finding in result.Findings)
    Console.WriteLine($"{finding.Severity} {finding.Code} {finding.Key}: {finding.Message}");

// Process exit code: 0 when there are no Error findings, 1 otherwise.
return EnvDoctorRunner.ToExitCode(result);
```

`Finding.Source` holds the merged name of all sources. To find the file that supplied a key, look it up in `merged.Origins`.

### Diff

Compare two environments. Passing the schema masks `Secret` and `ConnectionString` values as `***`:

```csharp
using DotnetEnvDoctor;

var schema = await new JsonSchemaLoader().LoadAsync("envdoctor.schema.json");

var staging = await new JsonFileConfigSource("appsettings.Staging.json").ReadAsync();
var production = await new JsonFileConfigSource("appsettings.Production.json").ReadAsync();

var diff = new EnvironmentDiffer().Diff(staging, production, schema);

Console.WriteLine($"{diff.LeftName} vs {diff.RightName}");

foreach (var entry in diff.Entries)
{
    var line = entry.Kind switch
    {
        DiffKind.OnlyInLeft => $"- {entry.Key} = {entry.LeftValue}",
        DiffKind.OnlyInRight => $"+ {entry.Key} = {entry.RightValue}",
        DiffKind.Changed => $"~ {entry.Key}: {entry.LeftValue} -> {entry.RightValue}",
        _ => entry.Kind.ToString(),
    };

    Console.WriteLine(line);
}
```

### Secrets

Scan for missing, placeholder and leaked secrets. Assigning a list property replaces its default, so copy any default entries you want to keep. `.env` is not in the default `CommittedFilePatterns`, so the pattern is added here on purpose:

```csharp
using DotnetEnvDoctor;

var schema = await new JsonSchemaLoader().LoadAsync("envdoctor.schema.json");

var secretOptions = new SecretDetectorOptions
{
    PlaceholderValues = new List<string> { "CHANGEME", "REPLACE_ME", "your-api-key" },
    CommittedFilePatterns = new List<string> { "appsettings.json", "appsettings.*.json", "*.env", "*.env.example" },
};

var detector = new SecretDetector(secretOptions);

var issues = detector.Scan(schema, new List<ConfigSnapshot>
{
    await new JsonFileConfigSource("appsettings.json").ReadAsync(),
    await new DotEnvFileConfigSource(".env").ReadAsync(),
    await new EnvironmentConfigSource("MYAPP_").ReadAsync(),
});

foreach (var issue in issues)
{
    var label = issue.Kind switch
    {
        SecretIssueKind.MissingSecret => "missing",
        SecretIssueKind.PlaceholderValue => "placeholder",
        SecretIssueKind.SecretInCommittedFile => "committed",
        SecretIssueKind.UndeclaredSecretLike => "undeclared",
        _ => issue.Kind.ToString(),
    };

    Console.WriteLine($"[{label}] {issue.Key} ({issue.Source ?? "no source"}): {issue.Message}");
}

// Exit code: 0 for no issues, 2 when every issue is UndeclaredSecretLike (warning), 1 otherwise.
return EnvDoctorRunner.ToExitCode(issues);
```

### Dependency injection

`AddEnvDoctor` registers the services as singletons and binds `EnvDoctorOptions`. `BuildServiceProvider` comes from the `Microsoft.Extensions.DependencyInjection` package, which the library does not reference. Console apps need to add it (`dotnet add package Microsoft.Extensions.DependencyInjection`). ASP.NET Core apps already include it.

```csharp
using DotnetEnvDoctor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var services = new ServiceCollection();
services.AddEnvDoctor(o => o.SchemaPath = "envdoctor.schema.json");

using var provider = services.BuildServiceProvider();

var runner = provider.GetRequiredService<EnvDoctorRunner>();
var schemaPath = provider.GetRequiredService<IOptions<EnvDoctorOptions>>().Value.SchemaPath
    ?? throw new InvalidOperationException("SchemaPath is not configured.");

var result = await runner.ValidateAsync(schemaPath, new IConfigSource[]
{
    new JsonFileConfigSource("appsettings.json"),
    new EnvironmentConfigSource("MYAPP_"),
});

return EnvDoctorRunner.ToExitCode(result);
```

`EnvDoctorRunner` does not read `SchemaPath` itself. Pass the path to each call.

### JSON output for CI

`JsonReportWriter.Write` returns a JSON document that starts with `"schemaVersion"`. Its shape is stable across releases, so CI scripts can parse it:

```csharp
using DotnetEnvDoctor;

var schema = await new JsonSchemaLoader().LoadAsync("envdoctor.schema.json");

var merged = SnapshotMerger.Merge(
    await new JsonFileConfigSource("appsettings.json").ReadAsync(),
    await new EnvironmentConfigSource("MYAPP_").ReadAsync());

var result = new ConfigValidator().Validate(schema, merged.Snapshot);

// Writes { "schemaVersion", "isValid", "findings": [...] } to stdout. Pass indented: false for compact output.
Console.WriteLine(JsonReportWriter.Write(result));

return EnvDoctorRunner.ToExitCode(result);
```

`JsonReportWriter.Write` also has overloads for `DiffResult` and for the secret issues list.

## Secret handling

dotnet-env-doctor is built so that secret values do not leave the process through its output:

- **Findings never contain values.** Finding messages are fixed text. `ED002` says what kind was expected, and `ED003` names the schema pattern. Neither echoes the value.
- **Secret issues never contain values.** `SecretIssue.Message` is fixed text, and `Source` is a source name.
- **Diffs mask `Secret` and `ConnectionString` values as `***`.** Redaction happens only when you pass the schema to `EnvironmentDiffer.Diff` or `EnvDoctorRunner.DiffAsync`. Without a schema nothing is masked. Keys the schema does not declare are never masked. Null and empty values are left as they are.
- **JSON diff output inherits that redaction.** `JsonReportWriter.Write(DiffResult)` writes the values the differ returned, so it is masked only when the diff was built with a schema.
- **`.env` is not treated as committed by default.** `SecretDetectorOptions.CommittedFilePatterns` defaults to `appsettings.json`, `appsettings.*.json` and `*.env.example`. A real secret in a `.env` file is not reported as `SecretInCommittedFile`. Add `*.env` to the list if your team commits `.env` files.

## Schema file format

`JsonSchemaLoader` reads `envdoctor.schema.json`. The root object holds a `keys` array with one entry per configuration key. The example below covers every `ConfigValueKind`:

```json
{
  "keys": [
    {
      "key": "App:Name",
      "kind": "String",
      "required": true,
      "description": "Display name shown on the health page"
    },
    {
      "key": "App:MaxItems",
      "kind": "Integer",
      "required": true,
      "pattern": "^[1-9][0-9]*$",
      "description": "Page size for list endpoints"
    },
    {
      "key": "Features:EnableBeta",
      "kind": "Boolean",
      "required": false,
      "description": "Turns on the beta endpoints"
    },
    {
      "key": "Services:Payments:BaseUrl",
      "kind": "Uri",
      "required": true,
      "pattern": "^https://",
      "description": "Base address of the payments API"
    },
    {
      "key": "ConnectionStrings:Default",
      "kind": "ConnectionString",
      "required": true,
      "pattern": "^Server=",
      "description": "Primary database"
    },
    {
      "key": "Smtp:Password",
      "kind": "Secret",
      "required": true,
      "description": "Injected from the environment, never committed"
    }
  ]
}
```

The example uses six keys because each key has exactly one `kind`.

Properties of each entry:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `key` | string | none (required) | Colon-separated configuration path, e.g. `ConnectionStrings:Default`. Must be non-empty and unique, ignoring case |
| `kind` | string | `String` | One of the `ConfigValueKind` names. Matched case-insensitively. Unknown names fail loading |
| `required` | boolean | `true` | When `true`, a missing or whitespace-only value raises `ED001`. `null` means the default |
| `pattern` | string | none | Regular expression the value must match (.NET syntax). Invalid patterns fail loading |
| `description` | string | none | Human-readable purpose of the key. Not used during validation |

Unknown properties are ignored. A wrong JSON type for a property (for example `"required": "yes"`) fails loading with a `FormatException`.

Value kinds:

| Kind | How the value is checked |
|------|--------------------------|
| `String` | Any non-empty value is accepted. |
| `Integer` | Must parse as a 64-bit integer with invariant culture (optional sign, no thousands separators). |
| `Boolean` | Must be `true` or `false`. Matching ignores case. Other values raise `ED002`. |
| `Uri` | Must be an absolute URI with both a scheme and a host, such as `https://api.example.com`. |
| `ConnectionString` | Any non-empty value is accepted. The value is treated as a secret. |
| `Secret` | Any non-empty value is accepted. The value is treated as a secret and must come from the environment or a vault. |

Secrets are never echoed. Findings never include a value, and diffs show `***` in place of a `Secret` or `ConnectionString` value.

Patterns are checked only after the kind check passes. A value that fails its kind check reports `ED002` only.

## Installation

As a global tool:

```bash
dotnet tool install -g DotnetEnvDoctor
```

As a library:

```bash
dotnet add package DotnetEnvDoctor
```

## API overview

| Type | Purpose |
|------|---------|
| `IConfigSchema` / `ConfigKeySpec` | Declares expected keys, value kinds, required flags and patterns |
| `IConfigSchemaLoader` | Loads a schema from JSON/YAML |
| `IConfigSource` (`JsonFileConfigSource`, `EnvironmentConfigSource`, `DotEnvFileConfigSource`) | Reads a source into a flat `ConfigSnapshot` |
| `SnapshotMerger` / `MergedSnapshot` | Merges snapshots with `Microsoft.Extensions.Configuration` override rules and records the origin of each key |
| `IConfigValidator` / `ValidationResult` / `Finding` / `ValidationOptions` | Validates a snapshot against a schema |
| `IEnvironmentDiffer` / `DiffResult` / `DiffEntry` | Diffs two snapshots with secret redaction |
| `SecretRedactor` | Masks `Secret` and `ConnectionString` values as `***` |
| `ISecretDetector` / `SecretDetector` / `SecretDetectorOptions` / `SecretIssue` | Finds missing, placeholder and leaked secrets |
| `SecretHeuristics` | Placeholder, entropy and known-token checks, plus the glob matching used for committed files |
| `JsonReportWriter` | Serialises validation, diff and secret results as JSON for CI |
| `EnvDoctorRunner` | Facade that loads a schema, reads sources and runs validation, diff and secret scans, with exit-code helpers |
| `ServiceCollectionExtensions` (`AddEnvDoctor`) | Registers the services with dependency injection and binds `EnvDoctorOptions` |

## Finding codes

Validation findings carry a stable code (`FindingCodes`) that will not change once published.

| Code | Default severity | Raised when |
|------|------------------|-------------|
| `ED001` | Error | A required key is missing, or its value is empty or whitespace |
| `ED002` | Error | The value does not match the `kind` declared for its key |
| `ED003` | Error | The value does not match the `pattern` declared for its key |
| `ED004` | Warning | The key is present in the configuration but not declared in the schema |
| `ED005` | Error | The pattern cannot be compiled, or matching took longer than one second |

`ED005` cannot come from a schema file that loads. `JsonSchemaLoader` rejects invalid patterns with a `FormatException`, so `ED005` only appears for schemas built in code or for a match timeout.

`ValidationOptions` changes only `ED004`. `ED001`, `ED002`, `ED003` and `ED005` are always errors.

- `ReportUndeclaredKeys` (default `true`): set it to `false` to skip `ED004` entirely.
- `TreatWarningsAsErrors` (default `false`): set it to `true` to promote `ED004` to an Error. The result is then invalid.

```csharp
var options = new ValidationOptions { ReportUndeclaredKeys = false };        // no ED004 findings
var strict = new ValidationOptions { TreatWarningsAsErrors = true };         // ED004 fails the run
```

## Exit codes

`EnvDoctorRunner` maps results to process exit codes.

Validation, from `EnvDoctorRunner.ToExitCode(ValidationResult)`:

| Exit code | Meaning |
|-----------|---------|
| `0` | No Error findings. Warnings such as `ED004` do not fail the run. |
| `1` | At least one Error finding. |

Secret scan, from `EnvDoctorRunner.ToExitCode(IReadOnlyList<SecretIssue>)`:

| Exit code | Meaning |
|-----------|---------|
| `0` | No issues. |
| `2` | Only `UndeclaredSecretLike` issues, which are warning-level. |
| `1` | Any other issue. |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
