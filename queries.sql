-- =========================
-- CREATE TABLE 
-- =========================

CREATE TABLE users (username VARCHAR,age INT,salary FLOAT);

CREATE TABLE products (product_name VARCHAR,price FLOAT,stock INT);

CREATE TABLE orders (order_id INT,username VARCHAR,order_year INT);

-- =========================
-- INSERT DATA
-- =========================

INSERT INTO users (username, age, salary) VALUES ('hamza', 28, 2100.50);
INSERT INTO users (username, age, salary) VALUES ('ayoub', 35, 1800.75);
INSERT INTO users (username, age, salary) VALUES ('ahmed', 22, 950.00);
INSERT INTO users (username, age, salary) VALUES ('ali', 41, 3200.10);

INSERT INTO products (product_name, price, stock) VALUES ('Laptop', 1200.99, 15);
INSERT INTO products (product_name, price, stock) VALUES ('Phone', 699.49, 30);
INSERT INTO products (product_name, price, stock) VALUES ('Tablet', 399.00, 20);

INSERT INTO orders (order_id, username, order_year) VALUES (1, 'hamza', 2022);
INSERT INTO orders (order_id, username, order_year) VALUES (2, 'ahmed', 2023);
INSERT INTO orders (order_id, username, order_year) VALUES (3, 'ali', 2024);

-- =========================
-- BASIC SELECT
-- =========================

SELECT * FROM users;
SELECT * FROM products;
SELECT * FROM orders;

SELECT username, age FROM users;
SELECT username FROM users;
SELECT product_name, price FROM products;

-- =========================
-- WHERE 
-- =========================

SELECT * FROM users WHERE age = 22;
SELECT * FROM users WHERE age > 30;
SELECT * FROM users WHERE age >= 28;
SELECT * FROM users WHERE salary < 2000.00;
SELECT * FROM users WHERE salary <= 1800.75;
SELECT * FROM users WHERE username = 'hamza';
SELECT * FROM users WHERE username != 'hamza';

SELECT * FROM products WHERE price < 500;
SELECT * FROM products WHERE stock >= 20;

SELECT * FROM orders WHERE order_year = 2023;

-- =========================
-- AND CONDITIONS
-- =========================

SELECT * FROM users WHERE age > 25 AND salary > 2000;
SELECT username, age FROM users WHERE age >= 28 AND age < 40;

SELECT * FROM products WHERE price > 400 AND stock < 25;

-- =========================
-- OR CONDITIONS
-- =========================

SELECT * FROM users WHERE age < 25 OR salary < 1000;
SELECT * FROM users WHERE username = 'hamza' OR username = 'ayoub';

SELECT * FROM products WHERE price < 500 OR stock > 25;

-- =========================
-- COMPLEX CONDITIONS
-- =========================

SELECT username, salary FROM users WHERE age >= 30 AND salary <= 3000;

SELECT product_name, stock FROM products WHERE price > 600 AND stock >= 15;

