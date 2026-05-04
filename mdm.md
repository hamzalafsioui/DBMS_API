# I Built a Mini Database Engine from Scratch in C# — Here's How a Real DBMS Works Under the Hood

_From SQL string to disk — the complete journey of a query through a database I built myself._

---

## Why I Built This

Every developer has typed `SELECT * FROM users` hundreds of times. But what actually happens when you hit Enter? Where does the data come from? How does the database know which rows match your `WHERE` clause?

I wanted to find out — not by reading Wikipedia, but by _building it myself_. So I created **a complete mini Database Management System (DBMS) from scratch in C#**, covering everything from SQL parsing to writing data to disk.

This post explains what I built, how each piece works, and — more importantly — **what it teaches you about how real databases like PostgreSQL or MySQL work internally**.

---

## What Is a DBMS, Really?

A Database Management System is software that:

1. **Understands** a query language (SQL)
2. **Executes** operations on structured data (tables and rows)
3. **Persists** data reliably to disk
4. **Manages concurrent access** from multiple clients

When you use MySQL or SQLite, all four of these are happening invisibly. My project implements each one at a primitive — but real — level.

---

## The Big Picture: System Architecture

Before diving in, here's how every component connects:

```
 Frontend (Browser)
      │  HTTP POST /query
      ▼
 REST API  (ASP.NET Core)
      │  TCP custom protocol
      ▼
 TCP Server  ←  CLI Client
      │
      ▼
 SqlParser       ← "SELECT * FROM users WHERE age > 20;"
      │             becomes a structured object
      ▼
 SqlExecution    ← routes to the right handler
      │
      ▼
 BufferPoolManager  ← does the actual work in memory
      │
      ▼
 DiskManager     ← reads/writes JSON files to disk
      │
      ▼
 databases_list/mydb.json
```

Every SQL query travels this exact path — from a raw text string to data on disk — and back. Let's walk through each layer.

---

## Layer 1: SQL Parsing — Turning Text Into Meaning

**File:** `Database/SqlParser.cs`

The first thing any database does when it receives a query is **parse** it: break the raw string into something the computer can reason about.

### The Problem

A human can read:

```sql
SELECT username, age FROM users WHERE age > 20 AND salary < 1500.0;
```

And immediately understand: _"get the username and age columns from the users table, but only for rows where age is greater than 20 and salary is below 1500."_

A computer sees a stream of characters. The parser's job is to bridge that gap.

### How My Parser Works

**Step 1 — Normalization:** Before anything else, I use a regular expression to insert spaces around operators like `>=`, `<=`, `!=`, `=`, `>`, `<`. This means a user can type `age=24` or `age = 24` — both work correctly.

**Step 2 — Tokenization:** The normalized string is split by whitespace into an array of tokens:

```
["SELECT", "username,age", "FROM", "users", "WHERE", "age", ">", "20", "AND", "salary", "<", "1500.0"]
```

**Step 3 — Routing:** The first token (`SELECT`, `INSERT`, `CREATE`, etc.) determines which specialized parsing method runs next.

### The Output: A Structured Object

After parsing, instead of a string, we have a clean C# object:

```
MethodType    → "SELECT"
Table         → "users"
Keys          → ["username", "age"]
WhereConditions → [
  { Column: "age", Operator: ">", Value: "20", LogicalOperator: "" },
  { Column: "salary", Operator: "<", Value: "1500.0", LogicalOperator: "AND" }
]
```

This structured object is everything the rest of the system needs. The SQL string is never looked at again.

### The Trickiest Part: WHERE Clause Parsing

Parsing `WHERE age > 20 AND salary < 1500.0` means handling:

- Standard 3-token conditions: `column operator value`
- 2-token special cases: `col IS NULL`
- 3-token special cases: `col IS NOT NULL`
- Logical operators (`AND`, `OR`) between conditions

Each condition becomes a `WhereCondition` object with its logical connector. This chain is evaluated later during query execution.

---

## Layer 2: SQL Execution — The Router

**File:** `Database/SqlExecution.cs`

Once we have a parsed `SqlParser` object, the `SqlExecution` class decides _what to do with it_. It's a **thin routing layer** — clean and simple:

