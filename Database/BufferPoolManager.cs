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
        
        if (!data.Tables.ContainsKey(parser.Table))
        {
            throw new Exception($"Table '{parser.Table}' does not exist.");
        }

        if (!data.Rows.ContainsKey(parser.Table))
        {
            return new List<Dictionary<string, object>>();
        }

        var rows = data.Rows[parser.Table];
        
        // Apply WHERE conditions if present
        if (parser.WhereConditions.Count > 0)
        {
            rows = FilterRowsByWhereConditions(rows, parser.WhereConditions, data.Tables[parser.Table]);
        }

        // SELECT * => return all columns
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
        
        if (!data.Tables.ContainsKey(parser.Table))
        {
            throw new Exception($"Table '{parser.Table}' does not exist.");
        }

        var tableSchema = data.Tables[parser.Table];
        var obj = new Dictionary<string, object>();

        // Map provided values for easy lookup
        var providedData = new Dictionary<string, string>();
        for (int i = 0; i < parser.Keys.Count; i++)
        {
            providedData[parser.Keys[i]] = parser.Values[i];
        }

        // Iterate through ALL schema columns to ensure distinct rows
        foreach (var column in tableSchema)
        {
            string colName = column.Key;
            string colType = column.Value;

            if (providedData.ContainsKey(colName))
            {
                obj[colName] = ParseType(providedData[colName], colType);
            }
            else
            {
                // Insert NULL if column is missing in the INSERT statement
                obj[colName] = null;
            }
        }

        if (!_page.Rows.ContainsKey(parser.Table))
        {
            _page.Rows[parser.Table] = new List<Dictionary<string, object>>();
        }

        _page.Rows[parser.Table].Add(obj);
        _isDirty = true;
        ReadOrWriteOnDisk();
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
        ReadOrWriteOnDisk();
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
        ReadOrWriteOnDisk();
    }

    /// <summary>
    /// Filter rows based on WHERE conditions
    /// </summary>
    private List<Dictionary<string, object>> FilterRowsByWhereConditions(
        List<Dictionary<string, object>> rows, 
        List<WhereCondition> conditions,
        Dictionary<string, string> tableSchema)
    {
        var filteredRows = new List<Dictionary<string, object>>();

        foreach (var row in rows)
        {
            bool? previousResult = null;

            foreach (var condition in conditions)
            {
                bool conditionResult = EvaluateCondition(row, condition, tableSchema);

                if (previousResult == null)
                {
                    // First condition
                    previousResult = conditionResult;
                }
                else
                {
                    // Apply logical operator from CURRENT condition
                    if (condition.LogicalOperator == "AND")
                    {
                        previousResult = previousResult.Value && conditionResult;
                    }
                    else if (condition.LogicalOperator == "OR")
                    {
                        previousResult = previousResult.Value || conditionResult;
                    }
                }
            }

            bool includeRow = previousResult ?? true;

            if (includeRow)
            {
                filteredRows.Add(row);
            }
        }

        return filteredRows;
    }

    /// <summary>
    /// Evaluate a single WHERE condition
    /// </summary>
    private bool EvaluateCondition(
        Dictionary<string, object> row, 
        WhereCondition condition,
        Dictionary<string, string> tableSchema)
    {
        if (!row.ContainsKey(condition.Column))
        {
            return false;
        }

        if (!tableSchema.ContainsKey(condition.Column))
        {
            // Schema mismatch or invalid column
            return false;
        }

        var columnValue = row[condition.Column];

        // Handle NULL values (comparisons with NULL return false)
        if (columnValue == null)
        {
             return false;
        }

        var fieldType = tableSchema[condition.Column];
        var conditionValue = ParseType(condition.Value, fieldType);

        return condition.Operator switch
        {
            "=" => CompareValues(columnValue, conditionValue) == 0,
            "!=" => CompareValues(columnValue, conditionValue) != 0,
            ">" => CompareValues(columnValue, conditionValue) > 0,
            "<" => CompareValues(columnValue, conditionValue) < 0,
            ">=" => CompareValues(columnValue, conditionValue) >= 0,
            "<=" => CompareValues(columnValue, conditionValue) <= 0,
            _ => false
        };
    }

    /// <summary>
    /// Compare two values of the same type
    /// </summary>
    private int CompareValues(object value1, object value2)
    {
        // Handle JsonElement from disk deserialization
        if (value1 is JsonElement json1)
        {
            try 
            {
                if (value2 is int) value1 = json1.GetInt32();
                else if (value2 is double) value1 = json1.GetDouble();
                else if (value2 is string) value1 = json1.GetString() ?? string.Empty;
            }
            catch
            {
                // If conversion fails => Fallback: convert both to string if types mismatch
                return string.Compare(value1?.ToString(), value2?.ToString(), StringComparison.Ordinal);

            }
        }

        // Handle numeric conversions (e.g. int vs double)
        if (value1 is int i1 && value2 is double d2)
        {
            return ((double)i1).CompareTo(d2);
        }
        if (value1 is double d1 && value2 is int i2)
        {
            return d1.CompareTo((double)i2);
        }

        if (value1 is int int1 && value2 is int int2)
        {
            return int1.CompareTo(int2);
        }
        else if (value1 is double double1 && value2 is double double2)
        {
            return double1.CompareTo(double2);
        }
        else if (value1 is string str1 && value2 is string str2)
        {
            return string.Compare(str1, str2, StringComparison.Ordinal);
        }
        
        // Fallback: convert both to string if types mismatch
        return string.Compare(value1?.ToString(), value2?.ToString(), StringComparison.Ordinal);
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
