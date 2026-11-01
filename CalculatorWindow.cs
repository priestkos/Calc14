using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Calc14;

public sealed class CalculatorWindow : Window, IDisposable
{
    private readonly CalculatorState calc = new();
    private readonly List<string> history = new();
    private string display = "0";
    private string subDisplay = string.Empty;
    private bool showHistory = true;

    public CalculatorWindow() : base("Calculator##Calc14")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                ImGuiWindowFlags.AlwaysAutoResize;
        Size = new Vector2(320, 0) * ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void AddHistory(string line)
    {
        history.Insert(0, line);
        if (history.Count > 100) history.RemoveAt(history.Count - 1);
    }

    private void RecordHistory(string expr, string result)
    {
        AddHistory($"{expr} = {result}");
    }

    private void ApplyOp(CalculatorState.Op op, char symbol)
    {
        var before = calc.Entry;
        var shown = calc.SetOp(op);
        if (shown == "Error")
        {
            display = "Error";
            subDisplay = string.Empty;
            return;
        }

        if (calc.HasPending || shown != before)
            subDisplay = $"{shown} {symbol}";
        display = shown;
    }

    private void Equals()
    {
        var expr = calc.HasPending
            ? $"{calc.Accumulator.ToString(CultureInfo.InvariantCulture)} {OpName(calc.CurrentOp)} {calc.Entry}"
            : calc.Entry;
        var result = calc.Equals_();
        if (result is null)
        {
            display = "Error";
            subDisplay = string.Empty;
            return;
        }
        if (expr != result) RecordHistory(expr, result);
        subDisplay = string.Empty;
        display = result;
    }

    private static string OpName(CalculatorState.Op op) => op switch
    {
        CalculatorState.Op.Add => "+",
        CalculatorState.Op.Sub => "\u2212",
        CalculatorState.Op.Mul => "\u00d7",
        CalculatorState.Op.Div => "\u00f7",
        _ => "",
    };

    public override void Draw()
    {
        // ---- display ----
        if (subDisplay.Length > 0)
            ImGui.TextUnformatted(subDisplay);
        else
            ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight()));

        // right-aligned result, auto-shrink font if too long
        var scale = display.Length > 14 ? 0.8f : display.Length > 9 ? 1.2f : 1.6f;
        ImGui.SetWindowFontScale(scale);
        var w = ImGui.GetContentRegionAvail().X;
        var tw = ImGui.CalcTextSize(display).X;
        ImGui.SetCursorPosX(MathF.Max(0, w - tw));
        ImGui.TextUnformatted(display);
        ImGui.SetWindowFontScale(1f);

        ImGui.Spacing();

        // ---- button grid ----
        var bs = 60 * ImGuiHelpers.GlobalScale; // button size

        void Key(string label, Action action)
        {
            if (ImGui.Button(label, new Vector2(bs, bs))) action();
        }

        // Row 1: AC, CE, ⌫, ÷
        Key("AC", () => { calc.ClearAll(); display = "0"; subDisplay = string.Empty; });
        ImGui.SameLine();
        // CE clears the entry only; keep subDisplay so the pending op remains visible.
        Key("CE", () => { calc.Clear(); display = "0"; });
        ImGui.SameLine();
        Key("\u232b", () => { calc.Backspace(); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u00f7", () => ApplyOp(CalculatorState.Op.Div, '\u00f7'));

        // Row 2: 7 8 9 ×
        Key("7", () => { calc.Digit('7'); display = calc.Entry; });
        ImGui.SameLine();
        Key("8", () => { calc.Digit('8'); display = calc.Entry; });
        ImGui.SameLine();
        Key("9", () => { calc.Digit('9'); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u00d7", () => ApplyOp(CalculatorState.Op.Mul, '\u00d7'));

        // Row 3: 4 5 6 −
        Key("4", () => { calc.Digit('4'); display = calc.Entry; });
        ImGui.SameLine();
        Key("5", () => { calc.Digit('5'); display = calc.Entry; });
        ImGui.SameLine();
        Key("6", () => { calc.Digit('6'); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u2212", () => ApplyOp(CalculatorState.Op.Sub, '\u2212'));

        // Row 4: 1 2 3 +
        Key("1", () => { calc.Digit('1'); display = calc.Entry; });
        ImGui.SameLine();
        Key("2", () => { calc.Digit('2'); display = calc.Entry; });
        ImGui.SameLine();
        Key("3", () => { calc.Digit('3'); display = calc.Entry; });
        ImGui.SameLine();
        Key("+", () => ApplyOp(CalculatorState.Op.Add, '+'));

        // Row 5: %, 0, ., =
        Key("%", () => { calc.Percent(); display = calc.Entry; });
        ImGui.SameLine();
        Key("0", () => { calc.Digit('0'); display = calc.Entry; });
        ImGui.SameLine();
        Key(".", () => { calc.Digit('.'); display = calc.Entry; });
        ImGui.SameLine();
        Key("=", Equals);

        // ---- history ----
        ImGui.Spacing();
        ImGui.Checkbox("History", ref showHistory);

        if (showHistory)
        {
            using (var _ = ImRaii.Child("##history", new Vector2(0, 130 * ImGuiHelpers.GlobalScale), true))
            {
                if (history.Count == 0)
                {
                    ImGui.TextDisabled("No calculations yet.");
                }
                else
                {
                    for (var i = 0; i < history.Count; i++)
                    {
                        if (ImGui.Selectable($"{history[i]}##hist{i}"))
                        {
                            // clicking a result loads it as the current entry
                            var idx = history[i].LastIndexOf(" = ", StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                var val = history[i][(idx + 3)..];
                                LoadEntry(val);
                            }
                        }
                        if (i > 0) ImGui.Separator();
                    }
                }
            }

            if (ImGui.Button("Clear History"))
                history.Clear();
        }
    }

    private void LoadEntry(string val)
    {
        if (!calc.TrySetEntry(val))
            return;
        display = calc.Entry;
    }

    public void Dispose() { }
}
