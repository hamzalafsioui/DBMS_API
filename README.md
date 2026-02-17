# 🗄️ C# DBMS : Database Management System

A simple, educational Database Management System built from scratch in **C#**. This project demonstrates how a real database engine works internally from SQL parsing to data persistence on disk with multiple ways to interact with it (CLI, REST API, Web UI).

> **Version 1.0** — Supports `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE TABLE`, `DROP TABLE`, and `WHERE` clause.

---

## ✨ Features

| Feature                    | Description                                                  |
| -------------------------- | ------------------------------------------------------------ |
| **SQL Parser**             | Tokenizes and parses raw SQL queries into structured objects |
| **SQL Execution Engine**   | Routes parsed queries to the appropriate handler             |
| **Buffer Pool Manager**    | In-memory data management with dirty page tracking           |
| **Disk Manager**           | Persistent storage using JSON serialization                  |
| **TCP Server**             | Multi-client TCP server with custom protocol on port `9090`  |
| **Interactive CLI Client** | Console REPL for sending SQL queries to the server           |
| **REST API**               | ASP.NET Core Minimal API bridge between HTTP and TCP         |
| **Web Frontend**           | Browser-based SQL editor with table rendering                |

---

## 🏗️ Architecture Overview

```
┌──────────────┐     HTTP POST /query     ┌──────────────┐
│   Frontend   │ ──────────────────────>  │   REST API   │
│  (HTML/JS)   │                          │  (ASP.NET)   │
└──────────────┘                          └──────┬───────┘
                                                 │ TCP (custom protocol)
┌──────────────┐     TCP (custom protocol)       │
│  CLI Client  │ ───────────────────────────────>│
└──────────────┘                                 ▼
                                          ┌───────────────────┐
                                          │   TCP Server      │
                                          │ ConnectionHandler │
                                          └────────┬──────────┘
                                                   │
                                              ┌────▼──────┐
                                              │SqlParser  │ → Tokenize & parse SQL
                                              └────┬──────┘
                                                   │ SqlParser object
                                              ┌────▼───────────┐
                                              │ SqlExecution   │ → Route to handler
                                              └────┬───────────┘
                                                   │
                                          ┌────────▼──────────────┐
                                          │  BufferPoolManager    │ → In-memory operations
                                          │  (dirty page tracking)│
                                          └────────┬──────────────┘
                                                   │ flush on write
                                              ┌────▼──────────┐
                                              │ DiskManager   │ → JSON file I/O
                                              └───────────────┘
                                                   │
                                              databases_list/
                                                mydb.json
```

---

## 📁 Project Structure

```
DBMS_API/
├── Database/                  # 🔧 Core Database Server
│   ├── Program.cs             #   Entry point — starts TCP server
│   ├── ConnectionHandler.cs   #   TCP server & client connection manager
│   ├── SqlParser.cs           #   SQL query tokenizer & parser
│   ├── SqlExecution.cs        #   Query execution router
│   ├── BufferPoolManager.cs   #   In-memory data + dirty page tracking
│   ├── DiskManager.cs         #   JSON read/write to disk
│   └── Database.csproj        #   Project file
│
├── Client/                    # 💻 CLI Client
│   ├── Program.cs             #   Entry point — prompts for DB name
│   ├── DbClient.cs            #   TCP client with REPL loop
│   └── Client.csproj          #   Project file
│
├── Api/                       # 🌐 REST API Bridge
│   ├── Program.cs             #   ASP.NET Minimal API (POST /query)
│   ├── DbClientService.cs     #   TCP client service for API
│   └── Api.csproj             #   Project file
│
├── Frontend/                  # 🎨 Web UI
│   ├── index.html             #   Main page
│   ├── style.css              #   Dark theme styling
│   └── app.js                 #   Frontend logic (fetch + table render)
│
├── databases_list/            # 📂 Data storage (JSON files)
├── queries.sql                # 📝 Sample SQL queries
├── presentation.txt           # 📄 Project presentation (French)
├── DBMS.sln                   # Visual Studio solution file
└── README.md                  # This file
```

---

## ⚙️ Requirements

- **.NET 8.0 SDK** or later

---

## 🚀 How to Use

### 1. Clone the Repository

```bash
git clone <repository-url>
cd DBMS_API
```

### 2. Start the Database Server

```bash
cd Database
dotnet run
```

You will see:

```
Starting DBMS Server...
Server Is Listening on: localhost:9090
```

### 3. Option A : Use the CLI Client

Open a **new terminal**:

```bash
cd Client
dotnet run
```

Enter a database name when prompted (e.g. `testdb`), then start typing SQL commands:

```sql
testdb >>> CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);
OK: New Table Created !

testdb >>> INSERT INTO users (username,age,salary) VALUES ('hamza',24,100.0);
OK: New Row Has Been Inserted !

testdb >>> SELECT * FROM users;
[
  {
    "username": "hamza",
    "age": 24,
    "salary": 100.0
  }
]

testdb >>> exit
```

