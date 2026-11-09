const request = require('supertest');
const app = require('../src/app');

//se inicializa la base de datos antes de correr los tests
const { pool, initDb } = require('../src/db');

beforeAll(async () => {
  await initDb();
});

beforeEach(async () => {
  await pool.query('TRUNCATE TABLE users RESTART IDENTITY');
});

afterAll(async () => {
  await pool.end();
});

describe('Auth Service', () => {

  describe('GET /health', () => {
    it('debe responder 200 con status ok', async () => {
      const res = await request(app).get('/health');
      expect(res.statusCode).toBe(200);
      expect(res.body.status).toBe('ok');
      expect(res.body.service).toBe('auth-service');
    });
  });

  describe('POST /auth/register', () => {
    it('debe registrar un usuario nuevo', async () => {
      const res = await request(app)
        .post('/auth/register')
        .send({ name: 'Test User', email: 'test@test.com', password: '123456' });

      expect(res.statusCode).toBe(201);
      expect(res.body.token).toBeDefined();
      expect(res.body.user.email).toBe('test@test.com');
      // Nunca debe devolver la contraseña
      expect(res.body.user.password).toBeUndefined();
    });

    it('debe rechazar si faltan campos', async () => {
      const res = await request(app)
        .post('/auth/register')
        .send({ email: 'test@test.com' }); // falta name y password

      expect(res.statusCode).toBe(400);
      expect(res.body.error).toBeDefined();
    });

    it('debe rechazar email duplicado', async () => {
      // Primer registro
      await request(app)
        .post('/auth/register')
        .send({ name: 'User', email: 'duplicate@test.com', password: '123456' });

      // Segundo registro con mismo email
      const res = await request(app)
        .post('/auth/register')
        .send({ name: 'User 2', email: 'duplicate@test.com', password: '654321' });

      expect(res.statusCode).toBe(400);
    });
  });

  describe('POST /auth/login', () => {
    beforeEach(async () => {
      // Crear usuario antes de cada test de login
      await request(app)
        .post('/auth/register')
        .send({ name: 'Login User', email: 'login@test.com', password: 'password123' });
    });

    it('debe hacer login con credenciales correctas', async () => {
      const res = await request(app)
        .post('/auth/login')
        .send({ email: 'login@test.com', password: 'password123' });

      expect(res.statusCode).toBe(200);
      expect(res.body.token).toBeDefined();
    });

    it('debe rechazar contraseña incorrecta', async () => {
      const res = await request(app)
        .post('/auth/login')
        .send({ email: 'login@test.com', password: 'wrongpassword' });

      expect(res.statusCode).toBe(401);
    });
  });
});