```csharp
switch (parser.MethodType)
{
    "SELECT" → HandleSelect()
    "INSERT" → HandleInsert()
    "CREATE" → HandleCreateTable()
    "UPDATE" → HandleUpdate()
    "DELETE" → HandleDelete()
    "DROP"   → HandleDropTable()
}
```

Each handler calls the appropriate method on the `BufferPoolManager` and wraps the result. That's it. The simplicity here is intentional — separation of concerns means the router doesn't think about _how_ to execute, only _which_ method to call.

---

## Layer 3: The Buffer Pool Manager — The Heart of It All

**File:** `Database/BufferPoolManager.cs`

This is the most important component. It's where data actually lives and where all operations happen. If this were a real production database, this class would be thousands of lines long.

### The Core Idea: Work In Memory, Flush to Disk

Real databases almost never read from disk on every query — that would be far too slow. Instead, they keep data in **memory pages** (buffers) and only touch disk when necessary.

My implementation follows this same pattern with a **dirty page flag**:

1. When the system starts, all data is loaded from disk into memory (`_page`).
2. Every READ operation (`SELECT`) works entirely from memory — no disk I/O.
3. Every WRITE operation (`INSERT`, `UPDATE`, `DELETE`, `CREATE`, `DROP`) modifies memory and sets `_isDirty = true`.
4. After any write, `ReadOrWriteOnDisk()` is called — it checks the dirty flag and flushes the updated data to disk.

Simple, but it mirrors exactly how PostgreSQL's buffer pool works conceptually.

### WHERE Clause Evaluation

The most interesting logic in the `BufferPoolManager` is how it filters rows for `SELECT`, `UPDATE`, and `DELETE`.

**The problem:** given a row like `{ "username": "hamza", "age": 24, "salary": 100.0 }` and a condition like `age > 20`, how do we compare them correctly?

There are hidden pitfalls:

- `age` might be stored as an `int`, but when the value was loaded from a JSON file, it came back as a `JsonElement` — a raw JSON token, not a typed C# value.
- The condition value `"20"` is a string from parsing. We need to convert it to an `int` before comparing.
- Comparing an `int` to a `double` requires careful type coercion.

My `CompareValues()` function handles all these cases: it unwraps `JsonElement` values, converts types when necessary, and then performs the actual comparison.

### Type-Aware Operations

Every time a value is read from user input (SQL is always text), it must be converted to the correct type based on the table schema:

- `INT` → `int.Parse()`
- `FLOAT` → `double.Parse()`
- `VARCHAR` → strip surrounding single quotes

This `ParseType()` function is called constantly — during `INSERT`, `UPDATE`, and `WHERE` evaluation. It's the glue between the text world of SQL and the typed world of C#.

---

## Layer 4: The Disk Manager — Making Data Permanent

**File:** `Database/DiskManager.cs`

Every database needs persistence: when you restart the server, your data should still be there.

In a production database, this involves complex file formats (B-trees, heap files, WAL logs). In my project, I keep it simple: **each database is one JSON file**.

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
    "users": [{ "username": "hamza", "age": 24, "salary": 100.0 }]
  }
}
```

The `DiskManager` has three functions: `Read()`, `Write()`, and `Exists()`. That's the entire API. The `BufferPoolManager` is the only caller — the rest of the system never touches disk directly.

This separation is important. If I wanted to switch from JSON files to a binary format, I'd only change one file.

---

## Layer 5: The TCP Server — Serving Multiple Clients

**File:** `Database/ConnectionHandler.cs`

The database server doesn't just run queries — it listens for connections over the network, allowing multiple clients to connect simultaneously.

### Custom Protocol

Rather than using HTTP or a standard database protocol, I designed a simple text-based protocol:

```
db:>testdb          ← client says which database to use
con:>1              ← server confirms connection
query:>SELECT * FROM users;   ← client sends a query
messages:>[...]     ← server sends the result
is_json:>1          ← tells the client how to interpret the result
```

Each message is a set of `key:>value` pairs separated by newlines. It's like a minimal HTTP, built from scratch.

### Concurrency

Every client connection runs in its own `Task.Run()` — a lightweight async task. This allows the server to handle multiple simultaneous connections without blocking.

---

## Layer 6: Three Ways to Interact

One of the goals of this project was to show that the same database engine can be accessed in multiple ways.

### Option A: CLI Client (REPL)

The CLI client (`Client/DbClient.cs`) gives you an interactive shell — just like `psql` for PostgreSQL or `mysql` for MySQL:

```
testdb >>> CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);
OK: New Table Created !

