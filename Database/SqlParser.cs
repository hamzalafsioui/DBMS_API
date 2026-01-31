namespace Database;
using System.Text.RegularExpressions;

/// <summary>
/// Represents a single WHERE condition
/// </summary>
public class WhereCondition
{
    public string Column { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string LogicalOperator { get; set; } = string.Empty; // AND or OR 
}

/// <summary>
/// Parses SQL queries into structured objects
/// </summary>
public class SqlParser
{
    public string MethodType { get; private set; } = string.Empty;
    public string Table { get; private set; } = string.Empty;
    public List<string> Keys { get; private set; } = new();
    public List<string> KeyTypes { get; private set; } = new();
    public List<string> Values { get; private set; } = new();
    public List<WhereCondition> WhereConditions { get; private set; } = new();

    public SqlParser(string query)
    {
        // Normalize query => add spaces around operators to ensure correct tokenization
        // Handles cases like "age=24" or "age>=24"
        query = Regex.Replace(query, @"(>=|<=|!=|=|>|<)", " $1 ");

        var tokens = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;

        MethodType = tokens[0].ToUpper();

        switch (MethodType)
        {
            case "SELECT":
                ParseSelect(tokens);
                break;
            case "CREATE":
                ParseCreateTable(tokens);
                break;
            case "INSERT":
                ParseInsert(tokens);
                break;
            case "DROP":
                ParseDropTable(tokens);
                break;
            default:
                throw new Exception($"Invalid Method: {MethodType}");
        }
    }

    /// <summary>
    /// Parse SELECT query
    /// Examples:
    ///   SELECT * FROM users;
    ///   SELECT username,age FROM users;
    ///   SELECT * FROM users WHERE age > 20;
    ///   SELECT username FROM users WHERE age >= 18 AND salary < 2000.0;
    /// </summary>
    private void ParseSelect(string[] tokens)
    {
        // Find WHERE keyword if it exists
        int whereIndex = Array.FindIndex(tokens, t => t.Equals("WHERE", StringComparison.OrdinalIgnoreCase));
        
        if (whereIndex > 0)
        {
            // Table is between FROM and WHERE
            Table = tokens[whereIndex - 1];
            
            // Parse WHERE conditions
            ParseWhereConditions(tokens, whereIndex + 1);
        }
        else
        {
            // Table is the last token (may have semicolon)
            Table = tokens[^1].TrimEnd(';');
        }
        
        // If not selecting all columns
        if (tokens[1] != "*")
        {
            Keys = tokens[1].Split(',').ToList();
        }
    }

    /// <summary>
    /// Parse INSERT query
    /// Example: INSERT INTO users (username,age,salary) VALUES ('hamza',24,100.0);
    /// </summary>
    private void ParseInsert(string[] tokens)
    {
        // Combine tokens to get a full string for regex matching, but tokens already had spaces around operators
        string fullQuery = string.Join(" ", tokens);
        
        var match = Regex.Match(fullQuery, @"INSERT\s+INTO\s+(\w+)\s*\((.*?)\)\s*VALUES\s*\((.*)\)", RegexOptions.IgnoreCase);
        
        if (!match.Success)
        {
            throw new Exception("Invalid INSERT syntax. Expected: INSERT INTO table (cols) VALUES (vals);");
        }

        Table = match.Groups[1].Value;
        
        // Extract keys
        string keysStr = match.Groups[2].Value;
        Keys = keysStr.Split(',').Select(k => k.Trim()).ToList();
        
        // Extract values
        string valuesStr = match.Groups[3].Value.TrimEnd(';', ' ', ')');
        Values = ParseValues(valuesStr);

        if (Keys.Count != Values.Count)
        {
            throw new Exception($"Column count ({Keys.Count}) does not match value count ({Values.Count}).");
        }
    }

    /// <summary>
    /// Parse CREATE TABLE query
    /// Example: CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);
    /// </summary>
    private void ParseCreateTable(string[] tokens)
    {
        string fullQuery = string.Join(" ", tokens);
        var match = Regex.Match(fullQuery, @"CREATE\s+TABLE\s+(\w+)\s*\((.*)\)", RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new Exception("Invalid CREATE TABLE syntax. Expected: CREATE TABLE table (col type, ...);");
        }

        Table = match.Groups[1].Value;
        string fieldsStr = match.Groups[2].Value.TrimEnd(';', ' ', ')');

        foreach (var field in fieldsStr.Split(','))
        {
            var parts = field.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            
            Keys.Add(parts[0]);
            KeyTypes.Add(parts[1]);
        }
    }

    /// <summary>
    /// Parse DROP TABLE query
    /// Example: DROP TABLE users;
    /// </summary>
    private void ParseDropTable(string[] tokens)
    {
        Table = tokens[2].TrimEnd(';');
    }

    /// <summary>
    /// Parse WHERE conditions from tokens
    /// </summary>
    private void ParseWhereConditions(string[] tokens, int startIndex)
    {
        var conditionTokens = new List<string>();
        
        // Collect all tokens from WHERE to end of query
        for (int j = startIndex; j < tokens.Length; j++)
        {
            conditionTokens.Add(tokens[j].TrimEnd(';'));
        }
        
        // Parse conditions separated by AND/OR
        int i = 0;
        string currentLogicalOp = "";
        
        while (i < conditionTokens.Count)
        {
            // Check if current token is a logical operator
            if (conditionTokens[i].Equals("AND", StringComparison.OrdinalIgnoreCase) || 
                conditionTokens[i].Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                currentLogicalOp = conditionTokens[i].ToUpper();
                i++;
                continue;
            }
            
            // Parse condition: column operator value
            if (i + 2 < conditionTokens.Count)
            {
                var condition = new WhereCondition
                {
                    Column = conditionTokens[i],
                    Operator = conditionTokens[i + 1],
                    Value = conditionTokens[i + 2],
                    LogicalOperator = currentLogicalOp
                };
                
                WhereConditions.Add(condition);
                i += 3;
                currentLogicalOp = ""; // Reset for next condition
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Parse comma-separated values, handling quoted strings
    /// </summary>
    private List<string> ParseValues(string valuesStr)
    {
        var result = new List<string>();
        var current = "";
        bool inQuotes = false;

        foreach (char c in valuesStr)
        {
            if (c == '\'' && !inQuotes)
            {
                inQuotes = true;
                current += c;
            }
            else if (c == '\'' && inQuotes)
            {
                inQuotes = false;
                current += c;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        
        if (!string.IsNullOrEmpty(current))
        {
            result.Add(current.Trim());
        }

        return result;
    }
}
