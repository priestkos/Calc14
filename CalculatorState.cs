using System;
using System.Collections.Generic;
using System.Globalization;

namespace Calc14;

/// <summary>Calculator state machine: entry, pending operation, history, percentage handling.</summary>
public sealed class CalculatorState
{
    /// <summary>Pending binary operation kinds.</summary>
    public enum Op { None, Add, Sub, Mul, Div }

    private string entry = "0";
    private Op pendingOp = Op.None;
    private double accumulator;
    private bool freshEntry = true;   // next digit starts a new entry

    public string Entry => entry;
    public bool HasPending => pendingOp != Op.None;
    public double Accumulator => accumulator;
    public Op CurrentOp => pendingOp;

    /// <summary>Formats a numeric result for display and history (always invariant culture).</summary>
    public static string FormatResult(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) return "Error";
        return Math.Abs(d) >= 1e15 || (Math.Abs(d) < 1e-10 && d != 0)
            ? d.ToString("G9", CultureInfo.InvariantCulture)
            : d.ToString("0.############", CultureInfo.InvariantCulture);
    }

    private static string Format(double d) => FormatResult(d);

    private static bool IsErrorValue(double d) => double.IsNaN(d) || double.IsInfinity(d);

    /// <summary>
    /// Loads a display/history value as the current entry without keystroke replay.
    /// Rejects non-numeric strings (including "Error").
    /// </summary>
    public bool TrySetEntry(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "Error")
            return false;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return false;
        if (IsErrorValue(v))
            return false;

        ClearAll();
        entry = Format(v);
        freshEntry = true;
        return true;
    }

    public void Digit(char c)
    {
        if (c != '.' && (c < '0' || c > '9'))
            return;

        if (freshEntry || entry == "0" && !entry.Contains('.'))
        {
            entry = c == '.' ? "0." : c.ToString();
            freshEntry = false;
            return;
        }

        if (c == '.')
        {
            if (!entry.Contains('.')) entry += '.';
            return;
        }

        if (entry.Length < 17) entry += c;
    }

    public void Clear() // CE — clear current entry only
    {
        entry = "0";
        freshEntry = true;
    }

    public void ClearAll() // AC — full reset
    {
        entry = "0";
        accumulator = 0;
        pendingOp = Op.None;
        freshEntry = true;
    }

    public void Backspace()
    {
        if (freshEntry) return;
        entry = entry.Length <= 1 ? "0" : entry[..^1];
        if (entry is "" or "-") entry = "0";
    }

    public void Negate()
    {
        if (entry != "0") entry = entry.StartsWith('-') ? entry[1..] : "-" + entry;
    }

    /// <summary>Percent: "x%" as an operand = x/100; but right after +/-, it is x% of the accumulator.</summary>
    public void Percent()
    {
        if (!double.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        if (pendingOp is Op.Add or Op.Sub && !freshEntry)
        {
            entry = Format(accumulator * v / 100.0);
        }
        else
        {
            entry = Format(v / 100.0);
        }
    }

    /// <summary>Sets the pending operation and folds the previous one in (immediate execution).</summary>
    public string SetOp(Op op)
    {
        if (!double.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return entry;

        if (pendingOp != Op.None && !freshEntry)
            accumulator = Apply(pendingOp, accumulator, v);
        else if (!freshEntry || pendingOp == Op.None)
            accumulator = v;

        if (IsErrorValue(accumulator))
        {
            ClearAll();
            entry = "Error";
            return entry;
        }

        pendingOp = op;
        freshEntry = true;
        return Format(accumulator);
    }

    /// <summary>Equals: applies pending operation and returns the display string, or null on error.</summary>
    public string? Equals_()
    {
        if (!double.TryParse(entry, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return null;

        double result;
        if (pendingOp != Op.None)
            result = Apply(pendingOp, accumulator, v);
        else
            result = v;

        if (IsErrorValue(result))
        {
            ClearAll();
            entry = "Error";
            return null;
        }

        entry = Format(result);
        accumulator = 0;
        pendingOp = Op.None;
        freshEntry = true;
        return entry;
    }

    public static double Apply(Op op, double a, double b) => op switch
    {
        Op.Add => a + b,
        Op.Sub => a - b,
        Op.Mul => a * b,
        Op.Div => b == 0 ? double.NaN : a / b,
        _ => b,
    };

    // ---------- expression evaluation (for /calc <expr>) ----------
    private sealed record Tok(double Num, char Sym, int Prec, bool Left)
    {
        public Tok(char sym, int prec, bool left) : this(0, sym, prec, left) { }
    }

    private static bool TryTokenize(string s, out List<Tok> tokens)
    {
        tokens = new List<Tok>();
        int i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || c == '.')
            {
                int j = i;
                while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
                if (!double.TryParse(s[i..j], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return false;
                tokens.Add(new Tok(n, ' ', 0, true));
                i = j;
                continue;
            }
            var prec = c switch { '*' or 'x' or '/' => 2, '+' or '-' => 1, _ => 0 };
            if (prec == 0) return false;
            tokens.Add(new Tok(c, prec, true));
            i++;
        }
        return tokens.Count > 0;
    }

    /// <summary>Shunting-yard evaluation of a plain + - * / expression. Null on parse error or divide by zero.</summary>
    public static double? Evaluate(string expression)
    {
        if (!TryTokenize(expression, out var tokens)) return null;

        var output = new Queue<Tok>();
        var ops = new Stack<Tok>();
        var expectOperand = true;

        foreach (var t in tokens)
        {
            if (t.Num != 0 || t.Sym == ' ')
            {
                output.Enqueue(t);
                expectOperand = false;
                continue;
            }
            if (expectOperand) // unary minus
            {
                if (t.Sym == '-') { output.Enqueue(new Tok(-1, ' ', 0, true)); ops.Push(new Tok('*', 2, true)); continue; }
                if (t.Sym != '+') return null;
                continue;
            }
            while (ops.Count > 0 && (ops.Peek().Prec > t.Prec || (ops.Peek().Prec == t.Prec && t.Left)))
                output.Enqueue(ops.Pop());
            ops.Push(t);
            expectOperand = true;
        }
        if (expectOperand) return null;

        while (ops.Count > 0) output.Enqueue(ops.Pop());

        var st = new Stack<double>();
        foreach (var t in output)
        {
            if (t.Num != 0 || t.Sym == ' ') { st.Push(t.Num); continue; }
            if (st.Count < 2) return null;
            var b = st.Pop();
            var a = st.Pop();
            if (t.Sym == '/' && b == 0) return null;
            st.Push(Apply(t.Sym switch
            {
                '+' => Op.Add,
                '-' => Op.Sub,
                '*' or 'x' => Op.Mul,
                _ => Op.Div,
            }, a, b));
        }

        return st.Count == 1 ? st.Pop() : null;
    }
}
