using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SeriMongo.Querying
{
    public class LogQueryException : Exception
    {
        public LogQueryException(string message) : base(message)
        {
        }
    }

    public class LogSqlParameter
    {
        public LogSqlParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public object Value { get; }
    }

    public class LogSqlQuery
    {
        public LogSqlQuery(string whereSql, IReadOnlyList<LogSqlParameter> parameters)
        {
            WhereSql = whereSql;
            Parameters = parameters;
        }

        public string WhereSql { get; }

        public IReadOnlyList<LogSqlParameter> Parameters { get; }
    }

    public class LogQueryCompiler
    {
        private readonly List<LogSqlParameter> _parameters = new List<LogSqlParameter>();
        private List<Token> _tokens = new List<Token>();
        private int _position;

        public static readonly string[] Examples = new[]
        {
            "*",
            "level = Error",
            "serviceName = \"serimongo-simulator\"",
            "level in (Error, Warning) and timestamp >= \"2026-01-01T00:00:00Z\"",
            "message contains \"checkout failed\"",
            "exception exists",
            "prop.CustomerId = 42 or prop.Country = \"Brazil\""
        };

        public LogSqlQuery Compile(string query)
        {
            _parameters.Clear();
            _tokens = Tokenize(query ?? string.Empty);
            _position = 0;

            if (_tokens.Count == 1)
            {
                return new LogSqlQuery("1 = 1", Array.Empty<LogSqlParameter>());
            }

            if (Match(TokenKind.Star))
            {
                Expect(TokenKind.End, "The '*' query cannot be combined with other predicates.");
                return new LogSqlQuery("1 = 1", Array.Empty<LogSqlParameter>());
            }

            var whereSql = ParseExpression();
            Expect(TokenKind.End, "Unexpected token after the end of the query.");

            return new LogSqlQuery(whereSql, _parameters.ToArray());
        }

        private string ParseExpression()
        {
            return ParseOr();
        }

        private string ParseOr()
        {
            var sql = ParseAnd();

            while (MatchKeyword("or"))
            {
                var right = ParseAnd();
                sql = $"({sql} OR {right})";
            }

            return sql;
        }

        private string ParseAnd()
        {
            var sql = ParsePrimary();

            while (MatchKeyword("and"))
            {
                var right = ParsePrimary();
                sql = $"({sql} AND {right})";
            }

            return sql;
        }

        private string ParsePrimary()
        {
            if (Match(TokenKind.OpenParen))
            {
                var sql = ParseExpression();
                Expect(TokenKind.CloseParen, "Expected ')' to close the group.");
                return $"({sql})";
            }

            return ParsePredicate();
        }

        private string ParsePredicate()
        {
            var fieldToken = Expect(TokenKind.Identifier, "Expected a log field name.");
            var field = ResolveField(fieldToken.Text);

            if (MatchKeyword("exists"))
            {
                return field.ExistsSql;
            }

            if (MatchKeyword("in"))
            {
                Expect(TokenKind.OpenParen, "Expected '(' after 'in'.");
                var parameterNames = new List<string>();

                do
                {
                    parameterNames.Add(AddParameter(NormalizeValue(field, ReadValue())));
                }
                while (Match(TokenKind.Comma));

                Expect(TokenKind.CloseParen, "Expected ')' after the 'in' values.");

                if (parameterNames.Count == 0)
                {
                    throw new LogQueryException("The 'in' operator requires at least one value.");
                }

                return $"{field.ValueSql} IN ({string.Join(", ", parameterNames)})";
            }

            if (MatchKeyword("contains"))
            {
                return BuildLike(field, ReadValue(), LikeMode.Contains);
            }

            if (MatchKeyword("startswith"))
            {
                return BuildLike(field, ReadValue(), LikeMode.StartsWith);
            }

            if (MatchKeyword("endswith"))
            {
                return BuildLike(field, ReadValue(), LikeMode.EndsWith);
            }

            var op = Expect(TokenKind.Operator, "Expected an operator: =, !=, >, >=, <, <=, contains, startswith, endswith, in or exists.");
            if (!IsComparisonOperator(op.Text))
            {
                throw new LogQueryException($"Unsupported operator '{op.Text}'.");
            }

            var parameterName = AddParameter(NormalizeValue(field, ReadValue()));
            return $"{field.ValueSql} {op.Text} {parameterName}";
        }

        private string BuildLike(FieldReference field, Token valueToken, LikeMode mode)
        {
            var value = Convert.ToString(NormalizeValue(field, valueToken), CultureInfo.InvariantCulture) ?? string.Empty;
            var escaped = EscapeLike(value);
            var pattern = mode switch
            {
                LikeMode.Contains => $"%{escaped}%",
                LikeMode.StartsWith => $"{escaped}%",
                LikeMode.EndsWith => $"%{escaped}",
                _ => escaped
            };

            var parameterName = AddParameter(pattern);
            return $"{field.TextSql} LIKE {parameterName} ESCAPE '\\'";
        }

        private static object NormalizeValue(FieldReference field, Token valueToken)
        {
            if (field.Kind == FieldKind.Timestamp)
            {
                if (!DateTimeOffset.TryParse(valueToken.Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
                {
                    throw new LogQueryException($"'{valueToken.Text}' is not a valid timestamp.");
                }

                return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            }

            if (valueToken.Kind == TokenKind.Number)
            {
                if (long.TryParse(valueToken.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return integer;
                }

                if (double.TryParse(valueToken.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }
            }

            return valueToken.Text;
        }

        private Token ReadValue()
        {
            if (Current.Kind == TokenKind.String || Current.Kind == TokenKind.Identifier || Current.Kind == TokenKind.Number)
            {
                return Advance();
            }

            throw new LogQueryException("Expected a string, number or bare word value.");
        }

        private FieldReference ResolveField(string fieldName)
        {
            var normalized = fieldName.Trim();
            var lower = normalized.ToLowerInvariant();

            switch (lower)
            {
                case "id":
                    return FieldReference.Column("Id", FieldKind.Text);
                case "timestamp":
                case "time":
                    return FieldReference.Column("TimestampUtc", FieldKind.Timestamp);
                case "level":
                    return FieldReference.Column("Level", FieldKind.Text);
                case "message":
                case "renderedmessage":
                    return FieldReference.Column("RenderedMessage", FieldKind.Text);
                case "exception":
                    return FieldReference.Column("Exception", FieldKind.Text);
                case "service":
                case "servicename":
                case "service.name":
                    return FieldReference.PropertyKey("resource.service.name");
            }

            const string propPrefix = "prop.";
            const string propertyPrefix = "property.";
            const string propertiesPrefix = "properties.";

            if (lower.StartsWith(propPrefix, StringComparison.Ordinal))
            {
                return FieldReference.Property(normalized.Substring(propPrefix.Length));
            }

            if (lower.StartsWith(propertyPrefix, StringComparison.Ordinal))
            {
                return FieldReference.Property(normalized.Substring(propertyPrefix.Length));
            }

            if (lower.StartsWith(propertiesPrefix, StringComparison.Ordinal))
            {
                return FieldReference.Property(normalized.Substring(propertiesPrefix.Length));
            }

            throw new LogQueryException($"Unknown field '{fieldName}'. Use id, timestamp, level, message, exception, serviceName or prop.<name>.");
        }

        private string AddParameter(object value)
        {
            var name = "$p" + _parameters.Count.ToString(CultureInfo.InvariantCulture);
            _parameters.Add(new LogSqlParameter(name, value));
            return name;
        }

        private static bool IsComparisonOperator(string op)
        {
            return op == "=" || op == "!=" || op == ">" || op == ">=" || op == "<" || op == "<=";
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private bool MatchKeyword(string keyword)
        {
            if (Current.Kind != TokenKind.Identifier || !string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Advance();
            return true;
        }

        private Token Expect(TokenKind kind, string message)
        {
            if (Current.Kind == kind)
            {
                return Advance();
            }

            throw new LogQueryException(message);
        }

        private Token Advance()
        {
            var token = Current;
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }

            return token;
        }

        private Token Current => _tokens[_position];

        private static List<Token> Tokenize(string query)
        {
            var tokens = new List<Token>();
            var position = 0;

            while (position < query.Length)
            {
                var c = query[position];
                if (char.IsWhiteSpace(c))
                {
                    position++;
                    continue;
                }

                if (c == '*')
                {
                    tokens.Add(new Token(TokenKind.Star, "*"));
                    position++;
                    continue;
                }

                if (c == '(')
                {
                    tokens.Add(new Token(TokenKind.OpenParen, "("));
                    position++;
                    continue;
                }

                if (c == ')')
                {
                    tokens.Add(new Token(TokenKind.CloseParen, ")"));
                    position++;
                    continue;
                }

                if (c == ',')
                {
                    tokens.Add(new Token(TokenKind.Comma, ","));
                    position++;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    tokens.Add(ReadQuotedString(query, ref position));
                    continue;
                }

                if (IsOperatorStart(c))
                {
                    tokens.Add(ReadOperator(query, ref position));
                    continue;
                }

                if (char.IsDigit(c) || c == '-')
                {
                    tokens.Add(ReadNumberOrIdentifier(query, ref position));
                    continue;
                }

                if (IsIdentifierChar(c))
                {
                    tokens.Add(ReadIdentifier(query, ref position));
                    continue;
                }

                throw new LogQueryException($"Unexpected character '{c}'.");
            }

            tokens.Add(new Token(TokenKind.End, string.Empty));
            return tokens;
        }

        private static Token ReadQuotedString(string query, ref int position)
        {
            var quote = query[position++];
            var builder = new StringBuilder();

            while (position < query.Length)
            {
                var c = query[position++];
                if (c == quote)
                {
                    return new Token(TokenKind.String, builder.ToString());
                }

                if (c == '\\' && position < query.Length)
                {
                    var escaped = query[position++];
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escaped
                    });
                    continue;
                }

                builder.Append(c);
            }

            throw new LogQueryException("Unterminated string literal.");
        }

        private static Token ReadOperator(string query, ref int position)
        {
            var c = query[position++];
            if (position < query.Length && query[position] == '=')
            {
                position++;
                return new Token(TokenKind.Operator, c + "=");
            }

            if (c == '!')
            {
                throw new LogQueryException("Expected '!=' operator.");
            }

            return new Token(TokenKind.Operator, c.ToString(CultureInfo.InvariantCulture));
        }

        private static Token ReadNumberOrIdentifier(string query, ref int position)
        {
            var start = position;
            var sawDigit = false;
            var sawNonNumber = false;

            while (position < query.Length && IsValueChar(query[position]))
            {
                sawDigit |= char.IsDigit(query[position]);
                sawNonNumber |= !(char.IsDigit(query[position]) || query[position] == '-' || query[position] == '.');
                position++;
            }

            var text = query.Substring(start, position - start);
            return sawDigit && !sawNonNumber ? new Token(TokenKind.Number, text) : new Token(TokenKind.Identifier, text);
        }

        private static Token ReadIdentifier(string query, ref int position)
        {
            var start = position;
            while (position < query.Length && IsValueChar(query[position]))
            {
                position++;
            }

            return new Token(TokenKind.Identifier, query.Substring(start, position - start));
        }

        private static bool IsOperatorStart(char c)
        {
            return c == '=' || c == '!' || c == '>' || c == '<';
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetter(c) || c == '_' || c == '@';
        }

        private static bool IsValueChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ':' || c == '@' || c == '/';
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }

        private enum TokenKind
        {
            Identifier,
            Number,
            String,
            Operator,
            OpenParen,
            CloseParen,
            Comma,
            Star,
            End
        }

        private enum FieldKind
        {
            Text,
            Timestamp,
            Property
        }

        private enum LikeMode
        {
            Contains,
            StartsWith,
            EndsWith
        }

        private readonly struct Token
        {
            public Token(TokenKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public TokenKind Kind { get; }

            public string Text { get; }
        }

        private readonly struct FieldReference
        {
            private FieldReference(string valueSql, string textSql, string existsSql, FieldKind kind)
            {
                ValueSql = valueSql;
                TextSql = textSql;
                ExistsSql = existsSql;
                Kind = kind;
            }

            public string ValueSql { get; }

            public string TextSql { get; }

            public string ExistsSql { get; }

            public FieldKind Kind { get; }

            public static FieldReference Column(string columnName, FieldKind kind)
            {
                return new FieldReference(columnName, columnName, $"{columnName} IS NOT NULL AND {columnName} <> ''", kind);
            }

            public static FieldReference Property(string propertyName)
            {
                var jsonPath = BuildJsonPath(propertyName);
                var valueSql = $"json_extract(PropertiesJson, '{jsonPath}')";
                var existsSql = $"json_type(PropertiesJson, '{jsonPath}') IS NOT NULL";
                return new FieldReference(valueSql, $"CAST({valueSql} AS TEXT)", existsSql, FieldKind.Property);
            }

            public static FieldReference PropertyKey(string propertyName)
            {
                var jsonPath = BuildJsonKeyPath(propertyName);
                var valueSql = $"json_extract(PropertiesJson, '{jsonPath}')";
                var existsSql = $"json_type(PropertiesJson, '{jsonPath}') IS NOT NULL";
                return new FieldReference(valueSql, $"CAST({valueSql} AS TEXT)", existsSql, FieldKind.Property);
            }

            private static string BuildJsonPath(string propertyName)
            {
                var segments = propertyName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0)
                {
                    throw new LogQueryException("Property queries must use prop.<name>.");
                }

                foreach (var segment in segments)
                {
                    if (!segment.All(IsSafePropertyNameChar))
                    {
                        throw new LogQueryException($"Unsafe property path segment '{segment}'. Use letters, numbers, '_' or '-'.");
                    }
                }

                return "$." + string.Join(".", segments.Select(segment => $"\"{segment}\""));
            }

            private static string BuildJsonKeyPath(string propertyName)
            {
                if (string.IsNullOrWhiteSpace(propertyName) || !propertyName.All(c => IsSafePropertyNameChar(c) || c == '.'))
                {
                    throw new LogQueryException("Unsafe property key. Use letters, numbers, '_', '-' or '.'.");
                }

                return "$." + $"\"{propertyName}\"";
            }

            private static bool IsSafePropertyNameChar(char c)
            {
                return char.IsLetterOrDigit(c) || c == '_' || c == '-';
            }
        }
    }
}
