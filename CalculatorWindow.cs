using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CalculatorXIV;

public sealed class CalculatorWindow : Window, IDisposable
{
    private readonly CalculatorState calc = new();
    private readonly List<string> history = new();
    private string display = "0";
    private string subDisplay = string.Empty;
    private bool showHistory = true;

    // Theme: warm ember palette
    private static readonly Vector4 Panel = new(0.14f, 0.08f, 0.06f, 0.95f);
    private static readonly Vector4 AccentOrange = new(0.95f, 0.45f, 0.10f, 1f);
    private static readonly Vector4 AccentOrangeHover = new(1f, 0.55f, 0.20f, 1f);
    private static readonly Vector4 AccentRed = new(0.72f, 0.16f, 0.12f, 1f);
    private static readonly Vector4 AccentRedHover = new(0.86f, 0.24f, 0.18f, 1f);
    private static readonly Vector4 KeyPad = new(0.20f, 0.13f, 0.10f, 1f);
    private static readonly Vector4 KeyPadHover = new(0.28f, 0.18f, 0.13f, 1f);
    private static readonly Vector4 TextWarm = new(1f, 0.92f, 0.85f, 1f);
    private static readonly Vector4 TextDim = new(0.75f, 0.62f, 0.52f, 1f);

    public CalculatorWindow() : base("Calculator##CalculatorXIV")
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
        // Keep style pushes alive for the whole frame (not just DrawTheme()).
        using var pad = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(10, 10) * ImGuiHelpers.GlobalScale);
        using var round = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 8 * ImGuiHelpers.GlobalScale);
        using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0);

        // ---- display ----
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Panel))
        using (var _ = ImRaii.Child("##display", new Vector2(0, 74 * ImGuiHelpers.GlobalScale), false))
        {
            if (subDisplay.Length > 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, TextDim))
                    ImGui.TextUnformatted(subDisplay);
            }
            else
            {
                ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight()));
            }

            // right-aligned big result, auto-shrink font if too long
            var scale = display.Length > 14 ? 0.8f : display.Length > 9 ? 1.2f : 1.6f;
            ImGui.SetWindowFontScale(scale);
            using (ImRaii.PushColor(ImGuiCol.Text, TextWarm))
            {
                var w = ImGui.GetContentRegionAvail().X;
                var tw = ImGui.CalcTextSize(display).X;
                ImGui.SetCursorPosX(MathF.Max(0, w - tw));
                ImGui.TextUnformatted(display);
            }
            ImGui.SetWindowFontScale(1f);
        }

        ImGui.Spacing();

        // ---- button grid ----
        var bs = 60 * ImGuiHelpers.GlobalScale; // button size

        void Key(string label, Vector4 color, Vector4 hover, Action action)
        {
            using var c = ImRaii.PushColor(ImGuiCol.Button, color);
            using var h = ImRaii.PushColor(ImGuiCol.ButtonHovered, hover);
            using var p = ImRaii.PushColor(ImGuiCol.ButtonActive, hover);
            using var t = ImRaii.PushColor(ImGuiCol.Text, TextWarm);
            if (ImGui.Button(label, new Vector2(bs, bs)) && action != null) action();
        }

        // Row 1: AC, CE, ⌫, ÷
        Key("AC", AccentRed, AccentRedHover, () => { calc.ClearAll(); display = "0"; subDisplay = string.Empty; });
        ImGui.SameLine();
        // CE clears the entry only; keep subDisplay so the pending op remains visible.
        Key("CE", AccentRed, AccentRedHover, () => { calc.Clear(); display = "0"; });
        ImGui.SameLine();
        Key("\u232b", KeyPad, KeyPadHover, () => { calc.Backspace(); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u00f7", AccentOrange, AccentOrangeHover, () => ApplyOp(CalculatorState.Op.Div, '\u00f7'));

        // Row 2: 7 8 9 ×
        Key("7", KeyPad, KeyPadHover, () => { calc.Digit('7'); display = calc.Entry; });
        ImGui.SameLine();
        Key("8", KeyPad, KeyPadHover, () => { calc.Digit('8'); display = calc.Entry; });
        ImGui.SameLine();
        Key("9", KeyPad, KeyPadHover, () => { calc.Digit('9'); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u00d7", AccentOrange, AccentOrangeHover, () => ApplyOp(CalculatorState.Op.Mul, '\u00d7'));

        // Row 3: 4 5 6 −
        Key("4", KeyPad, KeyPadHover, () => { calc.Digit('4'); display = calc.Entry; });
        ImGui.SameLine();
        Key("5", KeyPad, KeyPadHover, () => { calc.Digit('5'); display = calc.Entry; });
        ImGui.SameLine();
        Key("6", KeyPad, KeyPadHover, () => { calc.Digit('6'); display = calc.Entry; });
        ImGui.SameLine();
        Key("\u2212", AccentOrange, AccentOrangeHover, () => ApplyOp(CalculatorState.Op.Sub, '\u2212'));

        // Row 4: 1 2 3 +
        Key("1", KeyPad, KeyPadHover, () => { calc.Digit('1'); display = calc.Entry; });
        ImGui.SameLine();
        Key("2", KeyPad, KeyPadHover, () => { calc.Digit('2'); display = calc.Entry; });
        ImGui.SameLine();
        Key("3", KeyPad, KeyPadHover, () => { calc.Digit('3'); display = calc.Entry; });
        ImGui.SameLine();
        Key("+", AccentOrange, AccentOrangeHover, () => ApplyOp(CalculatorState.Op.Add, '+'));

        // Row 5: %, 0, ., =
        Key("%", KeyPad, KeyPadHover, () => { calc.Percent(); display = calc.Entry; });
        ImGui.SameLine();
        Key("0", KeyPad, KeyPadHover, () => { calc.Digit('0'); display = calc.Entry; });
        ImGui.SameLine();
        Key(".", KeyPad, KeyPadHover, () => { calc.Digit('.'); display = calc.Entry; });
        ImGui.SameLine();
        Key("=", AccentOrange, AccentOrangeHover, Equals);

        // ---- history ----
        ImGui.Spacing();
        ImGui.Checkbox("History", ref showHistory);

        if (showHistory)
        {
            using (ImRaii.PushColor(ImGuiCol.ChildBg, Panel))
            using (var _ = ImRaii.Child("##history", new Vector2(0, 130 * ImGuiHelpers.GlobalScale), true))
            {
                if (history.Count == 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, TextDim))
                        ImGui.TextUnformatted("No calculations yet.");
                }
                else
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, TextWarm))
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
