const { Pool } = require('pg');

// Un Pool mantiene varias conexiones abiertas y las reutiliza.
// Es mucho más eficiente que abrir una conexión nueva por cada request.
const pool = new Pool({
  host: process.env.DB_HOST || 'localhost',
  port: process.env.DB_PORT || 5432,
  user: process.env.DB_USER || 'authuser',
  password: process.env.DB_PASSWORD || 'authpass',
  database: process.env.DB_NAME || 'authdb',
});

// Crea la tabla si no existe. Es idempotente: puedes correrla mil veces.
const initDb = async () => {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS users (
      id SERIAL PRIMARY KEY,
      name VARCHAR(100) NOT NULL,
      email VARCHAR(255) UNIQUE NOT NULL,
      password VARCHAR(255) NOT NULL,
      created_at TIMESTAMPTZ DEFAULT NOW()
    )
  `);
};

module.exports = { pool, initDb };