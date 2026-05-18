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
  console.warn('⚠️ [load-env] Arquivo .env não encontrado:', envPath);
  console.warn('   Execute em backend/: npm run setup');
  if (!process.env.JWT_SECRET) {
    process.env.JWT_SECRET = 'dev-jwt-secret-altere-em-producao';
  }
  if (!process.env.SESSION_SECRET) {
    process.env.SESSION_SECRET = process.env.JWT_SECRET;
  }
  if (!process.env.USE_MOCK_AI) {
    process.env.USE_MOCK_AI = 'true';
  }
} else {
  const hasSupabase = !!(process.env.SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY);
  console.log('✅ [load-env] .env carregado de:', envPath);
  if (!hasSupabase) {
    console.warn('⚠️ [load-env] SUPABASE_URL ou SUPABASE_SERVICE_ROLE_KEY não definidos no .env');
  } else {
    const url = String(process.env.SUPABASE_URL).trim();
    const key = String(process.env.SUPABASE_SERVICE_ROLE_KEY).trim();
    const placeholderUrl =
      url.includes('seu-projeto.supabase.co') || url.includes('your-project.supabase.co');
    const placeholderKey =
      key === 'sua_service_role_key_aqui' || key.startsWith('sua_') || key.length < 40;
    if (placeholderUrl || placeholderKey) {
      console.warn('⚠️ [load-env] Supabase ainda com valores de exemplo no .env');
      console.warn('   Edite backend/.env com URL e Service Role Key reais (Supabase → Settings → API)');
    }
  }
}
