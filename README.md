# CurriculosPro IA

Sistema de análise e otimização de currículos utilizando Inteligência Artificial.

## 🚀 Tecnologias

### Backend
- Node.js + Express.js
- Supabase (PostgreSQL)
- OpenAI API (GPT-4)
- JWT para autenticação
- Nodemailer para envio de emails
- Stripe para pagamentos

### Frontend
- Angular 19
- Angular Material
- Tailwind CSS
- RxJS

## 📋 Funcionalidades

- ✅ Análise de currículos com IA
- ✅ Geração de currículos melhorados em PDF
- ✅ Sistema de autenticação com verificação de email
- ✅ Sistema de créditos e compras
- ✅ Painel administrativo
- ✅ Histórico de compras e uso de créditos

## 🛠️ Instalação

### Backend

```bash
cd backend
npm install
cp ENV_EXAMPLE.env .env
# Configure as variáveis de ambiente no arquivo .env
npm start
```

### Frontend

```bash
cd frontend
npm install
npm start
```

## ⚙️ Configuração

### Variáveis de Ambiente (Backend)

Crie um arquivo `.env` na pasta `backend` com as seguintes variáveis:

```env
PORT=3000
JWT_SECRET=seu_secret_key_aqui
SUPABASE_URL=sua_url_supabase
SUPABASE_SERVICE_ROLE_KEY=sua_service_role_key
OPENAI_API_KEY=sua_chave_openai
USE_MOCK_AI=true
SMTP_HOST=seu_servidor_smtp
SMTP_PORT=587
EMAIL_SENDER=seu_email@exemplo.com
EMAIL_SENDER_PASSWORD=sua_senha
FRONTEND_URL=http://localhost:4200
```

### Banco de Dados

Execute os scripts SQL no Supabase:

1. `backend/CREATE_PURCHASES_TABLE.sql` - Cria tabelas de compras e créditos
2. `backend/ADD_USER_TYPE_COLUMN.sql` - Adiciona coluna de tipo de usuário

## 📝 Scripts Disponíveis

### Backend
- `npm start` - Inicia o servidor
- `npm run dev` - Inicia em modo desenvolvimento com nodemon

### Frontend
- `npm start` - Inicia o servidor de desenvolvimento
- `npm run build` - Gera build de produção

## 🔐 Autenticação

O sistema utiliza JWT para autenticação. Tokens expiram em 30 dias.

## 💳 Sistema de Créditos

- Cada crédito permite 1 análise + 1 geração de PDF
- Planos disponíveis:
  - Análise Única: R$ 9,90 (1 crédito)
  - Pacote 3 Análises: R$ 24,90 (3 créditos)

## 📊 Painel Admin

Acesse `/admin` para visualizar:
- Estatísticas gerais
- Vendas e compras
- Uso de créditos
- Usuários ativos

## 🧪 Modo Mock

Para testes sem consumir créditos reais:
- Configure `USE_MOCK_AI=true` no `.env`
- A compra mockada não requer token válido (apenas para testes)

## 📄 Licença

Este projeto é privado e proprietário.

## 👤 Autor

Francisco Junior