### 3. Option B : Use the REST API + Web UI

Start the API (in a **new terminal**, while the server is running):

```bash
cd Api
dotnet run
```

The API will start on `http://localhost:5232`.

Then open `Frontend/index.html` in your browser to use the **web-based SQL editor**.

You can also call the API directly:

```bash
curl -X POST http://localhost:5232/query \
  -H "Content-Type: application/json" \
  -d '{"dbName": "testdb", "query": "SELECT * FROM users;"}'
```

---

## 📋 Supported SQL Commands

| Command                     | Syntax                                   | Example                                                              |
| --------------------------- | ---------------------------------------- | -------------------------------------------------------------------- |
| **CREATE TABLE**            | `CREATE TABLE name (col TYPE, ...);`     | `CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);`        |
| **INSERT INTO**             | `INSERT INTO name (cols) VALUES (vals);` | `INSERT INTO users (username,age,salary) VALUES ('hamza',24,100.0);` |
| **SELECT \***               | `SELECT * FROM name;`                    | `SELECT * FROM users;`                                               |
| **SELECT columns**          | `SELECT col1,col2 FROM name;`            | `SELECT username,age FROM users;`                                    |
| **SELECT WHERE**            | `SELECT * FROM name WHERE condition;`    | `SELECT * FROM users WHERE age > 20;`                                |
| **SELECT WHERE (compound)** | `... WHERE cond1 AND/OR cond2;`          | `SELECT * FROM users WHERE age > 20 AND salary < 1500.0;`            |
| **UPDATE**                  | `UPDATE name SET col=val [WHERE ...];`   | `UPDATE users SET age = 25 WHERE username = 'hamza';`                |
| **UPDATE (all rows)**       | `UPDATE name SET col=val;`               | `UPDATE users SET salary = 2000.0;`                                  |
| **DELETE**                  | `DELETE FROM name [WHERE ...];`          | `DELETE FROM users WHERE age < 25;`                                  |
| **DELETE (all rows)**       | `DELETE FROM name;`                      | `DELETE FROM users;`                                                 |
| **DROP TABLE**              | `DROP TABLE name;`                       | `DROP TABLE users;`                                                  |

---

## 🔍 WHERE Clause

The `WHERE` clause can be used with `SELECT`, `UPDATE`, and `DELETE`.

### Comparison Operators

| Operator      | Meaning                  |
| ------------- | ------------------------ |
| `=`           | Equal to                 |
| `!=`          | Not equal to             |
| `>`           | Greater than             |
| `<`           | Less than                |
| `>=`          | Greater than or equal to |
| `<=`          | Less than or equal to    |
| `IS NULL`     | Value is null            |
| `IS NOT NULL` | Value is not null        |

### Logical Operators

| Operator | Meaning                             |
| -------- | ----------------------------------- |
| `AND`    | Both conditions must be true        |
| `OR`     | At least one condition must be true |

### Examples

```sql
SELECT * FROM users WHERE age = 23;
SELECT * FROM users WHERE age > 20 AND salary < 1300.0;
SELECT username FROM users WHERE age < 20 OR salary > 1300.0;
UPDATE users SET age = 25 WHERE username = 'hamza';
UPDATE users SET age = 30, salary = 3000.0 WHERE age > 25;
DELETE FROM users WHERE salary < 1000.0;
```

---

## 📊 Data Types

| Type      | Description                       | Example   |
| --------- | --------------------------------- | --------- |
| `VARCHAR` | String values (use single quotes) | `'hamza'` |
| `INT`     | Integer values                    | `24`      |
| `FLOAT`   | Decimal / floating-point values   | `100.0`   |

---

## 🔌 Custom TCP Protocol

The server and clients communicate using a simple text-based protocol:

**Format:** `key:>value\n`

| Message             | Direction       | Example                                                             |
| ------------------- | --------------- | ------------------------------------------------------------------- |
| Database connection | Client → Server | `db:>testdb\n`                                                      |
| Connection response | Server → Client | `message:>Connected to testdb Successfully !\nis_json:>0\ncon:>1\n` |
| SQL query           | Client → Server | `query:>SELECT * FROM users;\n`                                     |
| Query response      | Server → Client | `messages:>[...]\nis_json:>1\n`                                     |

---

## 📝 Sample Queries

A comprehensive set of sample queries is available in [queries.sql](./queries.sql), covering:

- Table creation (`CREATE TABLE`)
- Data insertion (`INSERT INTO`)
- Basic `SELECT` queries
- `WHERE` with all comparison operators
- Compound conditions with `AND` / `OR`

---

## ⚠️ Notes

- This is an **educational project** — not all SQL features are supported.
- Data is persisted as **JSON files** inside the `databases_list/` folder.
- Each database is stored as a single `.json` file with `Tables` (schema) and `Rows` (data).
- The server supports **multiple concurrent client connections** via `Task.Run`.

---

## 📜 License

This project is for **educational purposes**.
