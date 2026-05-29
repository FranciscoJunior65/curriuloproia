# CurriculosPro IA — API .NET 8

API em arquitetura em camadas (Clean Architecture).

## Solution

```
backend/
├── CurriculosProIA.sln
└── src/
    ├── CurriculosProIA.Domain/       # Entidades + Signatures (requests) + DTOs
    ├── CurriculosProIA.Repository/   # Interfaces e acesso Supabase
    ├── CurriculosProIA.Service/      # Infraestrutura (IA, email, pagamentos, PDF)
    ├── CurriculosProIA.App/          # Casos de uso (orquestração)
    └── CurriculosProIA.Api/          # Controllers HTTP + Program.cs
```

## Executar

```bash
cd backend
# Criar .env (ou copie as variáveis do backend-node/.env que já funciona)
./scripts/setup-env.ps1
# Edite .env: SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY (Supabase → Settings → API)

dotnet run --project src/CurriculosProIA.Api/CurriculosProIA.Api.csproj
```

A API carrega `.env` de `backend/.env` ou, em fallback, `backend-node/.env` (mesmo padrão da API Node).

Teste Supabase: `GET http://localhost:3000/api/test/supabase`

Porta padrão: **http://localhost:3000**

## Publicar no IIS / Plesk (Windows)

1. Mantenha suas chaves em `backend/app.env` (ou `backend/.env`) — **não vão para o Git** (`.gitignore`).
2. `dotnet publish src/CurriculosProIA.Api/CurriculosProIA.Api.csproj -c Release -o ./publish`  
   O publish **copia automaticamente** `backend/app.env` (ou `.env`) para a pasta `publish/` como `app.env`.
3. Copie a pasta `publish` inteira para o site `api.curriculoproia.com.br` (deve conter `CurriculosProIA.Api.dll`, `web.config` e **`app.env`**).
4. No Plesk: site ASP.NET Core, runtime **.NET 8**, pool **Sem código gerenciado** (ou integrado, conforme o módulo)
5. **Variáveis no servidor** — confira `app.env` na mesma pasta do `.dll` (FTP costuma preferir `app.env` em vez de `.env`):

   ```bash
   # macOS/Linux (opcional, se o publish não trouxe o arquivo)
   dotnet publish src/CurriculosProIA.Api/CurriculosProIA.Api.csproj -c Release -o ./publish
   ./scripts/copy-env-to-publish.sh ./publish
   ```

   ```powershell
   # Windows
   dotnet publish src/CurriculosProIA.Api/CurriculosProIA.Api.csproj -c Release -o ./publish
   .\scripts\copy-env-to-publish.ps1 -PublishDir ".\publish"
   ```

   Inclua `SERPAPI_KEY=...` no `backend/app.env` junto com Supabase, Gemini, etc.

   Alternativa: variáveis `SUPABASE_URL` e `SUPABASE_SERVICE_ROLE_KEY` no painel Plesk → ASP.NET Core → Variáveis de ambiente.

5. Defina `ENABLE_SWAGGER=true` e `ASPNETCORE_ENVIRONMENT=Production`
6. Teste: `https://api.curriculoproia.com.br/api/test/supabase`

Se `/api/health` responder mas Supabase falhar, o `.env` não está na pasta do site ou as chaves estão vazias.

## Camadas

| Camada | Responsabilidade |
|--------|------------------|
| **Domain** | `Entities/` (modelo de negócio), `Signatures/` (body POST com sufixo `Signature`), `Dtos/` (respostas) |
| **Repository** | `IUserProfileRepository`, `IPurchaseRepository`, etc. + `SupabaseService` |
| **Service** | `IJwtService`, `IEmailService`, `IAiService`, Stripe, Mercado Pago, PDF |
| **App** | `IAuthAppService`, `IAnalyzeAppService`, … + `SignatureToEntityMapper` |
| **Api** | Controllers finos → delegam para `I*AppService` |

## Convenções

- Requests HTTP POST: classes em `Domain.Signatures.*` com sufixo **`Signature`** (ex.: `RegisterSignature`)
- Persistência: **`Entities`** no Domain; mapeamento Signature → Entity no `App/Mappers`
- DI: `AddRepositories()`, `AddInfrastructureServices()`, `AddApplicationServices()` no `Program.cs`

## Backup Node.js

Código legado: `../backend-node/`
