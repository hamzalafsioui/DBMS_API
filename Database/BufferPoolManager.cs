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
    public List<Dictionary<string, object?>> SelectRows(SqlParser parser)
    {
        var data = ReadOrWriteOnDisk();
        
        if (!data.Tables.ContainsKey(parser.Table))
        {
            throw new Exception($"Table '{parser.Table}' does not exist.");
        }

        if (!data.Rows.ContainsKey(parser.Table))
        {
            return new List<Dictionary<string, object?>>();
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
        var selectedRows = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var obj = new Dictionary<string, object?>();
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
        var obj = new Dictionary<string, object?>();

        // Map provided values for easy lookup
        var providedData = new Dictionary<string, string>();
        for (int i = 0; i < parser.Keys.Count; i++)
        {
            providedData[parser.Keys[i]] = parser.Values[i];
        }

        PrimaryKeyInfo? pkInfo = _page.PrimaryKeys.ContainsKey(parser.Table) ? _page.PrimaryKeys[parser.Table] : null;
        if (pkInfo != null)
        {
            EnsureNextValueSynchronized(parser.Table, pkInfo);
        }

        // Iterate through ALL schema columns to ensure distinct rows
        foreach (var column in tableSchema)
        {
            string colName = column.Key;
            string colType = column.Value;

            bool isAutoIncCol = pkInfo?.AutoIncrementColumn == colName;
            bool isProvided = providedData.ContainsKey(colName);
            string? rawVal = isProvided ? providedData[colName]?.Trim() : null;
            bool isNullOrEmpty = string.IsNullOrEmpty(rawVal) || (rawVal != null && rawVal.Equals("NULL", StringComparison.OrdinalIgnoreCase));

            if (isAutoIncCol && (isNullOrEmpty || rawVal == "0"))
            {
                // Auto-generate value if omitted, NULL, or 0
                obj[colName] = pkInfo!.NextValue;
                pkInfo.NextValue++;
            }
            else if (isProvided)
            {
                if (isNullOrEmpty)
                {
                    obj[colName] = null;
                }
                else
                {
                    var val = ParseType(rawVal!, colType);
                    obj[colName] = val;

                    // Update auto_increment counter if a value was provided manually
                    if (isAutoIncCol && val is int intVal)
                    {
                        if (intVal >= pkInfo!.NextValue)
                        {
                            pkInfo.NextValue = intVal + 1;
                        }
                    }
                }
            }
            else if (isAutoIncCol)
            {
                // Automatically assign auto_increment value when column is omitted
                obj[colName] = pkInfo!.NextValue;
                pkInfo.NextValue++;
            }
            else
            {
                // Insert NULL if column is missing in the INSERT statement
                obj[colName] = null;
            }
        }

        // Check for Primary Key violation
        if (pkInfo != null)
        {
            // Ensure PK columns are NOT NULL
            foreach (var col in pkInfo.Columns)
            {
                if (obj[col] == null)
                {
                    throw new Exception($"Primary Key Violation: Column '{col}' cannot be NULL.");
                }
            }

            if (CheckPrimaryKeyViolation(parser.Table, obj, pkInfo.Columns))
            {
                var pkValues = string.Join(", ", pkInfo.Columns.Select(c => $"{c}='{obj[c]}'"));
                throw new Exception($"Primary Key Violation: A row with {pkValues} already exists in table '{parser.Table}'.");
            }
        }

        if (!_page.Rows.ContainsKey(parser.Table))
        {
            _page.Rows[parser.Table] = new List<Dictionary<string, object?>>();
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
        _page.Rows[parser.Table] = new List<Dictionary<string, object?>>();

        // Store primary key information if provided
        if (parser.PrimaryKeys.Count > 0 || !string.IsNullOrEmpty(parser.AutoIncrementColumn))
        {
            _page.PrimaryKeys[parser.Table] = new PrimaryKeyInfo 
            { 
                Columns = parser.PrimaryKeys,
                AutoIncrementColumn = parser.AutoIncrementColumn,
                NextValue = 1
            };
        }

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

        if (_page.PrimaryKeys.ContainsKey(parser.Table))
        {
            _page.PrimaryKeys.Remove(parser.Table);
        }

        _isDirty = true;
        ReadOrWriteOnDisk();
    }

    /// <summary>
    /// Execute DELETE query
    /// </summary>
    public int DeleteRows(SqlParser parser)
    {
        var data = ReadOrWriteOnDisk();
        
        if (!data.Tables.ContainsKey(parser.Table))
        {
            throw new Exception($"Table '{parser.Table}' does not exist.");
        }

        if (!data.Rows.ContainsKey(parser.Table))
        {
            return 0; // No rows to delete
        }

        var rows = data.Rows[parser.Table];
        int originalCount = rows.Count;

        // If no WHERE conditions => delete all rows -)
        if (parser.WhereConditions.Count == 0)
        {
            _page.Rows[parser.Table].Clear();
            _isDirty = true;
            ReadOrWriteOnDisk();
            return originalCount;
        }

        // Filter rows to keep (inverse of rows to delete)
        var rowsToKeep = new List<Dictionary<string, object?>>();
        var tableSchema = data.Tables[parser.Table];

        foreach (var row in rows)
        {
            bool matchesConditions = EvaluateAllConditions(row, parser.WhereConditions, tableSchema);
            
            // Keep rows that DON'T match the WHERE conditions
            if (!matchesConditions)
            {
                rowsToKeep.Add(row);
            }
        }

        int deletedCount = originalCount - rowsToKeep.Count;

        if (deletedCount > 0)
        {
            _page.Rows[parser.Table] = rowsToKeep;
            _isDirty = true;
            ReadOrWriteOnDisk();
        }

        return deletedCount;
    }

    /// <summary>
    /// Execute UPDATE query
    /// </summary>
    public int UpdateRows(SqlParser parser)
    {
        var data = ReadOrWriteOnDisk();
        
        if (!data.Tables.ContainsKey(parser.Table))
        {
            throw new Exception($"Table '{parser.Table}' does not exist.");
        }

        if (!data.Rows.ContainsKey(parser.Table))
        {
            return 0; // No rows to update
        }

        var rows = data.Rows[parser.Table];
        var tableSchema = data.Tables[parser.Table];
        int updatedCount = 0;

        // Validate that all columns in SET clause exist in schema
        foreach (var key in parser.Keys)
        {
            if (!tableSchema.ContainsKey(key))
            {
                throw new Exception($"Column '{key}' does not exist in table '{parser.Table}'.");
            }
        }

        // Prepare update values with proper types
        var updateValues = new Dictionary<string, object?>();
        for (int i = 0; i < parser.Keys.Count; i++)
        {
            string columnName = parser.Keys[i];
            string columnType = tableSchema[columnName];
            updateValues[columnName] = ParseType(parser.Values[i], columnType);
        }

        // Update matching rows
        foreach (var row in rows)
        {
            bool shouldUpdate = true;

            // If WHERE conditions exist: check if row matches
            if (parser.WhereConditions.Count > 0)
            {
                shouldUpdate = EvaluateAllConditions(row, parser.WhereConditions, tableSchema);
            }

            if (shouldUpdate)
            {
                // Create a copy of the row to check for PK violation before applying changes
                var updatedRow = new Dictionary<string, object?>(row);
                foreach (var kvp in updateValues)
                {
                    updatedRow[kvp.Key] = kvp.Value;
                }

                // Check for PK violation if PK columns are being updated
                if (_page.PrimaryKeys.ContainsKey(parser.Table))
                {
                    var pkInfo = _page.PrimaryKeys[parser.Table];

                    // Ensure PK columns are NOT NULL
                    foreach (var col in pkInfo.Columns)
                    {
                        if (updatedRow[col] == null)
                        {
                            throw new Exception($"Primary Key Violation: Column '{col}' cannot be NULL.");
                        }
                    }

                    bool pkChanged = pkInfo.Columns.Any(c => updateValues.ContainsKey(c));
                    
                    if (pkChanged && CheckPrimaryKeyViolation(parser.Table, updatedRow, pkInfo.Columns, row))
                    {
                         var pkValues = string.Join(", ", pkInfo.Columns.Select(c => $"{c}='{updatedRow[c]}'"));
                         throw new Exception($"Primary Key Violation: A row with {pkValues} already exists in table '{parser.Table}'.");
                    }

                    // Synchronize NextValue if an auto-increment column was updated manually
                    if (!string.IsNullOrEmpty(pkInfo.AutoIncrementColumn) && 
                        updateValues.ContainsKey(pkInfo.AutoIncrementColumn) && 
                        updateValues[pkInfo.AutoIncrementColumn] is int newIntVal)
                    {
                        if (newIntVal >= pkInfo.NextValue)
                        {
                            pkInfo.NextValue = newIntVal + 1;
                        }
                    }
                }

                // Apply updates to the original row
                foreach (var kvp in updateValues)
                {
                    row[kvp.Key] = kvp.Value;
                }
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            _isDirty = true;
            ReadOrWriteOnDisk();
        }

        return updatedCount;
    }

    /// <summary>
    /// Evaluate all WHERE conditions for a single row
    /// </summary>
    private bool EvaluateAllConditions(
        Dictionary<string, object?> row,
        List<WhereCondition> conditions,
        Dictionary<string, string> tableSchema)
    {
        bool? previousResult = null;

        foreach (var condition in conditions)
        {
            bool conditionResult = EvaluateCondition(row, condition, tableSchema);

            if (previousResult == null)
            {
                previousResult = conditionResult;
            }
            else
            {
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

        return previousResult ?? true;
    }

    /// <summary>
    /// Filter rows based on WHERE conditions
    /// </summary>
    private List<Dictionary<string, object?>> FilterRowsByWhereConditions(
        List<Dictionary<string, object?>> rows, 
        List<WhereCondition> conditions,
        Dictionary<string, string> tableSchema)
    {
        var filteredRows = new List<Dictionary<string, object?>>();

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
        Dictionary<string, object?> row, 
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

        // Handle IS NULL and IS NOT NULL operators
        if (condition.Operator == "IS NULL")
        {
            return columnValue == null;
        }
        if (condition.Operator == "IS NOT NULL")
        {
            return columnValue != null;
        }

        // Handle NULL values for standard comparisons (comparisons with NULL return false)
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
    private int CompareValues(object? value1, object? value2)
    {
        // Handle nulls
        if (value1 == null && value2 == null) return 0;
        if (value1 == null) return -1;
        if (value2 == null) return 1;

        // Handle JsonElement from disk deserialization
        if (value1 is JsonElement json1)
        {
            if (json1.ValueKind == JsonValueKind.Null) return value2 == null ? 0 : -1;
            if (value2 is int) value1 = json1.GetInt32();
            else if (value2 is double) value1 = json1.GetDouble();
            else if (value2 is string) value1 = json1.GetString() ?? string.Empty;
            else if (json1.ValueKind == JsonValueKind.Number)
            {
                if (json1.TryGetInt32(out var i)) value1 = i;
                else if (json1.TryGetDouble(out var d)) value1 = d;
            }
            else if (json1.ValueKind == JsonValueKind.String)
            {
                value1 = json1.GetString() ?? string.Empty;
            }
        }

        if (value2 is JsonElement json2)
        {
            if (json2.ValueKind == JsonValueKind.Null) return value1 == null ? 0 : 1;
            if (value1 is int) value2 = json2.GetInt32();
            else if (value1 is double) value2 = json2.GetDouble();
            else if (value1 is string) value2 = json2.GetString() ?? string.Empty;
            else if (json2.ValueKind == JsonValueKind.Number)
            {
                if (json2.TryGetInt32(out var i)) value2 = i;
                else if (json2.TryGetDouble(out var d)) value2 = d;
            }
            else if (json2.ValueKind == JsonValueKind.String)
            {
                value2 = json2.GetString() ?? string.Empty;
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
    /// Check if a row with the same primary key values already exists
    /// </summary>
    private bool CheckPrimaryKeyViolation(string tableName, Dictionary<string, object?> newRow, List<string> pkColumns, Dictionary<string, object?>? currentRow = null)
    {
        if (pkColumns == null || pkColumns.Count == 0) return false;
        if (!_page.Rows.ContainsKey(tableName)) return false;

        foreach (var existingRow in _page.Rows[tableName])
        {
            // If we are updating => we dont compare the row with itself
            if (currentRow != null && ReferenceEquals(existingRow, currentRow)) continue;

            bool allMatch = true;
            foreach (var col in pkColumns)
            {
                var val1 = existingRow.ContainsKey(col) ? existingRow[col] : null;
                var val2 = newRow.ContainsKey(col) ? newRow[col] : null;

                if (CompareValues(val1, val2) != 0)
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch) return true;
        }

        return false;
    }

    /// <summary>
    /// Synchronize NextValue for auto_increment if it's behind existing rows
    /// </summary>
    private void EnsureNextValueSynchronized(string tableName, PrimaryKeyInfo pkInfo)
    {
        if (string.IsNullOrEmpty(pkInfo.AutoIncrementColumn)) return;
        if (!_page.Rows.ContainsKey(tableName)) return;

        int maxVal = 0;
        foreach (var row in _page.Rows[tableName])
        {
            if (row.TryGetValue(pkInfo.AutoIncrementColumn, out var val) && val != null)
            {
                int intVal = 0;
                if (val is int i) intVal = i;
                else if (val is JsonElement je && je.ValueKind == JsonValueKind.Number) je.TryGetInt32(out intVal);
                else if (int.TryParse(val.ToString(), out var parsed)) intVal = parsed;

                if (intVal > maxVal) maxVal = intVal;
            }
        }

        if (pkInfo.NextValue <= maxVal)
        {
            pkInfo.NextValue = maxVal + 1;
        }
    }

    /// <summary>
    /// Parse value to appropriate type based on schema
    /// </summary>
    private object? ParseType(string? fieldValue, string fieldType)
    {
        if (fieldValue == null || fieldValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fieldType.ToUpper() switch
        {
            "INT" => int.TryParse(fieldValue, out var i) ? i : (object?)null,
            "FLOAT" => double.TryParse(fieldValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : (object?)null,
            "VARCHAR" => fieldValue.Trim('\'', '"'),
            _ => fieldValue
        };
    }
}
