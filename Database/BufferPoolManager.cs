using System.Text.Json;

namespace Database;

/// <summary>
/// Buffer Pool Manager - Manages in-memory data with dirty page tracking
/// </summary>
public class BufferPoolManager
{
    private readonly DiskManager _diskManager;
    private readonly string _database;
    private DatabasePage _page;
    private bool _isDirty = false;

    public BufferPoolManager(string database)
    {
        _database = database;
        _diskManager = new DiskManager();

        if (_diskManager.Exists(database))
        {
            _page = _diskManager.Read(database);
        }
        else
        {
            _page = new DatabasePage();
            _diskManager.Write(database, _page);
        }
    }

    /// <summary>
    /// Flush dirty pages to disk if needed
    /// </summary>
    private DatabasePage ReadOrWriteOnDisk()
    {
        if (_isDirty)
        {
            _diskManager.Write(_database, _page);
            _isDirty = false;
        }
        return _page;
    }

    /// <summary>
    /// Execute SELECT query
    /// </summary>
    public List<Dictionary<string, object>> SelectRows(SqlParser parser)
    {
        var data = ReadOrWriteOnDisk();
        
        if (!data.Rows.ContainsKey(parser.Table))
        {
            return new List<Dictionary<string, object>>();
        }

        var rows = data.Rows[parser.Table];

        // SELECT * - return all columns
        if (parser.Keys.Count == 0)
        {
            return rows;
        }

        // SELECT specific columns
        var selectedRows = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            var obj = new Dictionary<string, object>();
            foreach (var key in parser.Keys)
            {
                if (row.ContainsKey(key))
                {
                    obj[key] = row[key];
                }
            }
            selectedRows.Add(obj);
        }

        return selectedRows;
    }

    /// <summary>
    /// Execute INSERT query
    /// </summary>
    public void InsertRow(SqlParser parser)
    {
        var data = ReadOrWriteOnDisk();
        var tableSchema = data.Tables[parser.Table];
        var obj = new Dictionary<string, object>();

        for (int i = 0; i < parser.Keys.Count; i++)
        {
            var key = parser.Keys[i];
            var value = parser.Values[i];
            var fieldType = tableSchema[key];
            
            obj[key] = ParseType(value, fieldType);
        }

        if (!_page.Rows.ContainsKey(parser.Table))
        {
            _page.Rows[parser.Table] = new List<Dictionary<string, object>>();
        }

        _page.Rows[parser.Table].Add(obj);
        _isDirty = true;
    }

    /// <summary>
    /// Execute CREATE TABLE query
    /// </summary>
    public void CreateTable(SqlParser parser)
    {
        var obj = new Dictionary<string, string>();

        for (int i = 0; i < parser.Keys.Count; i++)
        {
            obj[parser.Keys[i]] = parser.KeyTypes[i];
        }

        _page.Tables[parser.Table] = obj;
        _isDirty = true;
    }

    /// <summary>
    /// Execute DROP TABLE query
    /// </summary>
    public void DropTable(SqlParser parser)
    {
        if (_page.Tables.ContainsKey(parser.Table))
        {
            _page.Tables.Remove(parser.Table);
        }

        if (_page.Rows.ContainsKey(parser.Table))
        {
            _page.Rows.Remove(parser.Table);
        }
        
        _isDirty = true;
    }

    /// <summary>
    /// Parse value to appropriate type based on schema
    /// </summary>
    private object ParseType(string fieldValue, string fieldType)
    {
        return fieldType.ToUpper() switch
        {
            "INT" => int.Parse(fieldValue),
            "FLOAT" => double.Parse(fieldValue),
            "VARCHAR" => fieldValue.Trim('\''),
            _ => fieldValue
        };
    }
}
