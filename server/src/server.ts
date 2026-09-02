import 'dotenv/config';
import { buildApp } from './app';

const app = buildApp();
const port = Number(process.env.PORT) || 3000;

app
  .listen({ port, host: '0.0.0.0' })
  .then(() => {
    console.log(`Server listening on port ${port}`);
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });
