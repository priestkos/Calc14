# CalculatorXIV

A calculator plugin for FINAL FANTASY XIV, built on Dalamud.

See [IDEA.md](IDEA.md) for the original goal.

## Layout

- `CalculatorXIVPlugin.cs`: plugin entry point.
- `CalculatorWindow.cs`: calculator UI.
- `CalculatorState.cs`: calculator state.
- `CalculatorXIV.Tests/`: unit tests for `CalculatorState`.
- `preview.html`: UI preview.

## Build

Requires the .NET SDK and a Dalamud development environment. Check `CalculatorXIV.csproj` before building.

## Tests

`CalculatorState` is covered by a standalone xUnit project (no Dalamud required):

```bash
dotnet test CalculatorXIV.Tests/CalculatorXIV.Tests.csproj
```
