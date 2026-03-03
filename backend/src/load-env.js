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
const result = dotenv.config({ path: envPath });

if (result.error) {
  console.error('❌ [load-env] Arquivo .env não encontrado ou inválido:', envPath);
  console.error('   No servidor, crie o arquivo em backend/.env com SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY');
} else {
  const hasSupabase = !!(process.env.SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY);
  console.log('✅ [load-env] .env carregado de:', envPath);
  if (!hasSupabase) console.warn('⚠️ [load-env] SUPABASE_URL ou SUPABASE_SERVICE_ROLE_KEY não definidos no .env');
}
