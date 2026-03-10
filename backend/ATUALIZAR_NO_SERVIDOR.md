# Atualizar o backend no servidor

Siga um dos fluxos abaixo conforme a forma que você usa para publicar o backend.

---

## Opção 1: Servidor com Git (recomendado)

Se o código no servidor veio de um repositório Git (ex.: você faz `git clone` no servidor):

1. **No seu PC:** faça commit e push das alterações (se ainda não fez):
   ```bash
   git add .
   git commit -m "Atualizações do backend (meus dados, CPF, admin free credits, etc)"
   git push origin main
   ```
   (troque `main` pelo nome do branch que o servidor usa)

2. **No servidor (SSH):** entre na pasta do backend e atualize:
   ```bash
   cd /caminho/para/CurriculoProIA/backend
   git pull origin main
   npm install
   ```
   Reinicie o processo do Node:
   - **Se usar PM2:** `pm2 restart curriculospro-backend` (ou o nome do seu processo)
   - **Se usar systemd:** `sudo systemctl restart curriculospro-backend`
   - **Se rodar direto:** pare com Ctrl+C e suba de novo: `npm start`

---

## Opção 2: Copiar arquivos (sem Git no servidor)

Se você sobe o backend por FTP, rsync ou outro meio:

1. **No seu PC:** copie toda a pasta `backend` para o servidor (mantendo a estrutura).
2. **No servidor:** na pasta do backend, instale dependências e reinicie:
   ```bash
   cd /caminho/para/backend
   npm install
   pm2 restart all
   ```
   (ou o comando que você usa para reiniciar a API)

---

## O que conferir após atualizar

- **Variáveis de ambiente:** o arquivo `.env` no servidor deve ter as mesmas variáveis (Supabase, JWT, Stripe, e-mail, etc.). Não sobrescreva o `.env` do servidor com o do seu PC.
- **Migrações de banco:** se houve mudança de estrutura (ex.: novos campos em `perfis_usuarios`), rode no Supabase os scripts SQL que ainda não rodou:
  - `ADICIONAR_CPF_PERFIS_USUARIOS.sql`
  - `ADICIONAR_CAMPOS_PERFIL_USUARIO.sql`
- **Porta e proxy:** confirme que a porta (ex.: 3000) e o proxy reverso (Nginx/Apache) continuam apontando para o backend correto.

---

## Script de exemplo (bash, no servidor)

Se no servidor você usa Linux e Git, pode salvar como `atualizar-backend.sh` na pasta do backend:

```bash
#!/bin/bash
set -e
cd "$(dirname "$0")"
git pull origin main
npm install
pm2 restart curriculospro-backend
echo "Backend atualizado e reiniciado."
```

Depois: `chmod +x atualizar-backend.sh` e execute com `./atualizar-backend.sh` quando quiser atualizar.
