# CalculatorXIV

A calculator plugin for FINAL FANTASY XIV, built on Dalamud.

See [IDEA.md](IDEA.md) for the original goal.

## Simulated screenshots

These are HTML mockups of the ImGui window (same warm ember palette and layout as `CalculatorWindow.cs`). Open [`preview.html`](preview.html) for an interactive gallery.

| Result + history | Mid-operation | Compact |
| --- | --- | --- |
| ![Result with history](screenshots/calc_result_history.png) | ![Pending division](screenshots/calc_mid_operation.png) | ![History hidden](screenshots/calc_compact.png) |

**In-game overlay mock** (how the floating Dalamud window might sit over the HUD):

![In-game overlay](screenshots/calc_ingame_overlay.png)

## Layout

- `CalculatorXIVPlugin.cs`: plugin entry point.
- `CalculatorWindow.cs`: calculator UI.
- `CalculatorState.cs`: calculator state.
- `CalculatorXIV.Tests/`: unit tests for `CalculatorState`.
- `preview.html`: UI preview (modes via `?mode=result|mid|compact|overlay|gallery`).
- `screenshots/`: simulated PNGs rendered from the preview.

## Build

Requires the .NET SDK and a Dalamud development environment. Check `CalculatorXIV.csproj` before building.

## Tests

`CalculatorState` is covered by a standalone xUnit project (no Dalamud required):

```bash
dotnet test CalculatorXIV.Tests/CalculatorXIV.Tests.csproj
```
