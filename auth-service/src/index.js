const app = require('./app');
const { initDb } = require('./db');

if (!process.env.JWT_SECRET) {
  console.error('JWT_SECRET no está definido');
  process.exit(1);
}

const PORT = process.env.PORT || 3001;

initDb()
  .then(() => {
    app.listen(PORT, () => {
      console.log(`auth-service corriendo en puerto ${PORT}`);
    });
  })
  .catch((err) => {
    console.error('No se pudo conectar a la base de datos:', err.message);
    process.exit(1);
  });