using System.Text.Json;

namespace Database;

/// <summary>
/// Handles database persistence using JSON serialization
/// </summary>
public class DiskManager
{
    private readonly string _basePath;

    public DiskManager(string basePath = "databases_list")
    {
        _basePath = basePath;
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    /// <summary>
    /// Read database from disk
    /// </summary>
    public DatabasePage Read(string database)
    {
        string filePath = Path.Combine(_basePath, $"{database}.json");
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DatabasePage>(json) ?? new DatabasePage();
    }

    /// <summary>
    /// Write database to disk
    /// </summary>
    public void Write(string database, DatabasePage data)
    {
        string filePath = Path.Combine(_basePath, $"{database}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Check if database exists
    /// </summary>
    public bool Exists(string database)
    {
        string filePath = Path.Combine(_basePath, $"{database}.json");
        return File.Exists(filePath);
    }
}

/// <summary>
/// Represents the database page structure
/// </summary>
public class DatabasePage
{
    public Dictionary<string, Dictionary<string, string>> Tables { get; set; } = new();
    public Dictionary<string, List<Dictionary<string, object>>> Rows { get; set; } = new();
}
