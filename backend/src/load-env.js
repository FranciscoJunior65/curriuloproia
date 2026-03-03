/**
 * Carrega o .env do diretório backend antes de qualquer outro módulo.
 * Assim o PM2 pode iniciar da raiz do repositório que o Supabase será configurado.
 */
import dotenv from 'dotenv';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(__dirname, '..');
const envPath = path.join(backendRoot, '.env');
dotenv.config({ path: envPath });
