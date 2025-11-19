# OpenAI Analyzers

A collection of Roslyn analyzers for detecting security and best practice issues when using the OpenAI .NET SDK.

## Analyzers

| Code | Title | Severity | Status |
| ---- | ----- | -------- | ------ |
| [BOA001](./rules/BOA001.md) | Avoid inputs in SystemChatMessage | ⚠️ | ✔️ |
| [BOA002](./rules/BOA002.md) | A `SystemChatMessage` should be last | ⚠️ |  ❌ |

## Installation

### In CSProj file

```xml
<PackageReference Include="BinkyLabs.OpenAI.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

### Via the command line

```shell
dotnet add package BinkyLabs.OpenAI.Analyzers --prerelease
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
