using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Postgrest.Exceptions;
using Supabase;
using static Postgrest.Constants;

namespace CurriculosProIA.Repository.Implementations;

public class SupabaseService : IAppDataStore, ISupabaseConnectionTester
{
    private readonly Client? _client;
    private readonly ILogger<SupabaseService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly string? _supabaseUrl;
    private readonly string? _supabaseServiceKey;
    private readonly bool _isPlaceholder;
    private bool _initialized;

    public bool IsConfigured => _client != null;

    private static readonly Dictionary<string, string> ProfileUpdateKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "nome",
        ["email"] = "email",
        ["cpf"] = "cpf",
        ["date_of_birth"] = "data_nascimento",
        ["city"] = "cidade",
        ["country"] = "pais",
        ["plan"] = "plano",
        ["last_analysis"] = "ultima_analise",
        ["updated_at"] = "atualizado_em",
        ["email_verified"] = "email_verificado",
        ["verification_code"] = "codigo_verificacao",
        ["verification_code_expires_at"] = "codigo_verificacao_expira_em",
        ["user_type"] = "tipo_usuario",
        ["password_hash"] = "hash_senha"
    };

    public SupabaseService(IConfiguration configuration, ILogger<SupabaseService> logger)
    {
        _logger = logger;

        _supabaseUrl = configuration["SUPABASE_URL"]?.Trim();
        _supabaseServiceKey = configuration["SUPABASE_SERVICE_ROLE_KEY"]?.Trim();

        var hasUrl = !string.IsNullOrEmpty(_supabaseUrl);
        var hasKey = !string.IsNullOrEmpty(_supabaseServiceKey);

        var isPlaceholderUrl = hasUrl &&
            (_supabaseUrl!.Contains("seu-projeto.supabase.co", StringComparison.OrdinalIgnoreCase) ||
             _supabaseUrl.Contains("your-project.supabase.co", StringComparison.OrdinalIgnoreCase));
        var isPlaceholderKey = hasKey &&
            (_supabaseServiceKey == "sua_service_role_key_aqui" ||
             _supabaseServiceKey!.StartsWith("sua_", StringComparison.Ordinal) ||
             _supabaseServiceKey.Length < 40);

        _isPlaceholder = isPlaceholderUrl || isPlaceholderKey;

        if (!hasUrl || !hasKey)
        {
            _logger.LogWarning(
                "Supabase não configurado. Variáveis SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY são necessárias. URL: {HasUrl}, Key: {HasKey}",
                hasUrl,
                hasKey);
        }
        else if (_isPlaceholder)
        {
            _logger.LogWarning(
                "Supabase com credenciais de exemplo — substitua no backend/.env pelos valores reais do painel Supabase.");
        }
        else
        {
            _logger.LogInformation("Supabase configurado corretamente");
            _client = new Client(_supabaseUrl!, _supabaseServiceKey!, new SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = false
            });
        }
    }

    public SupabaseConnectionTestResult GetConfigurationStatus()
    {
        var hasUrl = !string.IsNullOrEmpty(_supabaseUrl);
        var hasKey = !string.IsNullOrEmpty(_supabaseServiceKey);

        if (!hasUrl || !hasKey)
        {
            return new SupabaseConnectionTestResult(
                Configured: false,
                Success: false,
                Message: "Supabase não configurado. Defina SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY no .env (backend/.env ou backend-node/.env).");
        }

        if (_isPlaceholder)
        {
            return new SupabaseConnectionTestResult(
                Configured: false,
                Success: false,
                Message: "Credenciais de exemplo no .env — substitua pelos valores reais do painel Supabase (Settings → API).");
        }

        if (_client == null)
        {
            return new SupabaseConnectionTestResult(
                Configured: false,
                Success: false,
                Message: "Cliente Supabase não inicializado.");
        }

        return new SupabaseConnectionTestResult(
            Configured: true,
            Success: true,
            Message: "Supabase configurado.");
    }

    public async Task<SupabaseConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var status = GetConfigurationStatus();
        if (!status.Configured)
        {
            return status;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client!
                .From<PerfilUsuarioRow>()
                .Select("id")
                .Limit(5)
                .Get(cancellationToken);

            var count = response.Models?.Count ?? 0;
            return new SupabaseConnectionTestResult(
                Configured: true,
                Success: true,
                Message: "Conexão com Supabase OK.",
                ProfileCount: count);
        }
        catch (PostgrestException ex) when (ex.Message.Contains("PGRST", StringComparison.Ordinal) ||
                                             ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return new SupabaseConnectionTestResult(
                Configured: true,
                Success: true,
                Message: "Conexão com Supabase OK.",
                Warning: "Tabela perfis_usuarios não encontrada ou sem permissão. Execute os scripts SQL de migração.",
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao testar conexão Supabase");
            return new SupabaseConnectionTestResult(
                Configured: true,
                Success: false,
                Message: "Erro ao consultar Supabase.",
                Error: ex.Message);
        }
    }

    private void EnsureConfigured()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Supabase não configurado");
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_client == null || _initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static bool IsNotFoundException(Exception ex) =>
        ex is PostgrestException pg && (pg.Message.Contains("PGRST116", StringComparison.Ordinal) ||
                                        pg.StatusCode == (int)System.Net.HttpStatusCode.NotFound);

    /// <summary>Postgrest C# não aceita bool em Filter — usa string compatível com PostgREST.</summary>
    private static string BoolCriterion(bool value) => value ? "true" : "false";

    public async Task<int> GetAvailableCreditsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var count = await _client
                .From<CreditoRow>()
                .Filter("id_usuario", Operator.Equals, userId)
                .Filter("usado", Operator.Equals, BoolCriterion(false))
                .Count(CountType.Exact);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular créditos disponíveis");
            return 0;
        }
    }

    public async Task<UserProfile?> MapProfileToEnglishAsync(
        PerfilUsuarioRow? profile,
        CancellationToken cancellationToken = default)
    {
        if (profile == null)
        {
            return null;
        }

        var credits = await GetAvailableCreditsAsync(profile.Id, cancellationToken);
        var dataNasc = profile.DataNascimento;

        return new UserProfile
        {
            Id = profile.Id,
            Email = profile.Email,
            Name = profile.Nome ?? string.Empty,
            Cpf = profile.Cpf,
            DateOfBirth = FormatDateOfBirth(dataNasc),
            City = profile.Cidade,
            Country = profile.Pais,
            Credits = credits,
            Plan = profile.Plano,
            CreatedAt = profile.CriadoEm,
            LastAnalysis = profile.UltimaAnalise,
            UpdatedAt = profile.AtualizadoEm,
            EmailVerified = profile.EmailVerificado ?? false,
            VerificationCode = profile.CodigoVerificacao,
            VerificationCodeExpiresAt = profile.CodigoVerificacaoExpiraEm,
            UserType = profile.TipoUsuario ?? "cliente",
            PasswordHash = profile.HashSenha
        };
    }

    public Purchase? MapPurchaseToEnglish(CompraRow? purchase)
    {
        if (purchase == null)
        {
            return null;
        }

        return new Purchase
        {
            Id = purchase.Id,
            UserId = purchase.IdUsuario,
            PlanId = purchase.IdPlano,
            PlanName = purchase.NomePlano,
            CreditsAmount = purchase.QuantidadeCreditos ?? 0,
            Price = purchase.Preco,
            Currency = purchase.Moeda ?? "BRL",
            Status = purchase.Status ?? "concluida",
            PaymentMethod = purchase.MetodoPagamento,
            PaymentId = purchase.IdPagamento,
            CreatedAt = purchase.CriadoEm,
            UpdatedAt = purchase.AtualizadoEm,
            ParentPurchaseId = purchase.IdCompraPai,
            ServiceType = purchase.TipoServico ?? "analysis_plan",
            CouponId = purchase.IdCupom,
            CouponName = purchase.NomeCupom,
            DiscountPercent = purchase.PorcentagemDescontoAplicado,
            OriginalPrice = purchase.PrecoOriginal
        };
    }

    public string NormalizeCpf(string? cpf)
    {
        if (cpf == null)
        {
            return string.Empty;
        }

        var digits = Regex.Replace(cpf, @"\D", string.Empty);
        return digits.Length > 11 ? digits[..11] : digits;
    }

    public async Task<bool> CouponAlreadyUsedByCpfAsync(
        string couponId,
        string cpf,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(couponId) || string.IsNullOrEmpty(cpf))
        {
            return false;
        }

        var cpfNorm = NormalizeCpf(cpf);
        if (cpfNorm.Length != 11)
        {
            return false;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CupomUsoRow>()
                .Select("id")
                .Filter("id_cupom", Operator.Equals, couponId)
                .Filter("cpf_normalizado", Operator.Equals, cpfNorm)
                .Get();

            return response.Models.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar uso de cupom");
            return false;
        }
    }

    public async Task RegisterCouponUseAsync(
        string couponId,
        string cpf,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(couponId) || string.IsNullOrEmpty(cpf))
        {
            return;
        }

        var cpfNorm = NormalizeCpf(cpf);
        if (cpfNorm.Length != 11)
        {
            return;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            await _client
                .From<CupomUsoInsert>()
                .Insert(new CupomUsoInsert
                {
                    IdCupom = couponId,
                    CpfNormalizado = cpfNorm
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar uso de cupom");
        }
    }

    public async Task<CupomRow?> GetCouponByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim().ToUpperInvariant();
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CupomRow>()
                .Select("id, nome, porcentagem_desconto, ativo")
                .Filter("nome", Operator.ILike, trimmed)
                .Filter("ativo", Operator.Equals, BoolCriterion(true))
                .Get();

            return response.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar cupom");
            return null;
        }
    }

    public async Task<CouponValidationResult> ValidateCouponAsync(
        string code,
        string? cpf = null,
        CancellationToken cancellationToken = default)
    {
        var coupon = await GetCouponByCodeAsync(code, cancellationToken);
        if (coupon == null)
        {
            return new CouponValidationResult
            {
                Valid = false,
                Message = "Cupom inválido ou inativo."
            };
        }

        if (cpf != null && !string.IsNullOrWhiteSpace(cpf))
        {
            var cpfNorm = NormalizeCpf(cpf);
            if (cpfNorm.Length != 11)
            {
                return new CouponValidationResult
                {
                    Valid = false,
                    Message = "CPF inválido. Informe os 11 dígitos."
                };
            }

            var alreadyUsed = await CouponAlreadyUsedByCpfAsync(coupon.Id, cpfNorm, cancellationToken);
            if (alreadyUsed)
            {
                return new CouponValidationResult
                {
                    Valid = false,
                    Message = "Este cupom já foi utilizado por este CPF."
                };
            }
        }

        return new CouponValidationResult
        {
            Valid = true,
            Coupon = new Coupon
            {
                Id = coupon.Id,
                Nome = coupon.Nome,
                PorcentagemDesconto = Convert.ToDouble(coupon.PorcentagemDesconto ?? 0)
            }
        };
    }

    private static Credit MapCreditToEnglish(CreditoRow credit)
    {
        return new Credit
        {
            Id = credit.Id,
            PurchaseId = credit.IdCompra,
            UserId = credit.IdUsuario,
            Used = credit.Usado ?? false,
            UsedAt = credit.UsadoEm,
            ActionType = credit.TipoAcao,
            ResumeFileName = credit.NomeArquivoCurriculo,
            SiteId = credit.IdSiteVagas,
            CreatedAt = credit.CriadoEm
        };
    }

    public async Task<UserProfile> GetOrCreateUserProfileAsync(
        string userId,
        string email,
        string name = "",
        string? passwordHash = null,
        bool emailVerified = false,
        string? verificationCode = null,
        string? cpf = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var existingResponse = await _client!
                .From<PerfilUsuarioRow>()
                .Select("*")
                .Filter("id", Operator.Equals, userId)
                .Get();

            var existing = existingResponse.Models.FirstOrDefault();
            if (existing != null)
            {
                return (await MapProfileToEnglishAsync(existing, cancellationToken))!;
            }

            var now = DateTimeOffset.UtcNow;
            var insert = new PerfilUsuarioRow
            {
                Id = userId,
                Email = email,
                Nome = name,
                EmailVerificado = emailVerified,
                TipoUsuario = "cliente",
                CriadoEm = now,
                AtualizadoEm = now
            };

            if (!string.IsNullOrEmpty(passwordHash))
            {
                insert.HashSenha = passwordHash;
            }

            if (!string.IsNullOrEmpty(verificationCode))
            {
                insert.CodigoVerificacao = verificationCode;
                insert.CodigoVerificacaoExpiraEm = now.AddMinutes(15);
            }

            if (cpf != null && !string.IsNullOrWhiteSpace(cpf))
            {
                insert.Cpf = NormalizeCpf(cpf);
            }

            var created = await _client
                .From<PerfilUsuarioRow>()
                .Insert(insert);

            var newProfile = created.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("Perfil não retornado após insert");

            return (await MapProfileToEnglishAsync(newProfile, cancellationToken))!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter/criar perfil");
            throw;
        }
    }

    public async Task<UserProfile?> GetUserProfileAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _client!
                .From<PerfilUsuarioRow>()
                .Select("*")
                .Filter("id", Operator.Equals, userId)
                .Get();

            var data = response.Models.FirstOrDefault();
            if (data == null)
            {
                return null;
            }

            return await MapProfileToEnglishAsync(data, cancellationToken);
        }
        catch (PostgrestException ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    public async Task<UserProfile?> GetUserProfileByEmailAsync(
        string email,
        bool includePassword = false,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var selectFields = includePassword
            ? "*"
            : "id, email, nome, cpf, data_nascimento, cidade, pais, plano, criado_em, ultima_analise, atualizado_em, email_verificado, codigo_verificacao, codigo_verificacao_expira_em, tipo_usuario";

        try
        {
            var response = await _client!
                .From<PerfilUsuarioRow>()
                .Select(selectFields)
                .Filter("email", Operator.Equals, email)
                .Get();

            var data = response.Models.FirstOrDefault();
            if (data == null)
            {
                return null;
            }

            return await MapProfileToEnglishAsync(data, cancellationToken);
        }
        catch (PostgrestException ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    public async Task<bool> VerifyUserPasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var profile = await GetUserProfileByEmailAsync(email, includePassword: true, cancellationToken);
        if (profile == null || string.IsNullOrEmpty(profile.PasswordHash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, profile.PasswordHash);
    }

    public async Task<UserProfile> UpdateUserProfileAsync(
        string userId,
        Dictionary<string, object?> updates,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var row = new PerfilUsuarioRow { Id = userId, AtualizadoEm = DateTimeOffset.UtcNow };

        foreach (var (key, value) in updates)
        {
            ApplyProfileUpdate(row, ProfileUpdateKeyMap.GetValueOrDefault(key, key), value);
        }

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Update(row);

        var updated = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Perfil não retornado após update");

        return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
    }

    public async Task<UserProfile> UpdateVerificationCodeAsync(
        string userId,
        string code,
        int expiresInMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes);
        var row = new PerfilUsuarioRow
        {
            Id = userId,
            CodigoVerificacao = code,
            CodigoVerificacaoExpiraEm = expiresAt,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Update(row);

        var updated = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Perfil não retornado após update");

        return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
    }

    public async Task<UserProfile> VerifyEmailCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Select("*")
            .Filter("email", Operator.Equals, email)
            .Get();

        var profile = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Usuário não encontrado");

        if (profile.CodigoVerificacao != code)
        {
            throw new InvalidOperationException("Código de verificação inválido");
        }

        var now = DateTimeOffset.UtcNow;
        if (profile.CodigoVerificacaoExpiraEm.HasValue && now > profile.CodigoVerificacaoExpiraEm.Value)
        {
            throw new InvalidOperationException("Código de verificação expirado");
        }

        var update = new PerfilUsuarioRow
        {
            Id = profile.Id,
            EmailVerificado = true,
            CodigoVerificacao = null,
            CodigoVerificacaoExpiraEm = null,
            AtualizadoEm = now
        };

        var updatedResponse = await _client
            .From<PerfilUsuarioRow>()
            .Update(update);

        var updated = updatedResponse.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Perfil não retornado após update");

        return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
    }

    public async Task<UserProfile> VerifyLoginCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Select("*")
            .Filter("email", Operator.Equals, email)
            .Get();

        var profile = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Usuário não encontrado");

        if (profile.CodigoVerificacao != code)
        {
            throw new InvalidOperationException("Código de login inválido");
        }

        var now = DateTimeOffset.UtcNow;
        if (profile.CodigoVerificacaoExpiraEm.HasValue && now > profile.CodigoVerificacaoExpiraEm.Value)
        {
            throw new InvalidOperationException("Código de login expirado");
        }

        var update = new PerfilUsuarioRow
        {
            Id = profile.Id,
            CodigoVerificacao = null,
            CodigoVerificacaoExpiraEm = null,
            AtualizadoEm = now
        };

        var updatedResponse = await _client
            .From<PerfilUsuarioRow>()
            .Update(update);

        var updated = updatedResponse.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Perfil não retornado após update");

        return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
    }

    public Task<AddCreditsResultDto> AddCreditsToUserAsync(
        string userId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "addCreditsToUser está deprecated. Use createPurchase para adicionar créditos.");
        return Task.FromResult(new AddCreditsResultDto { Success = true });
    }

    public async Task<DeductCreditsResultDto> DeductCreditsFromUserAsync(
        string userId,
        int amount = 1,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var creditsResponse = await _client!
            .From<CreditoRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("usado", Operator.Equals, BoolCriterion(false))
            .Limit(amount)
            .Get();

        var credits = creditsResponse.Models;
        if (credits.Count < amount)
        {
            throw new InvalidOperationException("Créditos insuficientes");
        }

        var creditIds = credits.Select(c => c.Id).ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var creditId in creditIds)
        {
            await _client
                .From<CreditoRow>()
                .Filter("id", Operator.Equals, creditId)
                .Set(x => x.Usado, true)
                .Set(x => x.UsadoEm, now)
                .Update();
        }

        await _client
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId)
            .Set(x => x.UltimaAnalise, now)
            .Set(x => x.AtualizadoEm, now)
            .Update();

        return new DeductCreditsResultDto { Success = true };
    }

    public async Task<bool> UserHasCreditsAsync(
        string userId,
        int amount = 1,
        CancellationToken cancellationToken = default)
    {
        var available = await GetAvailableCreditsAsync(userId, cancellationToken);
        return available >= amount;
    }

    public Task<int> GetUserCreditsAsync(string userId, CancellationToken cancellationToken = default) =>
        GetAvailableCreditsAsync(userId, cancellationToken);

    public async Task<List<Credit>> GetCreditsByPurchaseAsync(
        string purchaseId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new List<Credit>();
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CreditoRow>()
                .Select("*")
                .Filter("id_compra", Operator.Equals, purchaseId)
                .Get();

            return response.Models.Select(MapCreditToEnglish).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar créditos da compra");
            return new List<Credit>();
        }
    }

    public async Task<Purchase> CreatePurchaseAsync(
        string userId,
        string planId,
        string planName,
        int creditsAmount,
        decimal price,
        string currency = "BRL",
        string paymentMethod = "mock",
        string? paymentId = null,
        string? parentPurchaseId = null,
        string serviceType = "analysis_plan",
        string? couponId = null,
        string? couponName = null,
        decimal? discountPercent = null,
        decimal? originalPrice = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var purchaseId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var insert = new CompraRow
        {
            Id = purchaseId,
            IdUsuario = userId,
            IdPlano = planId,
            NomePlano = planName,
            QuantidadeCreditos = creditsAmount,
            Preco = price,
            Moeda = currency,
            Status = "concluida",
            MetodoPagamento = paymentMethod,
            IdPagamento = paymentId ?? $"mock_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            CriadoEm = now,
            AtualizadoEm = now,
            IdCompraPai = parentPurchaseId,
            TipoServico = serviceType,
            IdCupom = couponId,
            NomeCupom = string.IsNullOrEmpty(couponName) ? null : couponName,
            PorcentagemDescontoAplicado = discountPercent,
            PrecoOriginal = originalPrice
        };

        var purchaseResponse = await _client!
            .From<CompraRow>()
            .Insert(insert);

        var purchase = purchaseResponse.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Compra não retornada após insert");

        if (creditsAmount > 0)
        {
            var creditos = Enumerable.Range(0, creditsAmount)
                .Select(_ => new CreditoRow
                {
                    IdCompra = purchaseId,
                    IdUsuario = userId,
                    Usado = false,
                    CriadoEm = now
                })
                .ToList();

            try
            {
                await _client.From<CreditoRow>().Insert(creditos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar créditos");
                await _client.From<CompraRow>().Filter("id", Operator.Equals, purchaseId).Delete();
                throw new InvalidOperationException("Erro ao criar créditos: " + ex.Message, ex);
            }
        }

        return MapPurchaseToEnglish(purchase)!;
    }

    public async Task<Purchase?> GetPurchaseByPaymentIdAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(paymentId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CompraRow>()
                .Select("*")
                .Filter("id_pagamento", Operator.Equals, paymentId)
                .Get();

            var data = response.Models.FirstOrDefault();
            return data == null ? null : MapPurchaseToEnglish(data);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<List<PurchaseWithCredits>> GetUserPurchasesAsync(
        string userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<CompraRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Order("criado_em", Ordering.Descending)
            .Limit(limit)
            .Get();

        var result = new List<PurchaseWithCredits>();
        foreach (var purchase in response.Models)
        {
            var credits = await GetCreditsByPurchaseAsync(purchase.Id, cancellationToken);
            var total = credits.Count;
            var used = credits.Count(c => c.Used);
            var available = total - used;

            var mapped = MapPurchaseToEnglish(purchase)!;
            result.Add(new PurchaseWithCredits
            {
                Id = mapped.Id,
                UserId = mapped.UserId,
                PlanId = mapped.PlanId,
                PlanName = mapped.PlanName,
                CreditsAmount = mapped.CreditsAmount,
                Price = mapped.Price,
                Currency = mapped.Currency,
                Status = mapped.Status,
                PaymentMethod = mapped.PaymentMethod,
                PaymentId = mapped.PaymentId,
                CreatedAt = mapped.CreatedAt,
                UpdatedAt = mapped.UpdatedAt,
                ParentPurchaseId = mapped.ParentPurchaseId,
                ServiceType = mapped.ServiceType,
                CouponId = mapped.CouponId,
                CouponName = mapped.CouponName,
                DiscountPercent = mapped.DiscountPercent,
                OriginalPrice = mapped.OriginalPrice,
                CreditsInfo = new PurchaseCreditsInfo
                {
                    Total = total,
                    Used = used,
                    Available = available,
                    Credits = credits
                }
            });
        }

        return result;
    }

    public async Task<CreditUsageResultDto> RecordCreditUsageAsync(
        string userId,
        string actionType,
        int creditsUsed = 1,
        string? resumeFileName = null,
        string? siteId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var creditsResponse = await _client!
            .From<CreditoRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("usado", Operator.Equals, BoolCriterion(false))
            .Limit(creditsUsed)
            .Get();

        var credits = creditsResponse.Models;
        if (credits.Count < creditsUsed)
        {
            throw new InvalidOperationException("Créditos insuficientes");
        }

        var creditIds = credits.Select(c => c.Id).ToList();
        var now = DateTimeOffset.UtcNow;

        foreach (var creditId in creditIds)
        {
            var query = _client
                .From<CreditoRow>()
                .Filter("id", Operator.Equals, creditId)
                .Set(x => x.Usado, true)
                .Set(x => x.UsadoEm, now)
                .Set(x => x.TipoAcao, actionType)
                .Set(x => x.NomeArquivoCurriculo, resumeFileName);

            if (!string.IsNullOrEmpty(siteId))
            {
                query = query.Set(x => x.IdSiteVagas, siteId);
            }

            await query.Update();
        }

        await _client
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId)
            .Set(x => x.UltimaAnalise, now)
            .Set(x => x.AtualizadoEm, now)
            .Update();

        return new CreditUsageResultDto
        {
            Success = true,
            CreditsUsed = creditsUsed,
            Id = creditIds.FirstOrDefault()
        };
    }

    public async Task<List<Credit>> GetUserCreditUsageAsync(
        string userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<CreditoRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("usado", Operator.Equals, BoolCriterion(true))
            .Order("usado_em", Ordering.Descending)
            .Limit(limit)
            .Get();

        return response.Models.Select(MapCreditToEnglish).ToList();
    }

    public async Task<List<Purchase>> GetAllPurchasesAsync(
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<CompraRow>()
            .Select("*")
            .Order("criado_em", Ordering.Descending)
            .Range(offset, offset + limit - 1)
            .Get();

        return response.Models
            .Select(MapPurchaseToEnglish)
            .Where(p => p != null)
            .Cast<Purchase>()
            .ToList();
    }

    public async Task<SalesStatsDto> GetSalesStatsAsync(
        string? startDate = null,
        string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var query = _client!.From<CompraRow>().Select("*");

        if (!string.IsNullOrEmpty(startDate))
        {
            query = query.Filter("criado_em", Operator.GreaterThanOrEqual, startDate);
        }

        if (!string.IsNullOrEmpty(endDate))
        {
            query = query.Filter("criado_em", Operator.LessThanOrEqual, endDate);
        }

        var response = await query.Get();
        var purchases = response.Models
            .Select(MapPurchaseToEnglish)
            .Where(p => p != null)
            .Cast<Purchase>()
            .ToList();

        return new SalesStatsDto
        {
            TotalPurchases = purchases.Count,
            TotalRevenue = purchases.Sum(p => Convert.ToDouble(p.Price ?? 0)),
            TotalCreditsSold = purchases.Sum(p => p.CreditsAmount),
            CompletedPurchases = purchases.Count(p =>
                p.Status is "concluida" or "completed"),
            PendingPurchases = purchases.Count(p =>
                p.Status is "pendente" or "pending"),
            CancelledPurchases = purchases.Count(p =>
                p.Status is "cancelada" or "cancelled")
        };
    }

    public async Task<UserProfile> UpdateVerificationTokenAsync(
        string userId,
        string token,
        int expiresInHours = 1,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.AddHours(expiresInHours);
        var row = new PerfilUsuarioRow
        {
            Id = userId,
            CodigoVerificacao = token,
            CodigoVerificacaoExpiraEm = expiresAt,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Update(row);

        var updated = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Perfil não retornado após update");

        return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
    }

    public async Task<UserProfile> VerifyEmailTokenAsync(
        string? email,
        string token,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var query = _client!
            .From<PerfilUsuarioRow>()
            .Select("*")
            .Filter("codigo_verificacao", Operator.Equals, token);

        if (!string.IsNullOrEmpty(email))
        {
            query = query.Filter("email", Operator.Equals, email);
        }

        var response = await query.Get();
        if (response.Models.Count == 0)
        {
            throw new InvalidOperationException("Token inválido");
        }

        var profile = response.Models[0];
        var now = DateTimeOffset.UtcNow;

        if (profile.CodigoVerificacaoExpiraEm.HasValue && now > profile.CodigoVerificacaoExpiraEm.Value)
        {
            throw new InvalidOperationException("Token expirado");
        }

        if (!string.IsNullOrEmpty(email))
        {
            var update = new PerfilUsuarioRow
            {
                Id = profile.Id,
                EmailVerificado = true,
                CodigoVerificacao = null,
                CodigoVerificacaoExpiraEm = null,
                AtualizadoEm = now
            };

            var updatedResponse = await _client
                .From<PerfilUsuarioRow>()
                .Update(update);

            var updated = updatedResponse.Models.FirstOrDefault()
                ?? throw new InvalidOperationException("Perfil não retornado após update");

            return (await MapProfileToEnglishAsync(updated, cancellationToken))!;
        }

        return (await MapProfileToEnglishAsync(profile, cancellationToken))!;
    }

    public async Task<UserProfile> GetUserByResetTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Select("*")
            .Filter("codigo_verificacao", Operator.Equals, token)
            .Get();

        if (response.Models.Count == 0)
        {
            throw new InvalidOperationException("Token inválido");
        }

        var profile = response.Models[0];
        var now = DateTimeOffset.UtcNow;

        if (profile.CodigoVerificacaoExpiraEm.HasValue && now > profile.CodigoVerificacaoExpiraEm.Value)
        {
            throw new InvalidOperationException("Token expirado");
        }

        return (await MapProfileToEnglishAsync(profile, cancellationToken))!;
    }

    private static string? FormatDateOfBirth(string? dataNasc)
    {
        if (string.IsNullOrEmpty(dataNasc))
        {
            return null;
        }

        if (dataNasc.Contains('T', StringComparison.Ordinal))
        {
            return dataNasc.Split('T')[0];
        }

        return dataNasc;
    }

    private static void ApplyProfileUpdate(PerfilUsuarioRow row, string column, object? value)
    {
        switch (column)
        {
            case "nome":
                row.Nome = value?.ToString();
                break;
            case "email":
                row.Email = value?.ToString();
                break;
            case "cpf":
                row.Cpf = value?.ToString();
                break;
            case "data_nascimento":
                row.DataNascimento = value switch
                {
                    null => null,
                    DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTimeOffset dto => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    _ => value.ToString()
                };
                break;
            case "cidade":
                row.Cidade = value?.ToString();
                break;
            case "pais":
                row.Pais = value?.ToString();
                break;
            case "plano":
                row.Plano = value?.ToString();
                break;
            case "ultima_analise":
                row.UltimaAnalise = ParseDateTimeOffset(value);
                break;
            case "atualizado_em":
                row.AtualizadoEm = ParseDateTimeOffset(value);
                break;
            case "email_verificado":
                row.EmailVerificado = value is bool b ? b : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                break;
            case "codigo_verificacao":
                row.CodigoVerificacao = value?.ToString();
                break;
            case "codigo_verificacao_expira_em":
                row.CodigoVerificacaoExpiraEm = ParseDateTimeOffset(value);
                break;
            case "tipo_usuario":
                row.TipoUsuario = value?.ToString();
                break;
            case "hash_senha":
                row.HashSenha = value?.ToString();
                break;
        }
    }

    private static DateTimeOffset? ParseDateTimeOffset(object? value) => value switch
    {
        null => null,
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
        string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
        JsonElement je when je.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(je.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedJe) => parsedJe,
        _ => null
    };

    public async Task<string?> GetAppConfigValueAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(key))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<AppConfiguracaoRow>()
                .Select("valor")
                .Filter("chave", Operator.Equals, key)
                .Get();

            return response.Models.FirstOrDefault()?.Valor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler app_configuracoes ({Key})", key);
            return null;
        }
    }

    public async Task SetAppConfigValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var row = new AppConfiguracaoRow
        {
            Chave = key,
            Valor = value,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        await _client!
            .From<AppConfiguracaoRow>()
            .Upsert(row);
    }

    public async Task<List<SiteVagasRow>> GetActiveJobSitesAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<SiteVagasRow>()
            .Select("*")
            .Filter("ativo", Operator.Equals, BoolCriterion(true))
            .Order("nome", Ordering.Ascending)
            .Get();

        return response.Models;
    }

    public async Task<SiteVagasRow?> GetJobSiteByIdAsync(string siteId, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(siteId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<SiteVagasRow>()
                .Select("*")
                .Filter("id", Operator.Equals, siteId)
                .Get();

            return response.Models.FirstOrDefault();
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    public async Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new AdminDashboardStatsDto();
        }

        await EnsureInitializedAsync(cancellationToken);

        var totalUsers = await _client.From<PerfilUsuarioRow>().Count(CountType.Exact);
        var totalCredits = await _client.From<CreditoRow>().Count(CountType.Exact);
        var creditsUsed = await _client
            .From<CreditoRow>()
            .Filter("usado", Operator.Equals, BoolCriterion(true))
            .Count(CountType.Exact);
        var creditsAvailable = await _client
            .From<CreditoRow>()
            .Filter("usado", Operator.Equals, BoolCriterion(false))
            .Count(CountType.Exact);

        var usersResponse = await _client
            .From<PerfilUsuarioRow>()
            .Select("criado_em,ultima_analise")
            .Get();

        var users = usersResponse.Models;
        var analysesPerformed = users.Count(u => u.UltimaAnalise != null);
        const decimal avgPricePerCredit = 8.30m;
        var estimatedRevenue = (totalCredits + analysesPerformed) * avgPricePerCredit;

        return new AdminDashboardStatsDto
        {
            TotalUsers = totalUsers,
            TotalCredits = totalCredits,
            CreditsUsed = creditsUsed,
            CreditsAvailable = creditsAvailable,
            AnalysesPerformed = analysesPerformed,
            EstimatedRevenue = Math.Round(estimatedRevenue, 2),
            ActiveUsers = analysesPerformed
        };
    }

    public async Task<List<AnaliseCurriculoListItemDto>> GetUserAnalysesAsync(
        string userId,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<AnaliseCurriculoRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Order("criado_em", Ordering.Descending)
            .Range(offset, offset + limit - 1)
            .Get();

        var result = new List<AnaliseCurriculoListItemDto>();
        foreach (var row in response.Models)
        {
            result.Add(await MapAnaliseWithRelationsAsync(row, cancellationToken));
        }

        return result;
    }

    public async Task<AnaliseCurriculoListItemDto?> GetAnalysisByIdAsync(
        string analysisId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _client!
                .From<AnaliseCurriculoRow>()
                .Select("*")
                .Filter("id", Operator.Equals, analysisId)
                .Get();

            var row = response.Models.FirstOrDefault();
            return row == null ? null : await MapAnaliseWithRelationsAsync(row, cancellationToken, includeResumeContent: true);
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    public async Task<string?> SaveAnalysisAsync(
        string resumeId,
        string userId,
        string siteId,
        ResumeAnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(resumeId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(siteId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var analysisId = Guid.NewGuid().ToString();
            var resultadoCompleto = JsonSerializer.SerializeToElement(new
            {
                experiencia = analysis.Experiencia,
                formacao = analysis.Formacao,
                habilidades = analysis.Habilidades,
                score = analysis.Score,
                pontosFortes = analysis.PontosFortes,
                pontosMelhorar = analysis.PontosMelhorar,
                recomendacoes = analysis.Recomendacoes
            });

            var insert = new AnaliseCurriculoRow
            {
                Id = analysisId,
                IdCurriculo = resumeId,
                IdUsuario = userId,
                IdSiteVagas = siteId,
                ScoreGeral = analysis.Score,
                PontosFortes = analysis.PontosFortes,
                PontosMelhorar = analysis.PontosMelhorar,
                PalavrasChaveSugeridas = analysis.Habilidades,
                Recomendacoes = analysis.Recomendacoes,
                ResultadoCompleto = resultadoCompleto,
                ServicosUtilizados = AnalysisServicesStatusHelper.SerializeStatus(
                    AnalysisBundledServiceKeys.CreateDefaultStatus()),
                CriadoEm = DateTimeOffset.UtcNow
            };

            await _client.From<AnaliseCurriculoRow>().Insert(insert);
            return analysisId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar análise de currículo");
            return null;
        }
    }

    public async Task<bool> UserOwnsAnalysisAsync(
        string userId,
        string analysisId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(analysisId))
        {
            return false;
        }

        var analysis = await GetAnalysisByIdAsync(analysisId, cancellationToken);
        return analysis != null && analysis.IdUsuario == userId;
    }

    public async Task<bool> MarkServiceUsedAsync(
        string analysisId,
        string serviceKey,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(analysisId) || string.IsNullOrEmpty(serviceKey))
        {
            return false;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<AnaliseCurriculoRow>()
                .Select("*")
                .Filter("id", Operator.Equals, analysisId)
                .Get();

            var row = response.Models.FirstOrDefault();
            if (row == null)
            {
                return false;
            }

            var status = AnalysisServicesStatusHelper.ParseStatus(row.ServicosUtilizados);
            status[serviceKey] = true;
            status[AnalysisBundledServiceKeys.Analise] = true;

            await _client
                .From<AnaliseCurriculoRow>()
                .Filter("id", Operator.Equals, analysisId)
                .Set(x => x.ServicosUtilizados, AnalysisServicesStatusHelper.SerializeStatus(status))
                .Update();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao marcar serviço {Service} na análise {AnalysisId}", serviceKey, analysisId);
            return false;
        }
    }

    public async Task<string?> GetAnalysisIdByResumeIdAsync(
        string userId,
        string resumeId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(resumeId))
        {
            return null;
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<AnaliseCurriculoRow>()
            .Select("id")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("id_curriculo", Operator.Equals, resumeId)
            .Order("criado_em", Ordering.Descending)
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault()?.Id;
    }

    public async Task<bool> HasInterviewForResumeAsync(
        string resumeId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(resumeId))
        {
            return false;
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<SimulacaoEntrevistaRow>()
            .Select("id")
            .Filter("id_curriculo", Operator.Equals, resumeId)
            .Limit(1)
            .Get();

        return response.Models.Count > 0;
    }

    public async Task<AnalysisServicesStatusDto> GetServicesStatusAsync(
        string analysisId,
        CancellationToken cancellationToken = default)
    {
        var empty = AnalysisServicesStatusHelper.Build(AnalysisBundledServiceKeys.CreateDefaultStatus());
        if (_client == null || string.IsNullOrEmpty(analysisId))
        {
            return empty;
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<AnaliseCurriculoRow>()
            .Select("*")
            .Filter("id", Operator.Equals, analysisId)
            .Get();

        var row = response.Models.FirstOrDefault();
        if (row == null)
        {
            return empty;
        }

        var hasInterview = await HasInterviewForResumeAsync(row.IdCurriculo ?? "", cancellationToken);
        var status = AnalysisServicesStatusHelper.ParseStatus(row.ServicosUtilizados);
        return AnalysisServicesStatusHelper.Build(status, hasInterview);
    }

    public async Task<PendingServicesSummaryDto> GetPendingServicesSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var summary = new PendingServicesSummaryDto();
        if (_client == null || string.IsNullOrEmpty(userId))
        {
            return summary;
        }

        var analyses = await GetUserAnalysesAsync(userId, 100, 0, cancellationToken);
        foreach (var analysis in analyses)
        {
            if (analysis.Servicos == null || analysis.Servicos.ServicosPendentes <= 0)
            {
                continue;
            }

            var pendentes = analysis.Servicos.Itens
                .Where(i => i.Pendente)
                .Select(i => i.Label)
                .ToList();

            summary.Analises.Add(new PendingAnalysisItemDto
            {
                AnalysisId = analysis.Id,
                NomeArquivo = analysis.CurriculosImportados?.NomeArquivoOriginal,
                SiteNome = analysis.SitesVagas?.Nome,
                ScoreGeral = analysis.ScoreGeral,
                CriadoEm = analysis.CriadoEm,
                ServicosPendentes = analysis.Servicos.ServicosPendentes,
                Pendentes = pendentes,
                Servicos = analysis.Servicos
            });

            summary.TotalServicosPendentes += analysis.Servicos.ServicosPendentes;
        }

        summary.AnalisesComPendencias = summary.Analises.Count;
        return summary;
    }

    private async Task<AnaliseCurriculoListItemDto> MapAnaliseWithRelationsAsync(
        AnaliseCurriculoRow row,
        CancellationToken cancellationToken,
        bool includeResumeContent = false)
    {
        CurriculoImportadoRefDto? curriculo = null;
        if (!string.IsNullOrEmpty(row.IdCurriculo))
        {
            var resume = await GetResumeByIdAsync(row.IdCurriculo, cancellationToken);
            if (resume != null)
            {
                curriculo = new CurriculoImportadoRefDto
                {
                    Id = resume.Id,
                    NomeArquivoOriginal = resume.NomeArquivoOriginal,
                    TipoArquivo = resume.TipoArquivo,
                    ConteudoExtraido = includeResumeContent ? resume.ConteudoExtraido : null,
                    DadosEstruturados = includeResumeContent
                        ? JsonElementCloneHelper.CloneOrNull(resume.DadosEstruturados)
                        : null,
                    CriadoEm = resume.CriadoEm
                };
            }
        }

        SiteVagasRefDto? site = null;
        if (!string.IsNullOrEmpty(row.IdSiteVagas))
        {
            var siteRow = await GetJobSiteByIdAsync(row.IdSiteVagas, cancellationToken);
            if (siteRow != null)
            {
                site = new SiteVagasRefDto
                {
                    Id = siteRow.Id,
                    Nome = siteRow.Nome,
                    UrlBase = siteRow.UrlBase
                };
            }
        }

        var hasInterview = !string.IsNullOrEmpty(row.IdCurriculo) &&
            await HasInterviewForResumeAsync(row.IdCurriculo, cancellationToken);
        var servicosStatus = AnalysisServicesStatusHelper.Build(
            AnalysisServicesStatusHelper.ParseStatus(JsonElementCloneHelper.CloneOrNull(row.ServicosUtilizados)),
            hasInterview);

        return new AnaliseCurriculoListItemDto
        {
            Id = row.Id,
            IdCurriculo = row.IdCurriculo,
            IdUsuario = row.IdUsuario,
            IdSiteVagas = row.IdSiteVagas,
            ScoreGeral = row.ScoreGeral,
            PontosFortes = row.PontosFortes,
            PontosMelhorar = row.PontosMelhorar,
            PalavrasChaveSugeridas = row.PalavrasChaveSugeridas,
            Recomendacoes = row.Recomendacoes,
            ResultadoCompleto = JsonElementCloneHelper.CloneOrNull(row.ResultadoCompleto),
            CriadoEm = row.CriadoEm,
            CurriculosImportados = curriculo,
            SitesVagas = site,
            Servicos = servicosStatus
        };
    }

    public async Task<string?> SaveImportedResumeAsync(
        string userId,
        string siteId,
        string fileName,
        string fileType,
        string textContent,
        string? creditId = null,
        object? analysisData = null,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(siteId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var resumeId = Guid.NewGuid().ToString();
            var insert = new CurriculoImportadoRow
            {
                Id = resumeId,
                IdUsuario = userId,
                IdSiteVagas = siteId,
                NomeArquivoOriginal = fileName,
                TipoArquivo = fileType,
                CaminhoArquivo = $"upload/{userId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{fileName}",
                ConteudoExtraido = textContent,
                DadosEstruturados = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["textLength"] = textContent?.Length ?? 0,
                    ["uploadedAt"] = DateTime.UtcNow.ToString("o"),
                    ["analysisData"] = analysisData
                }),
                IdCreditoUsado = creditId,
                CriadoEm = DateTimeOffset.UtcNow,
                AtualizadoEm = DateTimeOffset.UtcNow
            };

            await _client.From<CurriculoImportadoRow>().Insert(insert);
            return resumeId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar currículo importado");
            return null;
        }
    }

    public async Task<CurriculoImportadoRow?> GetResumeByIdAsync(string resumeId, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(resumeId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CurriculoImportadoRow>()
                .Select("*")
                .Filter("id", Operator.Equals, resumeId)
                .Get();

            return response.Models.FirstOrDefault();
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return null;
        }
    }

    public async Task<string?> CreateInterviewSimulationAsync(
        string userId,
        string resumeId,
        string siteId,
        List<string> questions,
        string areaFoco,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var simulationId = Guid.NewGuid().ToString();
        var insert = new SimulacaoEntrevistaRow
        {
            Id = simulationId,
            IdCurriculo = resumeId,
            IdUsuario = userId,
            IdSiteVagas = siteId,
            Titulo = "Simulação de Entrevista",
            AreaFoco = areaFoco,
            PerguntasFeitas = questions,
            RespostasDadas = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            CriadoEm = DateTimeOffset.UtcNow,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        await _client!.From<SimulacaoEntrevistaRow>().Insert(insert);
        return simulationId;
    }

    public async Task<bool> SaveInterviewMessageAsync(
        string simulationId,
        string question,
        string answer,
        InterviewEvaluation evaluation,
        int order,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var questionInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "pergunta",
            Conteudo = question,
            Ordem = order * 3 - 2,
            DadosExtras = new Dictionary<string, object?> { ["questionIndex"] = order - 1 }
        };

        var answerInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "resposta",
            Conteudo = answer,
            Ordem = order * 3 - 1,
            DadosExtras = new Dictionary<string, object?> { ["questionIndex"] = order - 1 }
        };

        var feedbackInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "feedback",
            Conteudo = JsonSerializer.Serialize(evaluation),
            Feedback = evaluation.Feedback,
            Ordem = order * 3,
            DadosExtras = new Dictionary<string, object?>
            {
                ["questionIndex"] = order - 1,
                ["score"] = evaluation.Score,
                ["strengths"] = evaluation.Strengths,
                ["improvements"] = evaluation.Improvements
            }
        };

        await _client!.From<MensagemEntrevistaInsert>().Insert(questionInsert);
        await _client.From<MensagemEntrevistaInsert>().Insert(answerInsert);
        await _client.From<MensagemEntrevistaInsert>().Insert(feedbackInsert);
        return true;
    }

    public async Task<FinishInterviewResult> UpdateSimulationAnswersAsync(
        string simulationId,
        List<InterviewAnswerItem> allAnswers,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var scores = allAnswers.Select(a => a.Evaluation?.Score ?? 70).ToList();
        var averageScore = scores.Count > 0 ? (int)Math.Round(scores.Average()) : 70;

        var feedbackGeral = new Dictionary<string, object?>
        {
            ["score"] = averageScore,
            ["totalPerguntas"] = allAnswers.Count,
            ["respostas"] = allAnswers,
            ["statistics"] = new Dictionary<string, object?>
            {
                ["goodAnswers"] = scores.Count(s => s >= 70),
                ["averageAnswers"] = scores.Count(s => s is >= 50 and < 70),
                ["poorAnswers"] = scores.Count(s => s < 50),
                ["minScore"] = scores.Count > 0 ? scores.Min() : 0,
                ["maxScore"] = scores.Count > 0 ? scores.Max() : 0
            }
        };

        var update = new SimulacaoEntrevistaRow
        {
            Id = simulationId,
            RespostasDadas = JsonSerializer.SerializeToElement(allAnswers),
            ScoreGeral = averageScore,
            FeedbackGeral = JsonSerializer.SerializeToElement(feedbackGeral),
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        await _client!
            .From<SimulacaoEntrevistaRow>()
            .Filter("id", Operator.Equals, simulationId)
            .Update(update);

        return new FinishInterviewResult { AverageScore = averageScore };
    }

    public async Task<InterviewDetailDto?> GetInterviewByIdAsync(
        string simulationId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var simResponse = await _client!
            .From<SimulacaoEntrevistaRow>()
            .Select("*")
            .Filter("id", Operator.Equals, simulationId)
            .Get();

        var simulation = simResponse.Models.FirstOrDefault();
        if (simulation == null)
        {
            return null;
        }

        var msgResponse = await _client
            .From<MensagemEntrevistaRow>()
            .Select("*")
            .Filter("id_simulacao", Operator.Equals, simulationId)
            .Order("ordem", Ordering.Ascending)
            .Get();

        return new InterviewDetailDto
        {
            Id = simulation.Id,
            IdCurriculo = simulation.IdCurriculo,
            IdUsuario = simulation.IdUsuario,
            IdSiteVagas = simulation.IdSiteVagas,
            Titulo = simulation.Titulo,
            AreaFoco = simulation.AreaFoco,
            PerguntasFeitas = simulation.PerguntasFeitas,
            RespostasDadas = simulation.RespostasDadas,
            FeedbackGeral = simulation.FeedbackGeral,
            ScoreGeral = simulation.ScoreGeral,
            CriadoEm = simulation.CriadoEm,
            AtualizadoEm = simulation.AtualizadoEm,
            Messages = msgResponse.Models.Select(m => new InterviewMessageDto
            {
                Id = m.Id,
                IdSimulacao = m.IdSimulacao,
                Tipo = m.Tipo,
                Conteudo = m.Conteudo,
                Feedback = m.Feedback,
                Ordem = m.Ordem,
                DadosExtras = m.DadosExtras,
                CriadoEm = m.CriadoEm
            }).ToList()
        };
    }

    public async Task<List<SimulacaoEntrevistaRow>> GetUserInterviewsAsync(
        string userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<SimulacaoEntrevistaRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Order("criado_em", Ordering.Descending)
            .Limit(limit)
            .Get();

        return response.Models;
    }

    public async Task SaveFoundJobsAsync(
        string userId,
        string resumeId,
        string siteId,
        List<JobListing> jobs,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || jobs.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        const int batchSize = 50;
        for (var i = 0; i < jobs.Count; i += batchSize)
        {
            var batch = jobs.Skip(i).Take(batchSize).Select(job => new VagaEncontradaInsert
            {
                IdCurriculo = resumeId,
                IdUsuario = userId,
                IdSiteVagas = siteId,
                TituloVaga = job.Title,
                Empresa = job.Company,
                Localizacao = job.Location,
                UrlVaga = job.Url,
                DescricaoVaga = job.Description,
                Requisitos = job.Requirements,
                ScoreCompatibilidade = job.CompatibilityScore ?? 0,
                PalavrasChaveMatch = job.MatchedKeywords,
                DadosCompletos = new Dictionary<string, object?>
                {
                    ["salary"] = job.Salary ?? "",
                    ["contractType"] = job.ContractType ?? "",
                    ["experienceLevel"] = job.ExperienceLevel ?? "",
                    ["site"] = job.Site ?? ""
                },
                Status = "ativa"
            }).ToList();

            try
            {
                await _client.From<VagaEncontradaInsert>().Insert(batch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar lote de vagas encontradas");
            }
        }
    }
}
