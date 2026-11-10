const express = require('express');
const { createProxyMiddleware } = require('http-proxy-middleware');
const rateLimit = require('express-rate-limit');
const dotenv = require('dotenv');
const { verifyToken } = require('./middleware/auth.middleware');

dotenv.config();

const app = express();
app.use(express.json());

if (!process.env.JWT_SECRET) {
  console.error('JWT_SECRET no está definido');
  process.exit(1);
}

// ── Rate limiting ─────────────────────────────────────────────────
// Máximo 100 requests por IP cada 15 minutos
const limiter = rateLimit({
  windowMs: 15 * 60 * 1000,
  max: 100,
  message: { error: 'Demasiadas solicitudes, intenta más tarde' }
});
app.use(limiter);

// ── Logging simple de requests ────────────────────────────────────
app.use((req, res, next) => {
  console.log(`[${new Date().toISOString()}] ${req.method} ${req.path}`);
  next();
});

// ── Verificación de JWT en todas las rutas ────────────────────────
app.use(verifyToken);

// ── Health check del gateway ──────────────────────────────────────
app.get('/health', (req, res) => {
  res.json({ status: 'ok', service: 'api-gateway' });
});

// ── Routing a microservicios ──────────────────────────────────────
const AUTH_SERVICE_URL = process.env.AUTH_SERVICE_URL || 'http://auth-service:3001';
const TASKS_SERVICE_URL = process.env.TASKS_SERVICE_URL || 'http://tasks-service:3002';
const NOTIFICATIONS_SERVICE_URL = process.env.NOTIFICATIONS_SERVICE_URL || 'http://notifications-service:3003';

// /api/auth/* → auth-service
app.use('/api/auth', createProxyMiddleware({
  target: AUTH_SERVICE_URL,
  changeOrigin: true,
  pathRewrite: { '^/api/auth': '/auth' },
  on: {
    error: (err, req, res) => {
      console.error('Auth service error:', err.message);
      res.status(503).json({ error: 'Auth service no disponible' });
    }
  }
}));

// /api/tasks/* → tasks-service
app.use('/api/tasks', createProxyMiddleware({
  target: TASKS_SERVICE_URL,
  changeOrigin: true,
  pathRewrite: { '^/api/tasks': '/tasks' },
  on: {
    error: (err, req, res) => {
      console.error('Tasks service error:', err.message);
      res.status(503).json({ error: 'Tasks service no disponible' });
    }
  }
}));

// /api/notifications/* → notifications-service
app.use('/api/notifications', createProxyMiddleware({
  target: NOTIFICATIONS_SERVICE_URL,
  changeOrigin: true,
  pathRewrite: { '^/api/notifications': '' },
  on: {
    error: (err, req, res) => {
      console.error('Notifications service error:', err.message);
      res.status(503).json({ error: 'Notifications service no disponible' });
    }
  }
}));

// ── 404 para rutas no definidas ───────────────────────────────────
app.use((req, res) => {
  res.status(404).json({ error: `Ruta ${req.path} no encontrada` });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`api-gateway corriendo en puerto ${PORT}`);
  console.log(`→ Auth service:          ${AUTH_SERVICE_URL}`);
  console.log(`→ Tasks service:         ${TASKS_SERVICE_URL}`);
  console.log(`→ Notifications service: ${NOTIFICATIONS_SERVICE_URL}`);
});