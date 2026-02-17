# 📖 DBMS Project : Detailed Explanation

This document explains the internal architecture of the DBMS project: what each component does, how they connect together, and the key functions inside each one.

---

## Table of Contents 

1. [How the System Works (Overview)](#1-how-the-system-works-overview)
2. [SqlParser — SQL Query Parsing](#2-sqlparser--sql-query-parsing)
3. [SqlExecution — Query Routing Engine](#3-sqlexecution--query-routing-engine)
4. [BufferPoolManager — In-Memory Data Management](#4-bufferpoolmanager--in-memory-data-management)
5. [DiskManager — Data Persistence](#5-diskmanager--data-persistence)
6. [ConnectionHandler — TCP Server](#6-connectionhandler--tcp-server)
7. [Client (DbClient) — CLI Interface](#7-client-dbclient--cli-interface)
8. [API (DbClientService) — REST Bridge](#8-api-dbclientservice--rest-bridge)
9. [Frontend — Web Interface](#9-frontend--web-interface)
10. [Data Flow: Full Query Lifecycle](#10-data-flow-full-query-lifecycle)

---

## 1. How the System Works (Overview)

The project is a **simplified database engine** that follows the same layered architecture as real-world databases:

```
User Input (SQL string)
    │
    ▼
┌───────────┐
│ SqlParser │  ← Converts raw SQL text into a structured object
└────┬──────┘
     │
     ▼
┌───────────────┐
│ SqlExecution  │  ← Routes the parsed query to the correct handler
└────┬──────────┘
     │
     ▼
┌────────────────────┐
│ BufferPoolManager  │  ← Performs the operation on in-memory data
└────┬───────────────┘
     │
     ▼
┌─────────────┐
│ DiskManager │  ← Reads/writes the data to JSON files on disk
└─────────────┘
```

**Key idea:** The user writes SQL → it gets parsed → routed → executed in memory → flushed to disk.

---

## 2. SqlParser — SQL Query Parsing

**File:** `Database/SqlParser.cs` 

The `SqlParser` is the **first component** that touches user input. It takes a raw SQL string and converts it into a structured object with properties that other components can use.

### How It Works

1. **Normalization:** The constructor uses `Regex.Replace` to add spaces around operators (`>=`, `<=`, `!=`, `=`, `>`, `<`) so that tokenization works correctly even when users type `age=24` without spaces.

2. **Tokenization:** The normalized query is split by spaces into an array of tokens.

3. **Routing:** The first token determines the query type (`SELECT`, `INSERT`, `CREATE`, `DROP`, `DELETE`, `UPDATE`), and the parser calls the corresponding `Parse*` method.

### Properties (Output)

| Property          | Type                   | Description                                              |
| ----------------- | ---------------------- | -------------------------------------------------------- |
| `MethodType`      | `string`               | The SQL command type: `SELECT`, `INSERT`, `CREATE`, etc. |
| `Table`           | `string`               | The target table name                                    |
| `Keys`            | `List<string>`         | Column names (meanings vary by query type)               |
| `KeyTypes`        | `List<string>`         | Column data types (only used by `CREATE TABLE`)          |
| `Values`          | `List<string>`         | Values to insert or update                               |
| `WhereConditions` | `List<WhereCondition>` | Parsed WHERE conditions                                  |

### Helper Class: WhereCondition

```csharp
public class WhereCondition
{
    public string Column { get; set; }          // e.g. "age"
    public string Operator { get; set; }        // e.g. ">", "=", "IS NULL"
    public string Value { get; set; }           // e.g. "20"
    public string LogicalOperator { get; set; } // "AND" or "OR" (empty for first condition)
}
```

### Key Functions

#### `ParseSelect(string[] tokens)`

Parses queries like `SELECT * FROM users WHERE age > 20;`

- Uses regex `SELECT (.*?) FROM (\w+)(?: WHERE (.*))?` to extract columns, table, and WHERE clause.
- If columns are `*`, `Keys` stays empty (meaning "all columns").
- If a WHERE clause is detected, calls `ParseWhereConditions()`.

#### `ParseInsert(string[] tokens)`

Parses queries like `INSERT INTO users (username,age,salary) VALUES ('hamza',24,100.0);`

- Uses regex to extract table name, column list, and values list.
- Calls `ParseValues()` for proper comma-separated value extraction (handles quoted strings).
- Validates that column count matches value count.

#### `ParseCreateTable(string[] tokens)`

Parses queries like `CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);`

- Extracts column names into `Keys` and column types into `KeyTypes`.

#### `ParseDropTable(string[] tokens)`

Parses `DROP TABLE users;` — simply extracts the table name from token index 2.

#### `ParseDelete(string[] tokens)`

Parses queries like `DELETE FROM users WHERE age > 20;`

- Extracts table name and optional WHERE clause.

#### `ParseUpdate(string[] tokens)`

Parses queries like `UPDATE users SET age = 25, salary = 2000.0 WHERE username = 'hamza';`

- Extracts table name, calls `ParseSetClause()` for SET assignments, then `ParseWhereConditions()` if present.

#### `ParseSetClause(string setClause)`

Parses `age = 25, salary = 2000.0` into key-value pairs.

- Handles quoted strings (won't split on commas inside quotes).
- Splits each assignment by `=` to extract column name and new value.

#### `ParseWhereConditions(string[] tokens, int startIndex)`

Parses conditions like `age > 20 AND salary < 1000.0`

- Iterates through tokens starting after `WHERE`.
- Detects logical operators (`AND`, `OR`) between conditions.
- Handles special operators: `IS NULL` (2 tokens), `IS NOT NULL` (3 tokens).
- Standard conditions are 3 tokens: `column operator value`.

#### `ParseValues(string valuesStr)`

Parses comma-separated values like `'hamza',24,100.0`

- Uses character-by-character iteration to avoid splitting on commas inside quoted strings.

---

## 3. SqlExecution — Query Routing Engine

**File:** `Database/SqlExecution.cs` 

The `SqlExecution` class is the **execution engine**. It acts as a **router** that takes a parsed `SqlParser` object and calls the correct method on the `BufferPoolManager`.

### How It Works

1. The constructor creates a `BufferPoolManager` instance for the specified database.
2. The `Execute()` method uses C# pattern matching (`switch` expression) on `parser.MethodType` to route to the appropriate handler.
3. Each handler calls the corresponding `BufferPoolManager` method and returns a result message.

### Key Functions

#### `Execute(SqlParser parser)`

The central routing function:

```
"SELECT" → HandleSelect()   → returns List<Dictionary<string, object>>
"CREATE" → HandleCreateTable() → returns "OK: New Table Created !"
"INSERT" → HandleInsert()   → returns "OK: New Row Has Been Inserted !"
"DROP"   → HandleDropTable() → returns "OK: Table Dropped !"
"DELETE" → HandleDelete()   → returns "OK: N Row(s) Deleted !"
"UPDATE" → HandleUpdate()   → returns "OK: N Row(s) Updated !"
```

#### Handler Methods

Each handler is a thin wrapper:

- `HandleInsert()` → calls `_buffer.InsertRow(parser)` → returns confirmation string
- `HandleSelect()` → calls `_buffer.SelectRows(parser)` → returns list of rows
- `HandleDelete()` → calls `_buffer.DeleteRows(parser)` → returns deleted count
- `HandleUpdate()` → calls `_buffer.UpdateRows(parser)` → returns updated count
- `HandleCreateTable()` → calls `_buffer.CreateTable(parser)` → returns confirmation
- `HandleDropTable()` → calls `_buffer.DropTable(parser)` → returns confirmation

#### `Results` Property

Holds the last execution result (can be a string message or a list of row dictionaries). Used by `ConnectionHandler` to serialize and send back to the client.

---

## 4. BufferPoolManager — In-Memory Data Management

**File:** `Database/BufferPoolManager.cs` 

This is the **core engine** of the DBMS. It manages all data in memory and handles the actual CRUD operations. It uses a **dirty page tracking** mechanism: modifications are made in memory first, then flushed to disk when needed.

### How It Works

1. **Initialization:** On construction, it checks if the database already exists on disk. If yes, it loads the data into memory (`_page`). If not, it creates a new empty `DatabasePage` and writes it to disk.

2. **Dirty Page Tracking:** A boolean flag `_isDirty` tracks whether in-memory data has been modified. After any write operation (INSERT, UPDATE, DELETE, CREATE, DROP), the flag is set to `true`, and `ReadOrWriteOnDisk()` is called to flush the changes.

3. **Data Structure:** All data lives in a `DatabasePage` object with two dictionaries:
   - `Tables`: maps table name → schema (column name → column type)
   - `Rows`: maps table name → list of rows (each row is a dictionary of column → value)

### Key Functions

#### `ReadOrWriteOnDisk()`

The **dirty page flush** mechanism:

- If `_isDirty` is `true`, writes the current in-memory page to disk via `DiskManager`, then resets the flag.
- Always returns the current page (used by read operations to ensure data is fresh).

#### `SelectRows(SqlParser parser)`

Executes a SELECT query:

1. Checks if the table exists, throws exception if not.
2. Gets all rows from the table.
3. If `WhereConditions` are present, calls `FilterRowsByWhereConditions()` to filter.
4. If specific columns are requested (`Keys` not empty), projects only those columns.
5. Returns the result as `List<Dictionary<string, object>>`.

#### `InsertRow(SqlParser parser)`

Executes an INSERT query:

1. Validates the table exists.
2. Iterates through the table schema (not just provided columns) to build a complete row.
3. For provided columns: parses the value to the correct type using `ParseType()`.
4. For missing columns: inserts `null`.
5. Adds the row to the in-memory page, marks dirty, and flushes.

#### `CreateTable(SqlParser parser)`

Creates a new table:

1. Builds a schema dictionary from `parser.Keys` (column names) and `parser.KeyTypes` (column types).
2. Adds the schema to `_page.Tables`.
3. Marks dirty and flushes.

#### `DropTable(SqlParser parser)`

Drops a table:

1. Removes the table from `_page.Tables` (schema).
2. Removes the table from `_page.Rows` (data).
3. Marks dirty and flushes.

#### `DeleteRows(SqlParser parser)`

Executes a DELETE query:

1. If no WHERE conditions: clears all rows from the table.
2. If WHERE conditions exist: iterates all rows, keeps rows that **don't** match the conditions (inverse logic).
3. Returns the number of deleted rows.

#### `UpdateRows(SqlParser parser)`

Executes an UPDATE query:

1. Validates that all SET columns exist in the schema.
2. Prepares typed update values using `ParseType()`.
3. Iterates all rows. If WHERE conditions exist, only updates matching rows. If no WHERE, updates all rows.
4. Applies each SET assignment to matching rows.
5. Returns the number of updated rows.

#### `EvaluateAllConditions(row, conditions, tableSchema)`

Evaluates all WHERE conditions for a single row:

- Iterates through conditions, applying `AND`/`OR` logical operators sequentially.
- Returns `true` if the row matches all conditions.

#### `FilterRowsByWhereConditions(rows, conditions, tableSchema)`

Filters a list of rows based on WHERE conditions:

- Iterates all rows, keeps only those where `EvaluateCondition()` returns `true` for all conditions (respecting AND/OR).

#### `EvaluateCondition(row, condition, tableSchema)`

Evaluates a single WHERE condition against a row:

1. Checks if the column exists in the row and schema.
2. Handles `IS NULL` and `IS NOT NULL` operators.
3. For standard operators (`=`, `!=`, `>`, `<`, `>=`, `<=`): parses the condition value to the correct type, then calls `CompareValues()`.

#### `CompareValues(value1, value2)`

Compares two values with type awareness:

- Handles `JsonElement` deserialization (values loaded from disk come as `JsonElement`).
- Handles numeric type mismatches (int vs double comparison).
- Supports `int`, `double`, and `string` comparisons.
- Falls back to string comparison if types don't match.

#### `ParseType(fieldValue, fieldType)`

Converts a string value to the correct C# type based on the schema:

- `"INT"` → `int.Parse()`
- `"FLOAT"` → `double.Parse()`
- `"VARCHAR"` → trims surrounding single quotes

---

## 5. DiskManager — Data Persistence

**File:** `Database/DiskManager.cs` 

The `DiskManager` handles reading and writing database files to disk. Each database is stored as a single JSON file.

### How It Works

- Databases are stored in the `databases_list/` folder.
- Each database is one file: `databases_list/{dbname}.json`.
- Uses `System.Text.Json` for serialization/deserialization.

### Key Functions

#### `Read(string database)`

Reads a database from disk:

- Constructs the file path: `databases_list/{database}.json`
- Reads the JSON content and deserializes it into a `DatabasePage` object.

#### `Write(string database, DatabasePage data)`

Writes a database to disk:

- Serializes the `DatabasePage` to formatted JSON (indented for readability).
- Writes to `databases_list/{database}.json`.

#### `Exists(string database)`

Checks if a database file exists on disk.

### DatabasePage Class

```csharp
public class DatabasePage
{
    // Schema: table_name → { column_name → column_type }
    public Dictionary<string, Dictionary<string, string>> Tables { get; set; }

    // Data: table_name → list of rows (each row = { column_name → value })
    public Dictionary<string, List<Dictionary<string, object>>> Rows { get; set; }
}
```

**Example JSON on disk** (`databases_list/testdb.json`):

```json
{
  "Tables": {
    "users": {
      "username": "VARCHAR",
      "age": "INT",
      "salary": "FLOAT"
    }
  },
  "Rows": {
    "users": [
      {
        "username": "hamza",
        "age": 24,
        "salary": 100.0
      }
    ]
  }
}
```

---

## 6. ConnectionHandler — TCP Server

**File:** `Database/ConnectionHandler.cs` (204 lines)

The `ConnectionHandler` is the **TCP server** that listens for client connections. It uses a custom text-based protocol for communication.

### Custom Protocol Format

Messages are encoded as `key:>value\n` pairs. For example:

```
db:>testdb
```

```
query:>SELECT * FROM users;
```

### How It Works

1. The server starts listening on `127.0.0.1:9090`.
2. When a client connects, the server reads the **database name** from the first message.
3. If valid, it creates a `SqlExecution` instance for that database and enters a read loop.
4. For each query received, it:
   - Parses the SQL with `SqlParser`
   - Executes it with `SqlExecution`
   - Serializes the result and sends it back
5. Each client runs in its own `Task` for concurrency.

### Key Functions

#### `Run()`

Entry point — starts the TCP listener and accepts clients in an infinite loop. Each accepted client is dispatched to `HandleConnection()` via `Task.Run()`.

#### `ReadDb(TcpClient client)`

Reads the initial database connection request from a new client:

- Deserializes the message to extract `db` key.
- If valid: sends success response with `con:>1`.
- If invalid: sends error response with `con:>0`.

#### `HandleConnection(TcpClient client, string db)`

Main client session handler:

- Creates a `SqlExecution` instance for the database.
- Enters a read loop: reads query, parses it, executes it, sends response.
- Supports multiple queries separated by `;` in a single message.
- Wraps the response with `messages` (result) and `is_json` (flag: 1 if result is a list/dict, 0 otherwise).

#### `Serialize(Dictionary<string, object> obj)` / `Deserialize(string body)`

Convert between dictionaries and the custom protocol format `key:>value\n`.

---

## 7. Client (DbClient) — CLI Interface

**File:** `Client/DbClient.cs` (186 lines)

The `DbClient` provides an **interactive REPL** (Read-Eval-Print Loop) for sending SQL queries to the server from the command line.

### How It Works

1. User starts the client and enters a database name.
2. The client connects via TCP to `localhost:9090`.
3. It sends a `db:>dbname` message and waits for confirmation.
4. Then it enters the REPL loop: prompts `dbname >>>`, reads SQL, sends to server, prints the response.
5. Type `exit` to disconnect.

### Key Functions

#### `Connect(string database)`

Establishes TCP connection and sends the database name. Returns `true` if server confirms connection.

#### `SendQuery(string query)`

Sends a query to the server and displays the response. If the response is JSON (`is_json` = 1), it pretty-prints it with indentation.

#### `RunRepl(string database)`

The main REPL loop — calls `Connect()` first, then loops reading input and calling `SendQuery()`.

---

## 8. API (DbClientService) — REST Bridge

**File:** `Api/DbClientService.cs` (108 lines) and `Api/Program.cs` (43 lines)

The API is an **ASP.NET Core Minimal API** that bridges HTTP requests to the TCP database server. It exposes a single endpoint:

```
POST /query
Body: { "dbName": "testdb", "query": "SELECT * FROM users;" }
```

### How It Works

1. The API receives an HTTP POST request.
2. `DbClientService.ExecuteQueryAsync()` opens a **new TCP connection** to the database server for each request.
3. It sends the database name, waits for confirmation, then sends the query.
4. Deserializes the response and returns it as JSON to the HTTP client.
5. CORS is enabled to allow the Frontend to make cross-origin requests.

### Key Function

#### `ExecuteQueryAsync(string dbName, string query)`

Full lifecycle of a query via TCP:

1. Opens a TCP connection to `127.0.0.1:9090`.
2. Sends `db:>dbName` → reads connection response.
3. Sends `query:>query` → reads query response.
4. If `is_json` = 1, deserializes the result as a list of dictionaries.
5. Returns the result (or an error object if something fails).

---

## 9. Frontend — Web Interface

**Files:** `Frontend/index.html`, `Frontend/style.css`, `Frontend/app.js`

A **browser-based SQL editor** with a dark theme (GitHub-inspired). It communicates with the REST API.

### How It Works

1. User enters a database name and SQL query in the form.
2. Clicking "Execute Query" sends a `POST /query` request to the API at `http://localhost:5232`.
3. Results are displayed:
   - **Lists of rows** → rendered as an HTML table with headers.
   - **Strings** (e.g. "OK: New Row Has Been Inserted !") → displayed as text.
   - **Errors** → shown in red.
4. The database name is persisted in `localStorage` between sessions.

### Features

- Loading spinner during query execution.
- Status messages (success in green, error in red).
- Responsive dark-themed design.
- Results displayed as formatted tables.

---

## 10. Data Flow: Full Query Lifecycle

Here is the complete lifecycle of a query from user input to result:

### Example: `SELECT * FROM users WHERE age > 20;`

```
Step 1: User types the query
         │
Step 2: CLI Client sends via TCP → "query:>SELECT * FROM users WHERE age > 20;\n"
         │
Step 3: ConnectionHandler receives and deserializes
         │
Step 4: SqlParser("SELECT * FROM users WHERE age > 20;")
         ├── MethodType = "SELECT"
         ├── Table = "users"
         ├── Keys = [] (empty = all columns)
         └── WhereConditions = [
               { Column: "age", Operator: ">", Value: "20", LogicalOperator: "" }
             ]
         │
Step 5: SqlExecution.Execute(parser)
         └── MethodType is "SELECT" → calls HandleSelect()
             └── calls _buffer.SelectRows(parser)
         │
Step 6: BufferPoolManager.SelectRows(parser)
         ├── ReadOrWriteOnDisk() → flushes any pending writes, returns data
         ├── Gets all rows from data.Rows["users"]
         ├── FilterRowsByWhereConditions(rows, conditions, schema)
         │   └── For each row:
         │       └── EvaluateCondition(row, {Column:"age", Op:">", Value:"20"}, schema)
         │           ├── Gets row["age"] → e.g. 24
         │           ├── ParseType("20", "INT") → 20
         │           └── CompareValues(24, 20) > 0 → true ✓
         └── Returns filtered List<Dictionary<string, object>>
         │
Step 7: Result is sent back through SqlExecution → ConnectionHandler
         │
Step 8: ConnectionHandler serializes result → sends via TCP
         └── "messages:>[{"username":"hamza","age":24,"salary":100.0}]\nis_json:>1\n"
         │
Step 9: CLI Client receives, detects is_json=1, pretty-prints JSON
```

---

## How Components Connect Together

```
┌─────────────────────────────────────────────────────────┐
│                    DATABASE SERVER                      │
│                                                         │
│   ConnectionHandler                                     │
│        │                                                │
│        │  receives raw SQL string                       │
│        ▼                                                │
│   SqlParser (creates structured object)                 │
│        │                                                │
│        │  passes SqlParser object                       │
│        ▼                                                │
│   SqlExecution (routes to correct handler)              │
│        │                                                │
│        │  calls appropriate method                      │
│        ▼                                                │
│   BufferPoolManager (executes in memory)                │
│        │                                                │
│        │  reads/writes when dirty                       │
│        ▼                                                │
│   DiskManager (JSON file I/O)                           │
│        │                                                │
│        ▼                                                │
│   databases_list/mydb.json                              │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

**ConnectionHandler** → creates `SqlParser` and `SqlExecution`
**SqlExecution** → creates and uses `BufferPoolManager`
**BufferPoolManager** → creates and uses `DiskManager`
**DiskManager** → reads/writes `DatabasePage` to/from JSON files
