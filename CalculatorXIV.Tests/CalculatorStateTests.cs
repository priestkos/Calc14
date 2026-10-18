using System.Globalization;
using Xunit;

namespace CalculatorXIV.Tests;

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
}
