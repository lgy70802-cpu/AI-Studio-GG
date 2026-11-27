using System.Globalization;

namespace DeskDisplay.Core.Models;

public static class NumericExpression
{
    public static bool TryEvaluate(string input, out double value)
    {
        value = 0; if (string.IsNullOrWhiteSpace(input) || input.Length > 80) return false;
        try { var parser = new Parser(input); value = parser.Expression(); parser.Space(); return parser.Position == input.Length && double.IsFinite(value); }
        catch (Exception e) when (e is FormatException or OverflowException or DivideByZeroException) { return false; }
    }
    private sealed class Parser(string text)
    {
        public int Position;
        public void Space() { while (Position < text.Length && char.IsWhiteSpace(text[Position])) Position++; }
        private bool Take(char c) { Space(); if (Position < text.Length && text[Position] == c) { Position++; return true; } return false; }
        public double Expression() { var v = Term(); while (true) { if (Take('+')) v += Term(); else if (Take('-')) v -= Term(); else return v; } }
        private double Term() { var v = Factor(); while (true) { if (Take('*')) v *= Factor(); else if (Take('/')) { var d = Factor(); if (d == 0) throw new DivideByZeroException(); v /= d; } else return v; } }
        private double Factor()
        {
            if (Take('+')) return Factor(); if (Take('-')) return -Factor();
            if (Take('(')) { var v = Expression(); if (!Take(')')) throw new FormatException(); return v; }
            Space(); var start = Position; while (Position < text.Length && (char.IsAsciiDigit(text[Position]) || text[Position] == '.')) Position++;
            if (start == Position) throw new FormatException(); return double.Parse(text[start..Position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }
    }
}
