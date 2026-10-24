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

Using the library directly:

```csharp
using DotnetEnvDoctor;

IConfigSchema schema = await new JsonSchemaLoader().LoadAsync("envdoctor.schema.json");

var snapshot = await new JsonFileConfigSource("appsettings.Production.json").ReadAsync();
var result = new ConfigValidator().Validate(schema, snapshot);

foreach (var finding in result.Findings)
    Console.WriteLine($"{finding.Severity} {finding.Code} {finding.Key}: {finding.Message}");

if (!result.IsValid) Environment.Exit(1);
```

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
| `IConfigSource` (`JsonFileConfigSource`, `EnvironmentConfigSource`) | Reads a source into a flat `ConfigSnapshot` |
| `IConfigValidator` / `ValidationResult` / `Finding` | Validates a snapshot against a schema |
| `IEnvironmentDiffer` / `DiffResult` | Diffs two snapshots with secret redaction |
| `ISecretDetector` / `SecretIssue` | Finds missing, placeholder and leaked secrets |

## Finding codes

Validation findings carry a stable code (`FindingCodes`) that will not change once published.

| Code | Severity | Meaning |
|------|----------|---------|
| `ED001` | Error | A key declared as required is missing or has an empty value |
| `ED002` | Error | A value does not match the kind declared for its key (e.g. `Integer`, `Boolean`, `Uri`) |
| `ED003` | Error | A value does not match the regular expression declared for its key |
| `ED004` | Warning | A key is present in the configuration but not declared in the schema (promoted to Error with `TreatWarningsAsErrors`) |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
