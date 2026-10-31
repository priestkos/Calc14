# Calc14

A keypad calculator that opens over FFXIV, with a 100-entry history and inline expression evaluation from chat.

`/calc` · Dalamud API 15 · .NET 10

## What it does

- **Calculator window** — five rows of 60 px keys (AC, CE, backspace, four operators, `%`, `.`, `=`), a right-aligned result that shrinks its font as it grows, and a collapsible history pane; auto-resizing, 320 px wide.
- **History** — the last 100 calculations, newest first. Click a line to load its result; `Clear History` empties the log.
- **Inline math** — `/calc 1+2*3` evaluates without the window and writes to log and history.

## How it works

- Digits append up to 17 characters; a second `.` is ignored and non-digits are dropped.
- `%` divides by 100, except straight after `+`/`−`, where it is that percent of the accumulator (`200 + 10%` → `220`).
- Operators execute immediately, left to right — no keypad precedence. Division by zero shows `Error`, resets, and the next digit starts fresh.
- Results are invariant-culture: `G9` above 1e15 or below 1e-10, else up to 12 decimals; NaN and infinity become `Error`.
- `/calc <expr>` is shunting-yard over `+ - * /` (`x` also multiplies), with precedence and unary minus, no parentheses; errors are logged and add nothing.

## Commands

| Command | Effect |
| --- | --- |
| `/calc` | Toggle the window (also the installer's main UI button) |
| `/calc <expr>` | Evaluate and log an expression; the result is added to history |

## Build

Requires the .NET 10 SDK and the Dalamud dev distribution (`%APPDATA%\XIVLauncher\addon\Hooks\dev`, or `DALAMUD_HOME`).

```
dotnet build -c Debug      # load bin/Debug/Calc14.dll as a dev plugin
dotnet build -c Release    # zip in bin/Release/Calc14/latest.zip
```

## Install (dev plugin)

1. `/xlsettings` → Experimental → Dev Plugin Locations → add `bin\Debug\Calc14.dll`.
2. `/xlplugins` → Dev Tools → enable **Calc14**.
3. `/calc` opens the window; there is no settings window.

## Tests

`CalculatorState` is a plain class, so the xUnit project runs offline without Dalamud: shunting-yard precedence and unary minus, rejected expressions, percent of the accumulator, the divide-by-zero reset, invariant formatting round-tripping through `TrySetEntry`, and operator chaining.

```
dotnet test Calc14.Tests/Calc14.Tests.csproj    # 18 cases
```

## Limits

- `%` is accumulator-relative only after `+`/`−`; after `×`/`÷` it is a plain entry/100 operand.
- `/calc <expr>` has no parentheses, exponents or functions, and prints to the log — never to the keypad display.
- Not verified in game: behaviour is taken from the code and the offline tests, not from a live client.

## Layout

```
Calc14Plugin.cs     plugin entry point, `/calc` handler and window wiring
CalculatorWindow.cs        ImGui keypad, display, history list
CalculatorState.cs         entry/pending-op state, formatting, shunting-yard evaluator
Calc14.Tests/       xUnit tests for `CalculatorState`
```
