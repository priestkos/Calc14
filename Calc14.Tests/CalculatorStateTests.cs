using System.Globalization;
using Xunit;

namespace Calc14.Tests;

public class CalculatorStateTests
{
    [Theory]
    [InlineData("1+2*3", 7.0)]
    [InlineData("5*-3", -15.0)]
    [InlineData("10/2", 5.0)]
    [InlineData("1+2+3", 6.0)]
    [InlineData("-5+2", -3.0)]
    public void Evaluate_expressions(string expr, double expected)
    {
        var result = CalculatorState.Evaluate(expr);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 10);
    }

    [Theory]
    [InlineData("6/0")]
    [InlineData("1+")]
    [InlineData("abc")]
    public void Evaluate_invalid_returns_null(string expr)
    {
        Assert.Null(CalculatorState.Evaluate(expr));
    }

    [Fact]
    public void Evaluate_precedence_and_unary()
    {
        Assert.Equal(7, CalculatorState.Evaluate("1+2*3"));
        Assert.Equal(-15, CalculatorState.Evaluate("5*-3"));
        Assert.Null(CalculatorState.Evaluate("10/0"));
    }

    [Fact]
    public void Percent_of_accumulator_after_add()
    {
        var calc = new CalculatorState();
        calc.Digit('2');
        calc.Digit('0');
        calc.Digit('0');
        calc.SetOp(CalculatorState.Op.Add);
        calc.Digit('1');
        calc.Digit('0');
        calc.Percent();
        Assert.Equal("20", calc.Entry);
    }

    [Fact]
    public void Percent_alone_divides_by_100()
    {
        var calc = new CalculatorState();
        calc.Digit('5');
        calc.Digit('0');
        calc.Percent();
        Assert.Equal("0.5", calc.Entry);
    }

    [Fact]
    public void Equals_divide_by_zero_returns_null_and_resets()
    {
        var calc = new CalculatorState();
        calc.Digit('1');
        calc.Digit('0');
        calc.SetOp(CalculatorState.Op.Div);
        calc.Digit('0');
        Assert.Null(calc.Equals_());
        Assert.Equal("Error", calc.Entry);
        Assert.False(calc.HasPending);
        Assert.Equal(0, calc.Accumulator);

        // Recoverable: next digit starts fresh
        calc.Digit('3');
        Assert.Equal("3", calc.Entry);
    }

    [Fact]
    public void SetOp_divide_by_zero_resets_state()
    {
        var calc = new CalculatorState();
        calc.Digit('8');
        calc.SetOp(CalculatorState.Op.Div);
        calc.Digit('0');
        var shown = calc.SetOp(CalculatorState.Op.Add);
        Assert.Equal("Error", shown);
        Assert.False(calc.HasPending);
        Assert.Equal(0, calc.Accumulator);
    }

    [Fact]
    public void FormatResult_scientific_roundtrips_via_TrySetEntry()
    {
        var formatted = CalculatorState.FormatResult(1e16);
        Assert.Contains("E", formatted, StringComparison.OrdinalIgnoreCase);

        var calc = new CalculatorState();
        Assert.True(calc.TrySetEntry(formatted));
        Assert.Equal(formatted, calc.Entry);

        calc.SetOp(CalculatorState.Op.Add);
        calc.Digit('0');
        var result = calc.Equals_();
        Assert.NotNull(result);
        Assert.Equal(formatted, result);
    }

    [Fact]
    public void TrySetEntry_rejects_error_and_non_numeric()
    {
        var calc = new CalculatorState();
        Assert.False(calc.TrySetEntry("Error"));
        Assert.False(calc.TrySetEntry("not-a-number"));
        Assert.True(calc.TrySetEntry("42.5"));
        Assert.Equal("42.5", calc.Entry);
    }

    [Fact]
    public void FormatResult_always_invariant()
    {
        var prev = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var formatted = CalculatorState.FormatResult(12.5);
            Assert.Equal("12.5", formatted);
            Assert.DoesNotContain(",", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = prev;
        }
    }

    [Fact]
    public void Digit_ignores_non_digit_characters()
    {
        var calc = new CalculatorState();
        calc.Digit('1');
        calc.Digit('E');
        calc.Digit('+');
        calc.Digit('2');
        Assert.Equal("12", calc.Entry);
    }

    [Fact]
    public void Immediate_execution_chains_ops()
    {
        var calc = new CalculatorState();
        calc.Digit('6');
        calc.SetOp(CalculatorState.Op.Mul);
        calc.Digit('7');
        Assert.Equal("42", calc.Equals_());
    }

    private static CalculatorState TinyPercent()
    {
        var calc = new CalculatorState();
        foreach (var c in "0.000000001") calc.Digit(c); // typed, so the entry is still editable
        calc.Percent();
        Assert.Equal("1E-11", calc.Entry);
        return calc;
    }

    [Fact]
    public void Digit_after_percent_starts_new_entry()
    {
        var calc = TinyPercent();
        calc.Digit('5');
        Assert.Equal("5", calc.Entry);
    }

    [Fact]
    public void Dot_after_percent_starts_new_entry()
    {
        var calc = TinyPercent();
        calc.Digit('.');
        Assert.Equal("0.", calc.Entry);
    }

    [Fact]
    public void Backspace_after_percent_does_not_edit_result()
    {
        var calc = TinyPercent();
        calc.Backspace();
        Assert.Equal("1E-11", calc.Entry);
    }

    [Fact]
    public void Percent_result_still_used_as_operand_by_next_op()
    {
        var calc = new CalculatorState();
        calc.Digit('2'); calc.Digit('0'); calc.Digit('0');
        calc.SetOp(CalculatorState.Op.Add);
        calc.Digit('1'); calc.Digit('0');
        calc.Percent();
        calc.SetOp(CalculatorState.Op.Add);
        calc.Digit('5');
        Assert.Equal("225", calc.Equals_());
    }

    [Fact]
    public void Percent_overflow_errors_and_resets_pending_state()
    {
        var calc = new CalculatorState();
        Assert.True(calc.TrySetEntry("1E+300"));
        calc.SetOp(CalculatorState.Op.Add);
        foreach (var c in "99999999999") calc.Digit(c);
        calc.Percent(); // 1e300 * 1e11 / 100 overflows to Infinity
        Assert.Equal("Error", calc.Entry);
        Assert.False(calc.HasPending);
        Assert.Equal(0, calc.Accumulator);

        calc.Digit('3');
        Assert.Equal("3", calc.Equals_()); // stale 1E+300 + pending op must not survive the error
    }

    [Fact]
    public void Evaluate_non_finite_returns_null()
    {
        var huge = new string('9', 400); // parses to +Infinity
        Assert.Null(CalculatorState.Evaluate(huge));
        Assert.Null(CalculatorState.Evaluate(huge + "*0")); // Infinity * 0 = NaN
    }

    [Theory]
    [InlineData("8-3-2", 3.0)]      // left-associative
    [InlineData("8/4/2", 1.0)]
    [InlineData("2x3", 6.0)]        // x multiplies
    [InlineData(" 1 + 2 ", 3.0)]
    [InlineData("+5", 5.0)]         // unary plus
    [InlineData("--5", 5.0)]
    [InlineData("2--3", 5.0)]
    [InlineData(".5*4", 2.0)]
    public void Evaluate_more_expressions(string expr, double expected)
    {
        Assert.Equal(expected, CalculatorState.Evaluate(expr)!.Value, precision: 10);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("*2")]
    [InlineData("(1+2)")]
    [InlineData("1e5")]
    public void Evaluate_more_invalid_returns_null(string expr)
    {
        Assert.Null(CalculatorState.Evaluate(expr));
    }

    [Theory]
    [InlineData(double.NaN, "Error")]
    [InlineData(double.PositiveInfinity, "Error")]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(1e-11, "1E-11")]
    [InlineData(123456789012345.0, "123456789012345")]
    public void FormatResult_cases(double value, string expected)
    {
        Assert.Equal(expected, CalculatorState.FormatResult(value));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("")]
    [InlineData("1,5")]
    public void TrySetEntry_rejects_non_finite_and_bad_input(string value)
    {
        var calc = new CalculatorState();
        Assert.False(calc.TrySetEntry(value));
        Assert.Equal("0", calc.Entry);
    }

    [Fact]
    public void Digit_leading_zero_dot_and_length_cap()
    {
        var calc = new CalculatorState();
        calc.Digit('0'); calc.Digit('0');
        Assert.Equal("0", calc.Entry);
        calc.Digit('.'); calc.Digit('.'); calc.Digit('5');
        Assert.Equal("0.5", calc.Entry);

        calc = new CalculatorState();
        foreach (var _ in Enumerable.Range(0, 20)) calc.Digit('9');
        Assert.Equal(17, calc.Entry.Length);
    }

    [Fact]
    public void Clear_entry_keeps_pending_operation()
    {
        var calc = new CalculatorState();
        calc.Digit('9');
        calc.SetOp(CalculatorState.Op.Sub);
        calc.Digit('5');
        calc.Clear();
        Assert.True(calc.HasPending);
        calc.Digit('4');
        Assert.Equal("5", calc.Equals_());
    }

    [Fact]
    public void Backspace_edits_typed_entry_only()
    {
        var calc = new CalculatorState();
        calc.Digit('1'); calc.Digit('2');
        calc.Backspace();
        Assert.Equal("1", calc.Entry);
        calc.Backspace();
        Assert.Equal("0", calc.Entry);

        calc.Digit('7');
        calc.Equals_();
        calc.Backspace(); // result is not editable
        Assert.Equal("7", calc.Entry);
    }

    [Fact]
    public void Repeated_operator_replaces_pending_op()
    {
        var calc = new CalculatorState();
        calc.Digit('5');
        calc.SetOp(CalculatorState.Op.Add);
        Assert.Equal("5", calc.SetOp(CalculatorState.Op.Mul));
        Assert.Equal(CalculatorState.Op.Mul, calc.CurrentOp);
        calc.Digit('3');
        Assert.Equal("15", calc.Equals_());
    }

    [Fact]
    public void Ops_after_error_do_not_resurrect_state()
    {
        var calc = new CalculatorState();
        calc.Digit('1');
        calc.SetOp(CalculatorState.Op.Div);
        calc.Digit('0');
        calc.Equals_();
        Assert.Equal("Error", calc.SetOp(CalculatorState.Op.Add));
        Assert.False(calc.HasPending);
        Assert.Null(calc.Equals_());
    }
}
