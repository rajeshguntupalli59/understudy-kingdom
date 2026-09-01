import 'dotenv/config';
import knexLib from 'knex';
import config from '../../knexfile.js';

const env = process.env.NODE_ENV === 'test' ? 'test' : 'development';

export default knexLib(config[env]);
