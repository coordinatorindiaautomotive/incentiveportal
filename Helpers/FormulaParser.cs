using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IncentivePortal.Helpers;

public static class FormulaParser
{
    public static decimal ParseAndEvaluate(string expression, Dictionary<string, decimal> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return 0m;

        var tokens = Tokenize(expression);
        var index = 0;
        return ParseOr(tokens, ref index, variables);
    }

    /// <summary>
    /// Returns all identifier tokens (variable names and function calls) found in
    /// the expression, without evaluating it. Used for validation and autocomplete.
    /// </summary>
    public static List<string> ExtractIdentifiers(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return [];

        try
        {
            var tokens = Tokenize(expression);
            return tokens
                .Where(t => t.Type == TokenType.Identifier)
                .Select(t => t.Value)
                .ToList();
        }
        catch
        {
            // If tokenising fails, return empty — caller will handle syntax error separately
            return [];
        }
    }

    private enum TokenType
    {
        Number,
        Identifier,
        Operator,
        OpenParenthesis,
        CloseParenthesis,
        Comma
    }

    private struct Token(TokenType type, string value)
    {
        public TokenType Type = type;
        public string Value = value;
    }

    private static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < expression.Length)
        {
            var c = expression[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new Token(TokenType.OpenParenthesis, "("));
                i++;
            }
            else if (c == ')')
            {
                tokens.Add(new Token(TokenType.CloseParenthesis, ")"));
                i++;
            }
            else if (c == ',')
            {
                tokens.Add(new Token(TokenType.Comma, ","));
                i++;
            }
            else if (char.IsDigit(c) || c == '.')
            {
                var sb = new StringBuilder();
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    sb.Append(expression[i]);
                    i++;
                }
                tokens.Add(new Token(TokenType.Number, sb.ToString()));
            }
            else if (char.IsLetter(c))
            {
                var sb = new StringBuilder();
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    sb.Append(expression[i]);
                    i++;
                }
                tokens.Add(new Token(TokenType.Identifier, sb.ToString()));
            }
            else if (c == '>' || c == '<' || c == '=' || c == '!')
            {
                var sb = new StringBuilder();
                sb.Append(c);
                i++;
                if (i < expression.Length && expression[i] == '=')
                {
                    sb.Append(expression[i]);
                    i++;
                }
                tokens.Add(new Token(TokenType.Operator, sb.ToString()));
            }
            else if (c == '+' || c == '-' || c == '*' || c == '/')
            {
                tokens.Add(new Token(TokenType.Operator, c.ToString()));
                i++;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected character in formula: '{c}'");
            }
        }
        return tokens;
    }

    private static decimal ParseOr(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParseAnd(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Identifier && tokens[index].Value.Equals("OR", StringComparison.OrdinalIgnoreCase))
        {
            index++; // consume OR
            var right = ParseAnd(tokens, ref index, variables);
            left = (left != 0m || right != 0m) ? 1m : 0m;
        }
        return left;
    }

    private static decimal ParseAnd(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParseEquality(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Identifier && tokens[index].Value.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            index++; // consume AND
            var right = ParseEquality(tokens, ref index, variables);
            left = (left != 0m && right != 0m) ? 1m : 0m;
        }
        return left;
    }

    private static decimal ParseEquality(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParseComparison(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && (tokens[index].Value == "==" || tokens[index].Value == "!="))
        {
            var op = tokens[index].Value;
            index++;
            var right = ParseComparison(tokens, ref index, variables);
            if (op == "==")
            {
                left = (left == right) ? 1m : 0m;
            }
            else
            {
                left = (left != right) ? 1m : 0m;
            }
        }
        return left;
    }

    private static decimal ParseComparison(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParseAddition(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && 
               (tokens[index].Value == ">" || tokens[index].Value == "<" || tokens[index].Value == ">=" || tokens[index].Value == "<="))
        {
            var op = tokens[index].Value;
            index++;
            var right = ParseAddition(tokens, ref index, variables);
            left = op switch
            {
                ">" => (left > right) ? 1m : 0m,
                "<" => (left < right) ? 1m : 0m,
                ">=" => (left >= right) ? 1m : 0m,
                "<=" => (left <= right) ? 1m : 0m,
                _ => throw new InvalidOperationException($"Unknown operator: '{op}'")
            };
        }
        return left;
    }

    private static decimal ParseAddition(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParseMultiplication(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && (tokens[index].Value == "+" || tokens[index].Value == "-"))
        {
            var op = tokens[index].Value;
            index++;
            var right = ParseMultiplication(tokens, ref index, variables);
            if (op == "+")
            {
                left += right;
            }
            else
            {
                left -= right;
            }
        }
        return left;
    }

    private static decimal ParseMultiplication(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var left = ParsePrimary(tokens, ref index, variables);
        while (index < tokens.Count && tokens[index].Type == TokenType.Operator && (tokens[index].Value == "*" || tokens[index].Value == "/"))
        {
            var op = tokens[index].Value;
            index++;
            var right = ParsePrimary(tokens, ref index, variables);
            if (op == "*")
            {
                left *= right;
            }
            else
            {
                if (right == 0m)
                    throw new DivideByZeroException("Division by zero in formula.");
                left /= right;
            }
        }
        return left;
    }

    private static decimal ParsePrimary(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        if (index >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression.");

        var token = tokens[index];

        if (token.Type == TokenType.Number)
        {
            index++;
            return decimal.Parse(token.Value, CultureInfo.InvariantCulture);
        }

        if (token.Type == TokenType.OpenParenthesis)
        {
            index++;
            var val = ParseOr(tokens, ref index, variables);
            if (index >= tokens.Count || tokens[index].Type != TokenType.CloseParenthesis)
                throw new InvalidOperationException("Missing closing parenthesis.");
            index++; // consume )
            return val;
        }

        if (token.Type == TokenType.Operator && (token.Value == "-" || token.Value == "+"))
        {
            index++;
            var val = ParsePrimary(tokens, ref index, variables);
            return token.Value == "-" ? -val : val;
        }

        if (token.Type == TokenType.Identifier)
        {
            var name = token.Value;
            index++;

            // Check if this is a function call like IF(...)
            if (index < tokens.Count && tokens[index].Type == TokenType.OpenParenthesis)
            {
                index++; // consume (
                if (name.Equals("IF", StringComparison.OrdinalIgnoreCase))
                {
                    var cond = ParseOr(tokens, ref index, variables);
                    if (index >= tokens.Count || tokens[index].Type != TokenType.Comma)
                        throw new InvalidOperationException("Missing comma in IF function call.");
                    index++; // consume ,

                    var trueVal = ParseOr(tokens, ref index, variables);
                    if (index >= tokens.Count || tokens[index].Type != TokenType.Comma)
                        throw new InvalidOperationException("Missing comma in IF function call.");
                    index++; // consume ,

                    var falseVal = ParseOr(tokens, ref index, variables);
                    if (index >= tokens.Count || tokens[index].Type != TokenType.CloseParenthesis)
                        throw new InvalidOperationException("Missing closing parenthesis in IF function call.");
                    index++; // consume )

                    return (cond != 0m) ? trueVal : falseVal;
                }
                else
                {
                    var args = ParseFunctionArgs(tokens, ref index, variables);

                    return name.ToUpperInvariant() switch
                    {
                        "AND" => args.TrueForAll(a => a != 0m) ? 1m : 0m,
                        "OR" => args.Exists(a => a != 0m) ? 1m : 0m,
                        "NOT" => args.Count != 1
                            ? throw new InvalidOperationException("NOT function requires exactly 1 argument.")
                            : (args[0] == 0m ? 1m : 0m),
                        "ROUND" => args.Count != 2
                            ? throw new InvalidOperationException("ROUND function requires exactly 2 arguments.")
                            : Math.Round(args[0], (int)args[1], MidpointRounding.AwayFromZero),
                        "ABS" => args.Count != 1
                            ? throw new InvalidOperationException("ABS function requires exactly 1 argument.")
                            : Math.Abs(args[0]),
                        "MIN" => args.Count == 0
                            ? throw new InvalidOperationException("MIN function requires at least 1 argument.")
                            : args.Min(),
                        "MAX" => args.Count == 0
                            ? throw new InvalidOperationException("MAX function requires at least 1 argument.")
                            : args.Max(),
                        _ => throw new InvalidOperationException($"Unknown function call: '{name}'")
                    };
                }
            }

            // Otherwise, it's a variable lookup
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }
            throw new InvalidOperationException($"Variable '{name}' is not defined in context.");
        }

        throw new InvalidOperationException($"Unexpected token in expression: '{token.Value}'");
    }

    private static List<decimal> ParseFunctionArgs(List<Token> tokens, ref int index, Dictionary<string, decimal> variables)
    {
        var args = new List<decimal>();

        // Handle empty argument list: e.g. FUNC()
        if (index < tokens.Count && tokens[index].Type == TokenType.CloseParenthesis)
        {
            index++; // consume )
            return args;
        }

        // Parse first argument
        args.Add(ParseOr(tokens, ref index, variables));

        // Parse subsequent comma-separated arguments
        while (index < tokens.Count && tokens[index].Type == TokenType.Comma)
        {
            index++; // consume ,
            args.Add(ParseOr(tokens, ref index, variables));
        }

        if (index >= tokens.Count || tokens[index].Type != TokenType.CloseParenthesis)
            throw new InvalidOperationException("Missing closing parenthesis in function call.");
        index++; // consume )

        return args;
    }
}
