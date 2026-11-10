const jwt = require('jsonwebtoken');

const verifyToken = (req, res, next) => {
  // Rutas públicas que no necesitan autenticación
  const publicRoutes = [
    { path: '/api/auth/register', method: 'POST' },
    { path: '/api/auth/login', method: 'POST' },
    { path: '/health', method: 'GET' },
  ];

  const isPublic = publicRoutes.some(
    route => req.path === route.path && req.method === route.method
  );

  if (isPublic) return next();

  // Extraer token del header Authorization: Bearer <token>
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];

  if (!token)
    return res.status(401).json({ error: 'Token requerido' });

  try {
    const decoded = jwt.verify(token, process.env.JWT_SECRET);
    // Pasamos el userId a los servicios internos via header
    req.headers['x-user-id'] = decoded.id;
    req.headers['x-user-email'] = decoded.email;
    next();
  } catch (err) {
    return res.status(401).json({ error: 'Token inválido o expirado' });
  }
};

module.exports = { verifyToken };