testdb >>> INSERT INTO users (username,age,salary) VALUES ('hamza',24,100.0);
OK: New Row Has Been Inserted !

testdb >>> SELECT * FROM users WHERE age > 20;
[
  {
    "username": "hamza",
    "age": 24,
    "salary": 100.0
  }
]
```

### Option B: REST API

The REST API (`Api/DbClientService.cs`) bridges HTTP and TCP. It's an ASP.NET Core Minimal API with a single endpoint:

```
POST /query
{
  "dbName": "testdb",
  "query": "SELECT * FROM users;"
}
```

The API connects to the TCP server for each request, sends the query, deserializes the result, and returns JSON. This is the pattern used by cloud databases like PlanetScale or Neon — your client talks HTTP, but the actual database speaks its own native protocol.

### Option C: Web Frontend

The frontend is a browser-based SQL editor — a dark-themed HTML/CSS/JS interface where you can type SQL, hit Execute, and see results rendered as a formatted table. No frameworks, just vanilla web.

---

## Tracing a Query From Start to Finish

Let's follow `SELECT * FROM users WHERE age > 20;` all the way through:

1. **User types the query** in the CLI or web frontend
2. **TCP client sends:** `query:>SELECT * FROM users WHERE age > 20;\n`
3. **ConnectionHandler** receives the message and extracts the query string
4. **SqlParser** tokenizes and parses it:
   - `MethodType = "SELECT"`
   - `Table = "users"`
   - `WhereConditions = [{ age > 20 }]`
5. **SqlExecution** sees `SELECT` → calls `HandleSelect()`
6. **BufferPoolManager.SelectRows()**:
   - Reads all rows from the `users` table in memory
   - For each row, evaluates `EvaluateCondition({age: 24}, {age > 20})`
   - `ParseType("20", "INT")` → `20`
   - `CompareValues(24, 20)` → `24 > 20` → `true` ✓
   - Returns only matching rows
7. **ConnectionHandler** serializes the result and sends back:
   `messages:>[{"username":"hamza","age":24,"salary":100.0}]\nis_json:>1\n`
8. **Client** detects `is_json=1`, pretty-prints the JSON

The entire round-trip for a simple query.

---

## What I Learned

Building this taught me things that years of _using_ databases never did:

- **SQL parsing is harder than it looks.** Handling edge cases like `IS NULL`, quoted string values with commas, and operators without spaces requires careful, defensive code.
- **The buffer pool concept is fundamental.** Every real database — Oracle, Postgres, MySQL — has some version of the dirty page / flush mechanism. Mine is a toy version, but the concept is identical.
- **Type coercion is invisible until it breaks.** When a database loads JSON from disk, numeric types come back as `JsonElement`. Comparing them to user-input strings requires explicit type handling at every stage.
- **Separation of concerns is not optional.** Each layer (Parser → Executor → Buffer → Disk) has one job. This makes the system testable, understandable, and extensible.

---

## What's Next

This is Version 1.0. Future versions will explore:

- **Indexing:** B-tree or hash indexes for O(log n) lookups instead of full table scans
- **Transactions:** ACID guarantees with rollback support
- **JOIN support:** Combining multiple tables in a single query
- **Aggregate functions:** `COUNT()`, `SUM()`, `AVG()`
- **Binary storage format:** Replacing JSON files with a more efficient page-based binary format

---

## Further Reading

If this sparked your curiosity about database internals, I highly recommend:

📖 **"Database Internals" by Alex Petrov** — The best book I know on storage engines, B-trees, and distributed database concepts. It's what pushed me to actually build this.

---

## Project Links

The full source code is on GitHub — including all C# source files, sample SQL queries, and documentation.

⭐ If you found this useful, feel free to star the repo or leave a comment below. I'd love to hear from developers who are also exploring systems programming and computer science fundamentals.

---

_Built with C#, .NET 8, ASP.NET Core, and a lot of curiosity about how things work under the hood._
