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
cp ENV_EXAMPLE.env .env   # se necessário
dotnet run --project src/CurriculosProIA.Api/CurriculosProIA.Api.csproj
```

Porta padrão: **http://localhost:3000**

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
