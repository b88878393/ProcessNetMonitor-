# Contributing to ProcessNetMonitor

Thanks for helping improve ProcessNetMonitor. This guide keeps contributions consistent and reviewable.

## How to contribute

1. **Fork** [b88878393/ProcessNetMonitor-](https://github.com/b88878393/ProcessNetMonitor-)
2. **Clone** your fork and create a branch from `main`:
   ```bash
   git checkout -b feature/short-description
   ```
3. Make focused changes (one concern per PR when possible)
4. Run tests:
   ```bash
   dotnet test ProcessNetMonitor.Tests/ProcessNetMonitor.Tests.csproj
   ```
5. **Commit** with a clear message (what / why)
6. **Push** and open a **Pull Request** against `main`

## Code style

- Prefer the existing **MVVM** layout: `Models/`, `ViewModels/`, `Services/`, `Native/`
- Put pure, platform-independent logic in **`ProcessNetMonitor.Core`** and cover it with unit tests
- Keep Windows-specific APIs (P/Invoke, WMI, ETW) in `ProcessNetMonitor` services
- Match naming and formatting already used in the project (C# conventions, nullable enabled)
- Avoid committing build outputs, binaries, or personal secrets

## Issues

- Use the **Bug report** or **Feature request** templates under `.github/ISSUE_TEMPLATE`
- For bugs: include Windows version, whether you ran as Administrator, and steps to reproduce

## Scope tips

- Security-sensitive process/network features need clear UX warnings (especially kill / critical processes)
- Do not bundle Npcap or other kernel drivers; detect optional dependencies at runtime
- Prefer small, reviewable PRs over large refactors unless discussed in an issue first

## License

By contributing, you agree that your contributions are licensed under the project’s [MIT License](LICENSE).
