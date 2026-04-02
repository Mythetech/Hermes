# Contributing to Hermes

Thank you for your interest in contributing to Hermes! This document provides guidelines and information for contributors.

## Reporting Issues

- Use [GitHub Issues](https://github.com/Mythetech/Hermes/issues) to report bugs or request features
- Use the provided issue templates when available
- Include your platform (Windows, macOS, Linux), .NET version, and Hermes version
- For bugs, include steps to reproduce, expected behavior, and actual behavior

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Platform-specific requirements:
  - **Windows**: No additional dependencies (uses WebView2, auto-installed)
  - **macOS**: Xcode command line tools (`xcode-select --install`)
  - **Linux**: GTK3 and WebKitGTK development libraries

### Building

```bash
# Clone the repository
git clone https://github.com/Mythetech/Hermes.git
cd Hermes

# Build the solution
dotnet build

# Run tests
dotnet test
```

### macOS Native Library

The macOS native library must be built separately:

```bash
cd src/Hermes.Native.macOS
make
```

### Linux Native Library

The Linux native library must be built separately:

```bash
cd src/Hermes.Native.Linux
make
```

## Pull Requests

1. Fork the repository and create your branch from `main`
2. Write clear, descriptive commit messages
3. Add tests for new functionality
4. Ensure all tests pass on your platform (`dotnet test`)
5. Update documentation if your change affects public APIs
6. Submit a pull request with a clear description of the change

## Code Style

- Follow existing code conventions in the project
- Use `file`-scoped namespaces
- Prefer modern C# features (.NET 10 target)
- Keep native code minimal, preferring managed C# solutions where possible
- Ensure AOT compatibility for new public APIs

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for an overview of the project structure and platform strategy.

## License

By contributing to Hermes, you agree that your contributions will be licensed under the [Elastic License 2.0](LICENSE).
