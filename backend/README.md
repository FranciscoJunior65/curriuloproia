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
./scripts/setup-env.ps1
# Edite backend/.env — único arquivo de configuração (localhost e servidor)

dotnet run --project src/CurriculosProIA.Api/CurriculosProIA.Api.csproj
```

A API carrega `backend/.env` (dev) ou `.env` na pasta do site (produção — o mesmo arquivo do publish).

Teste Supabase: `GET http://localhost:3000/api/test/supabase`

Porta padrão: **http://localhost:3000**

## Publicar no IIS / Plesk (Windows)

**Um único arquivo:** `backend/.env` — edite só ele. O publish leva o mesmo `.env` para o servidor.

```powershell
cd backend
copy ENV_EXAMPLE.env .env   # primeira vez
# Edite .env com todas as chaves

.\scripts\publish-production.ps1
```

Ou: `dotnet publish src/CurriculosProIA.Api/CurriculosProIA.Api.csproj -c Release -o ./publish`

Suba a pasta `publish/` para `api.curriculoproia.com.br` (contém `CurriculosProIA.Api.dll`, `web.config` e **`.env`**).

No Plesk: runtime **.NET 8**, reinicie o app pool após publicar.

Teste: `https://api.curriculoproia.com.br/api/test/env`

### Mercado Pago no `.env`

| `MERCADOPAGO_MODE` | Token |
|--------------------|-------|
| `test` | `MERCADOPAGO_ACCESS_TOKEN_TEST` |
| `production` | `MERCADOPAGO_ACCESS_TOKEN_PRODUCTION` |

O valor no `.env` só é usado se o admin não tiver modo salvo. O **token** sempre vem do `.env`.

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
