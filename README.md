# OpenAI Analyzers

[![Build, test and publish dotnet](https://github.com/BinkyLabs/openai-analyzers/actions/workflows/dotnet.yml/badge.svg)](https://github.com/BinkyLabs/openai-analyzers/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/BinkyLabs.OpenAI.Analyzers.svg)](https://www.nuget.org/packages/BinkyLabs.OpenAI.Analyzers/)

A collection of Roslyn analyzers for detecting security and best practice issues when using the OpenAI .NET SDK.

## Analyzers

| Code | Title | Severity | Status |
| ---- | ----- | -------- | ------ |
| [BOA001](./rules/BOA001.md) | Avoid inputs in **SystemChatMessage** | ⚠️ | ✔️ |
| [BOA002](./rules/BOA002.md) | A **SystemChatMessage** should be last | ⚠️ |  ❌ |

## Installation

### Via the command line

```shell
dotnet add package BinkyLabs.OpenAI.Analyzers --prerelease
```

### In CSProj file

```xml
<!-- x-release-please-start-version -->
<PackageReference Include="BinkyLabs.OpenAI.Analyzers" Version="1.0.0-preview.2" PrivateAssets="all" />
<!-- x-release-please-end -->
```

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines.

## License

See [LICENSE](LICENSE) for license information.
