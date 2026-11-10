const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const { pool } = require('../db');

const generateToken = (user) =>
  jwt.sign(
    { id: user.id, email: user.email },
    process.env.JWT_SECRET,
    { expiresIn: '24h' }
  );

const register = async (req, res) => {
  try {
    const { name, email, password } = req.body;

    if (!name || !email || !password)
      return res.status(400).json({ error: 'Todos los campos son requeridos' });

    const hashedPassword = await bcrypt.hash(password, 10);

    // $1, $2, $3 son parámetros: pg los escapa por ti.
    // NUNCA concatenes variables dentro del SQL (SQL injection).
    const result = await pool.query(
      `INSERT INTO users (name, email, password)
       VALUES ($1, $2, $3)
       RETURNING id, name, email`,
      [name, email, hashedPassword]
    );

    const user = result.rows[0];
    res.status(201).json({ token: generateToken(user), user });
  } catch (err) {
    // 23505 = código de PostgreSQL para "violación de restricción UNIQUE"
    if (err.code === '23505')
      return res.status(400).json({ error: 'El email ya está registrado' });

    console.error(err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
};

const login = async (req, res) => {
  try {
    const { email, password } = req.body;

    const result = await pool.query(
      'SELECT id, name, email, password FROM users WHERE email = $1',
      [email]
    );

    const user = result.rows[0];
    if (!user)
      return res.status(401).json({ error: 'Credenciales inválidas' });

    const valid = await bcrypt.compare(password, user.password);
    if (!valid)
      return res.status(401).json({ error: 'Credenciales inválidas' });

    res.json({
      token: generateToken(user),
      user: { id: user.id, name: user.name, email: user.email }
    });
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: 'Error interno del servidor' });
  }
};

const me = (req, res) => {
  res.json({ message: 'Endpoint protegido — pendiente' });
};

module.exports = { register, login, me };