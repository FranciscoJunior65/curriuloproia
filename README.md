# CurriculosPro IA

Sistema de análise e otimização de currículos utilizando Inteligência Artificial.

## 🚀 Tecnologias

### Backend (produção)
- **ASP.NET Core 8** (C#)
- Supabase (PostgreSQL)
- Google Gemini API
- JWT para autenticação
- MailKit (SMTP)
- Stripe / Mercado Pago

### Backend Node.js (backup)
- O projeto original em Node.js + Express está em `backend-node/` apenas como referência/backup.

### Frontend
- Angular 19
- Angular Material
- Tailwind CSS
- RxJS

## 📋 Funcionalidades

- ✅ Análise de currículos com IA
- ✅ Geração de currículos melhorados em PDF
- ✅ Sistema de autenticação com verificação de email
- ✅ Login com Google OAuth
- ✅ Sistema de créditos e compras
- ✅ Painel administrativo
- ✅ Histórico de compras e uso de créditos
- ✅ Simulador de entrevista com IA

## 🛠️ Instalação

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 18+ (apenas para o frontend Angular)

### Backend (.NET 8)

```bash
cd backend
cp ENV_EXAMPLE.env .env   # configure as variáveis
dotnet restore CurriculosProIA.sln
dotnet run --project src/CurriculosProIA.Api/CurriculosProIA.Api.csproj
```

A API sobe em **http://localhost:3000**. A solution usa camadas: **Domain → Repository → Service → App → Api** (ver `backend/README.md`).

- Swagger: http://localhost:3000/swagger
- Health: http://localhost:3000/api/health

### Frontend

```bash
cd frontend
npm install
npm start
```

O frontend em desenvolvimento aponta para `http://localhost:3000/api` (ver `frontend/src/environments/environment.ts`).

> Se `npm install` falhar com **EACCES** no cache do npm, use o `.npmrc` na raiz do repositório (cache local em `.npm-cache/`).

### Backend Node.js (opcional — backup)

```bash
cd backend-node
npm install
npm run setup
npm start
```

## ⚙️ Configuração

### Variáveis de Ambiente (Backend)

Crie um arquivo `.env` na pasta `backend` (use `ENV_EXAMPLE.env` como modelo):

```env
PORT=3000
JWT_SECRET=seu_secret_key_aqui
SUPABASE_URL=sua_url_supabase
SUPABASE_SERVICE_ROLE_KEY=sua_service_role_key
GEMINI_API_KEY=sua_chave_gemini
USE_MOCK_AI=false
SMTP_HOST=seu_servidor_smtp
SMTP_PORT=587
EMAIL_SENDER=seu_email@exemplo.com
EMAIL_SENDER_PASSWORD=sua_senha
FRONTEND_URL=http://localhost:4200
PAYMENT_PROVIDER=stripe
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
```

### Banco de Dados

Execute os scripts SQL no Supabase (pasta `backend/`):

1. `COMPLETE_SETUP.sql` ou scripts individuais conforme necessário
2. `CRIAR_TABELA_CONFIGURACOES.sql`

## 📝 Scripts

### Backend .NET
- `dotnet run --project src/CurriculosProIA.Api/CurriculosProIA.Api.csproj` — inicia a API
- `dotnet build CurriculosProIA.sln` — compila a solution

### Frontend
- `npm start` — desenvolvimento
- `npm run build` — build de produção

### VS Code
- Task **start:all** — sobe backend .NET + frontend Angular em paralelo

## 🔐 Autenticação

JWT com expiração de 30 dias. Header: `Authorization: Bearer <token>`.

## 💳 Sistema de Créditos

- Cada crédito permite 1 análise + 1 geração de PDF
- Planos: Análise Única, Pacote 3, Pacote 5, Currículo em Inglês

## 📊 Painel Admin

Acesse `/admin` no frontend para estatísticas, vendas e configuração de pagamentos.

## 🧪 Modo Mock

Configure `USE_MOCK_AI=true` no `.env` para testes sem consumir créditos da API Gemini.

## 📄 Licença

Este projeto é privado e proprietário.

## 👤 Autor

Francisco Junior
