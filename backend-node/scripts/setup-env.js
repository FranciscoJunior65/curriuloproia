import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(__dirname, '..');
const envPath = path.join(backendRoot, '.env');
const examplePath = path.join(backendRoot, 'ENV_EXAMPLE.env');

if (fs.existsSync(envPath)) {
  console.log('✅ backend/.env já existe — nada a fazer.');
  process.exit(0);
}

if (!fs.existsSync(examplePath)) {
  console.error('❌ ENV_EXAMPLE.env não encontrado em', backendRoot);
  process.exit(1);
}

let content = fs.readFileSync(examplePath, 'utf8');
content = content.replace(/^USE_MOCK_AI=false/m, 'USE_MOCK_AI=true');

fs.writeFileSync(envPath, content, 'utf8');
console.log('✅ Criado backend/.env a partir de ENV_EXAMPLE.env');
console.log('   Edite o arquivo com SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY e chaves de IA.');
console.log('   USE_MOCK_AI=true já vem ativado para desenvolvimento local.');
