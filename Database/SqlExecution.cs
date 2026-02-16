namespace Database;

/// <summary>
/// SQL Execution Engine - Routes queries to appropriate handlers
/// </summary>
public class SqlExecution
{
    private readonly BufferPoolManager _buffer;
    private object? _results;

    public SqlExecution(string database)
    {
        _buffer = new BufferPoolManager(database);
    }

    /// <summary>
    /// Execute a parsed SQL query
    /// </summary>
    public void Execute(SqlParser parser)
    {
        _results = parser.MethodType switch
        {
            "SELECT" => HandleSelect(parser),
            "CREATE" => HandleCreateTable(parser),
            "INSERT" => HandleInsert(parser),
            "DROP" => HandleDropTable(parser),
            "DELETE" => HandleDelete(parser),
            "UPDATE" => HandleUpdate(parser),
            _ => "Error: Unknown command"
        };
    }

    private string HandleInsert(SqlParser parser)
    {
        _buffer.InsertRow(parser);
        return "OK: New Row Has Been Inserted !";
    }

    private string HandleCreateTable(SqlParser parser)
    {
        _buffer.CreateTable(parser);
        return "OK: New Table Created !";
    }

    private string HandleDropTable(SqlParser parser)
    {
        _buffer.DropTable(parser);
        return "OK: Table Dropped !";
    }

    private string HandleDelete(SqlParser parser)
    {
        int deletedCount = _buffer.DeleteRows(parser);
        return $"OK: {deletedCount} Row(s) Deleted !";
    }

    private string HandleUpdate(SqlParser parser)
    {
        int updatedCount = _buffer.UpdateRows(parser);
        return $"OK: {updatedCount} Row(s) Updated !";
    }

    private List<Dictionary<string, object>> HandleSelect(SqlParser parser)
    {
        return _buffer.SelectRows(parser);
    }

    /// <summary>
    /// Get execution results
    /// </summary>
    public object? Results => _results;
}
