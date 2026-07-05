import { cpSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const browserDir = join(root, 'dist', 'browser');
const deployDir = join(root, 'dist', 'deploy');

function assertBrowserBuild() {
  const indexPath = join(browserDir, 'index.html');
  try {
    statSync(indexPath);
  } catch {
    console.error('ERRO: dist/browser/index.html não encontrado.');
    console.error('Execute antes: npm run build');
    process.exit(1);
  }
}

assertBrowserBuild();

rmSync(deployDir, { recursive: true, force: true });
mkdirSync(deployDir, { recursive: true });
cpSync(browserDir, deployDir, { recursive: true });

const files = readdirSync(deployDir);
const jsFiles = files.filter((name) => name.endsWith('.js'));
const indexHtml = readFileSync(join(deployDir, 'index.html'), 'utf8');
const referencedJs = [...indexHtml.matchAll(/script src="([^"]+\.js)"/g)].map((m) => m[1]);
const missingJs = referencedJs.filter((name) => !jsFiles.includes(name));

if (missingJs.length > 0) {
  console.error('ERRO: index.html referencia JS que não está no pacote:');
  missingJs.forEach((name) => console.error(`  - ${name}`));
  process.exit(1);
}

writeFileSync(
  join(deployDir, 'LEIA-ANTES-DO-UPLOAD.txt'),
  [
    'DEPLOY DO FRONTEND (Plesk / IIS)',
    '',
    'Envie TODO o conteúdo DESTA PASTA (dist/deploy) para a RAIZ do site:',
    '  curriculoproia.com.br',
    '',
    'NÃO envie só o index.html.',
    'NÃO envie a pasta browser/ aninhada — os .js devem ficar na raiz do site.',
    'NÃO envie a pasta dist/ — use dist/deploy/ (conteúdo já achatado).',
    '',
    `Arquivos JS neste pacote (${jsFiles.length}):`,
    ...jsFiles.map((name) => `  - ${name}`),
    '',
    'Após o upload: apague no servidor main-*.js e polyfills-*.js antigos (hashes diferentes).',
    'Depois limpe cache do navegador (Ctrl+Shift+R).',
  ].join('\n'),
  'utf8'
);

console.log('');
console.log('Pacote de deploy pronto em: frontend/dist/deploy/');
console.log(`Arquivos: ${files.length} (+ LEIA-ANTES-DO-UPLOAD.txt)`);
console.log('Envie TODO o conteúdo de dist/deploy/ para a raiz do site no Plesk.');
console.log('');
