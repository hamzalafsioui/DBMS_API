namespace Database;

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

    public SqlParser(string query)
    {
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
    /// </summary>
    private void ParseSelect(string[] tokens)
    {
        // Table is the last token (may have semicolon)
        Table = tokens[^1].TrimEnd(';');
        
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
        Table = tokens[2];
        
        // Extract keys from parentheses
        string keysStr = tokens[3].TrimStart('(').TrimEnd(')');
        Keys = keysStr.Split(',').ToList();
        
        // Extract values from the last token
        string valuesStr = tokens[^1].TrimStart('(').TrimEnd(')', ';');
        Values = ParseValues(valuesStr);
    }

    /// <summary>
    /// Parse CREATE TABLE query
    /// Example: CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);
    /// </summary>
    private void ParseCreateTable(string[] tokens)
    {
        Table = tokens[2];
        
        // Join remaining tokens and extract field definitions
        string fieldsStr = string.Join(" ", tokens.Skip(3));
        fieldsStr = fieldsStr.TrimStart('(').TrimEnd(')', ';');
        
        foreach (var field in fieldsStr.Split(','))
        {
            var parts = field.Trim().Split(' ');
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
