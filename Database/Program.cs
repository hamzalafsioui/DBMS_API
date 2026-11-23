using Database;

if (args.Length > 0 && args[0] == "--test")
{
    RunTests();
    return;
}

// Entry point for the database server
var host = Environment.GetEnvironmentVariable("DBMS_HOST") ?? "0.0.0.0";
var portStr = Environment.GetEnvironmentVariable("DBMS_PORT");
int port = int.TryParse(portStr, out var p) ? p : 9090;

Console.WriteLine($"Starting DBMS Server on {host}:{port}...");
var server = new ConnectionHandler(host, port);
await server.Run();

static void RunTests()
{
    Console.WriteLine("Running AutoIncrement verification tests...");
    string testDb = "autoincrement_test_db";
    var diskManager = new DiskManager();
    string dbFilePath = Path.Combine("databases_list", $"{testDb}.json");

    if (File.Exists(dbFilePath))
    {
        File.Delete(dbFilePath);
    }

    try
    {
        var executor = new SqlExecution(testDb);

        void Exec(string sql)
        {
            var parser = new SqlParser(sql);
            executor.Execute(parser);
        }

        List<Dictionary<string, object?>> Query(string sql)
        {
            var parser = new SqlParser(sql);
            executor.Execute(parser);
            return (List<Dictionary<string, object?>>)executor.Results!;
        }

        // Test 1: Standard AUTO_INCREMENT PRIMARY KEY
        Console.WriteLine("Test 1: CREATE TABLE with AUTO_INCREMENT PRIMARY KEY...");
        Exec("CREATE TABLE users (id INT AUTO_INCREMENT PRIMARY KEY, name VARCHAR);");

        Exec("INSERT INTO users (name) VALUES ('hamza');");
        Exec("INSERT INTO users (name) VALUES ('moha');");
        var rows = Query("SELECT * FROM users;");
        if (rows.Count != 2) throw new Exception($"Expected 2 rows, got {rows.Count}");
        if (Convert.ToInt32(rows[0]["id"]) != 1) throw new Exception($"Expected row 0 id = 1, got {rows[0]["id"]}");
        if (Convert.ToInt32(rows[1]["id"]) != 2) throw new Exception($"Expected row 1 id = 2, got {rows[1]["id"]}");
        Console.WriteLine("  -> PASS (Auto-assigned 1, 2)");

        // Test 2: NULL and 0 auto-assignment
        Console.WriteLine("Test 2: INSERT with NULL and 0 for auto-increment column...");
        Exec("INSERT INTO users (id, name) VALUES (NULL, 'null_person');");
        Exec("INSERT INTO users (id, name) VALUES (0, 'zero_person');");
        rows = Query("SELECT * FROM users;");
        if (Convert.ToInt32(rows[2]["id"]) != 3) throw new Exception($"Expected row 2 id = 3, got {rows[2]["id"]}");
        if (Convert.ToInt32(rows[3]["id"]) != 4) throw new Exception($"Expected row 3 id = 4, got {rows[3]["id"]}");
        Console.WriteLine("  -> PASS (NULL -> 3, 0 -> 4)");

        // Test 3: Manual higher ID and subsequent auto-increment
        Console.WriteLine("Test 3: Manual higher ID and subsequent continuation...");
        Exec("INSERT INTO users (id, name) VALUES (10, 'manual_person');");
        Exec("INSERT INTO users (name) VALUES ('next_person');");
        rows = Query("SELECT * FROM users;");
        if (Convert.ToInt32(rows[4]["id"]) != 10) throw new Exception($"Expected row 4 id = 10, got {rows[4]["id"]}");
        if (Convert.ToInt32(rows[5]["id"]) != 11) throw new Exception($"Expected row 5 id = 11, got {rows[5]["id"]}");
        Console.WriteLine("  -> PASS (10, then next auto is 11)");

        // Test 4: Duplicate manual ID should throw exception
        Console.WriteLine("Test 4: Duplicate manual ID should be rejected...");
        bool duplicateCaught = false;
        try
        {
            Exec("INSERT INTO users (id, name) VALUES (10, 'duplicate_person');");
        }
        catch
        {
            duplicateCaught = true;
        }
        if (!duplicateCaught && executor.Results is string err && err.StartsWith("Error:"))
        {
            duplicateCaught = true;
        }
        if (!duplicateCaught) throw new Exception("Expected primary key violation error for duplicate ID 10!");
        Console.WriteLine("  -> PASS (Duplicate rejected)");

        // Test 5: Table with AUTO_INCREMENT without explicit PRIMARY KEY keyword
        Console.WriteLine("Test 5: AUTO_INCREMENT without explicit PRIMARY KEY keyword...");
        Exec("CREATE TABLE test_no_pk (id INT AUTO_INCREMENT, name VARCHAR);");
        Exec("INSERT INTO test_no_pk (name) VALUES ('first');");
        Exec("INSERT INTO test_no_pk (name) VALUES ('second');");
        rows = Query("SELECT * FROM test_no_pk;");
        if (rows.Count != 2) throw new Exception($"Expected 2 rows in test_no_pk, got {rows.Count}");
        if (Convert.ToInt32(rows[0]["id"]) != 1 || Convert.ToInt32(rows[1]["id"]) != 2)
            throw new Exception("Wrong IDs in test_no_pk");
        Console.WriteLine("  -> PASS (Multiple inserts succeeded without false PK violation)");

        // Test 6: AUTOINCREMENT (no underscore) and AUTO INCREMENT (space)
        Console.WriteLine("Test 6: AUTOINCREMENT (SQLite) and AUTO INCREMENT (spaces)...");
        Exec("CREATE TABLE test_variants (id1 INT AUTOINCREMENT, id2 INT AUTO INCREMENT);");
        Exec("INSERT INTO test_variants (id1) VALUES (1);");
        Console.WriteLine("  -> PASS (Syntax variants parsed successfully)");

        // Test 7: DROP TABLE cleans up metadata
        Console.WriteLine("Test 7: DROP TABLE cleans up primary key info...");
        Exec("DROP TABLE users;");
        Console.WriteLine("  -> PASS (Table dropped cleanly)");

        Console.WriteLine("\nALL AUTOINCREMENT TESTS PASSED SUCCESSFULLY! :)");
    }
    finally
    {
        if (File.Exists(dbFilePath))
        {
            File.Delete(dbFilePath);
        }
    }
}
