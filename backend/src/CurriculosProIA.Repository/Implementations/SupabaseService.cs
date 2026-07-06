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

public class SupabaseService : IAppDataStore, ISupabaseConnectionTester, IKiwifyWebhookLogRepository
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
                .Select("id, nome, porcentagem_desconto, ativo, id_parceiro, porcentagem_parceiro")
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

    public async Task<CupomRow?> GetCouponByIdAsync(string couponId, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrWhiteSpace(couponId))
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CupomRow>()
                .Select("id, nome, porcentagem_desconto, ativo, id_parceiro, porcentagem_parceiro")
                .Filter("id", Operator.Equals, couponId)
                .Get();

            return response.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar cupom por id");
            return null;
        }
    }

    public async Task<List<PartnerDto>> ListPartnersAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new List<PartnerDto>();
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<ParceiroRow>()
            .Select("*")
            .Order("nome", Ordering.Ascending)
            .Get();

        return response.Models.Select(p => new PartnerDto
        {
            Id = p.Id,
            Nome = p.Nome ?? string.Empty,
            Cpf = p.Cpf,
            Descricao = p.Descricao,
            Email = p.Email,
            Ativo = p.Ativo ?? true,
            CriadoEm = p.CriadoEm
        }).ToList();
    }

    public async Task<PartnerDto> CreatePartnerAsync(
        string nome,
        string cpf,
        string? descricao,
        string? email,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var trimmed = nome.Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("Nome do parceiro é obrigatório.");
        }

        var docNorm = NormalizeCpf(cpf);
        if (docNorm.Length != 11 && docNorm.Length != 14)
        {
            throw new InvalidOperationException("CPF ou CNPJ inválido. Informe 11 dígitos (CPF) ou 14 dígitos (CNPJ).");
        }

        var existingDoc = await _client!
            .From<ParceiroRow>()
            .Select("id")
            .Filter("cpf", Operator.Equals, docNorm)
            .Get();
        if (existingDoc.Models.Count > 0)
        {
            throw new InvalidOperationException("Já existe um parceiro cadastrado com este CPF ou CNPJ.");
        }

        var now = DateTimeOffset.UtcNow;
        var partnerId = Guid.NewGuid().ToString();
        var insert = new ParceiroRow
        {
            Id = partnerId,
            Nome = trimmed,
            Cpf = docNorm,
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Ativo = true,
            CriadoEm = now,
            AtualizadoEm = now
        };

        var response = await _client.From<ParceiroRow>().Insert(insert);
        var row = response.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Parceiro não retornado após insert");

        return new PartnerDto
        {
            Id = row.Id,
            Nome = row.Nome ?? trimmed,
            Cpf = row.Cpf,
            Descricao = row.Descricao,
            Email = row.Email,
            Ativo = row.Ativo ?? true,
            CriadoEm = row.CriadoEm
        };
    }

    public async Task<List<AdminCouponDto>> ListCouponsAdminAsync(CancellationToken cancellationToken = default)
    {
        var metrics = await GetCouponMetricsAsync(cancellationToken);
        var referralCounts = await CountReferralsByCouponAsync(cancellationToken);
        return metrics.ByCoupon.Select(c => new AdminCouponDto
        {
            Id = c.CouponId,
            Nome = c.CouponName,
            PorcentagemDesconto = c.DiscountPercent,
            Ativo = c.Ativo,
            ParceiroId = c.ParceiroId,
            ParceiroNome = c.ParceiroNome,
            PorcentagemParceiro = c.ParceiroPercent,
            TotalCompras = c.PurchasesCount,
            TotalUsosCpf = c.UniqueCpfUses,
            TotalCadastrosViaLink = referralCounts.GetValueOrDefault(c.CouponId),
            ReceitaTotal = c.RevenueTotal,
            TotalParceiro = c.PartnerTotal
        }).ToList();
    }

    public async Task<AdminCouponDto> CreateCouponAsync(
        string nome,
        decimal porcentagemDesconto,
        string? parceiroId,
        decimal? porcentagemParceiro,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var code = nome.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            throw new InvalidOperationException("Código do cupom é obrigatório.");
        }

        if (porcentagemDesconto < 0 || porcentagemDesconto > 100)
        {
            throw new InvalidOperationException("Desconto deve estar entre 0 e 100.");
        }

        if (!string.IsNullOrEmpty(parceiroId))
        {
            if (porcentagemParceiro is null or < 0 or > 100)
            {
                throw new InvalidOperationException("Informe a porcentagem de recebimento do parceiro (0-100).");
            }
        }
        else
        {
            parceiroId = null;
            porcentagemParceiro = null;
        }

        var existing = await GetCouponByCodeAsync(code, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException("Já existe um cupom com este código.");
        }

        var now = DateTimeOffset.UtcNow;
        var couponId = Guid.NewGuid().ToString();
        var insert = new CupomRow
        {
            Id = couponId,
            Nome = code,
            PorcentagemDesconto = porcentagemDesconto,
            Ativo = true,
            IdParceiro = parceiroId,
            PorcentagemParceiro = porcentagemParceiro,
            CriadoEm = now
        };

        await _client!.From<CupomRow>().Insert(insert);

        var list = await ListCouponsAdminAsync(cancellationToken);
        return list.FirstOrDefault(c => c.Id == couponId)
            ?? new AdminCouponDto
            {
                Id = couponId,
                Nome = code,
                PorcentagemDesconto = porcentagemDesconto,
                Ativo = true,
                ParceiroId = parceiroId,
                PorcentagemParceiro = porcentagemParceiro
            };
    }

    public async Task<AdminCouponDto?> UpdateCouponAsync(
        string couponId,
        decimal? porcentagemDesconto,
        string? parceiroId,
        decimal? porcentagemParceiro,
        bool? ativo,
        bool clearParceiro,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var current = await GetCouponByIdAsync(couponId, cancellationToken)
            ?? throw new InvalidOperationException("Cupom não encontrado.");

        if (porcentagemDesconto is < 0 or > 100)
        {
            throw new InvalidOperationException("Desconto deve estar entre 0 e 100.");
        }

        var update = new CupomRow
        {
            Id = current.Id,
            Nome = current.Nome,
            PorcentagemDesconto = porcentagemDesconto ?? current.PorcentagemDesconto,
            Ativo = ativo ?? current.Ativo,
            IdParceiro = clearParceiro ? null : (parceiroId ?? current.IdParceiro),
            PorcentagemParceiro = clearParceiro ? null : (porcentagemParceiro ?? current.PorcentagemParceiro)
        };

        if (!string.IsNullOrEmpty(update.IdParceiro) && update.PorcentagemParceiro is null or < 0 or > 100)
        {
            throw new InvalidOperationException("Informe a porcentagem de recebimento do parceiro (0-100).");
        }

        await _client!
            .From<CupomRow>()
            .Filter("id", Operator.Equals, couponId)
            .Update(update);

        var list = await ListCouponsAdminAsync(cancellationToken);
        return list.FirstOrDefault(c => c.Id == couponId);
    }

    public async Task<CouponMetricsSummaryDto> GetCouponMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new CouponMetricsSummaryDto();
        }

        await EnsureInitializedAsync(cancellationToken);

        var cuponsResponse = await _client.From<CupomRow>().Select("*").Get();
        var parceirosResponse = await _client.From<ParceiroRow>().Select("id, nome").Get();
        var comprasResponse = await _client
            .From<CompraRow>()
            .Select("id, id_cupom, preco, id_parceiro, valor_parceiro, status")
            .Get();
        var usosResponse = await _client.From<CupomUsoRow>().Select("id_cupom").Get();

        var parceirosById = parceirosResponse.Models.ToDictionary(p => p.Id, p => p.Nome ?? string.Empty);
        var usosByCupom = usosResponse.Models
            .Where(u => !string.IsNullOrEmpty(u.IdCupom))
            .GroupBy(u => u.IdCupom!)
            .ToDictionary(g => g.Key, g => g.Count());

        var comprasConcluidas = comprasResponse.Models
            .Where(c => !string.IsNullOrWhiteSpace(c.IdCupom) && IsPurchaseCompleted(c.Status))
            .ToList();

        var comprasByCupom = comprasConcluidas
            .GroupBy(c => c.IdCupom!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var byCoupon = cuponsResponse.Models.Select(cupom =>
        {
            comprasByCupom.TryGetValue(cupom.Id, out var compras);
            compras ??= new List<CompraRow>();
            var partnerName = cupom.IdParceiro != null && parceirosById.TryGetValue(cupom.IdParceiro, out var pn)
                ? pn
                : null;

            return new CouponMetricItemDto
            {
                CouponId = cupom.Id,
                CouponName = cupom.Nome ?? string.Empty,
                DiscountPercent = cupom.PorcentagemDesconto ?? 0,
                Ativo = cupom.Ativo ?? true,
                ParceiroId = cupom.IdParceiro,
                ParceiroNome = partnerName,
                ParceiroPercent = cupom.PorcentagemParceiro,
                PurchasesCount = compras.Count,
                UniqueCpfUses = usosByCupom.GetValueOrDefault(cupom.Id),
                RevenueTotal = compras.Sum(c => c.Preco ?? 0),
                PartnerTotal = compras.Sum(c => c.ValorParceiro ?? 0)
            };
        }).OrderByDescending(c => c.PurchasesCount).ToList();

        var byPartner = comprasConcluidas
            .Where(c => !string.IsNullOrEmpty(c.IdParceiro))
            .GroupBy(c => c.IdParceiro!)
            .Select(g =>
            {
                parceirosById.TryGetValue(g.Key, out var nome);
                var couponIds = cuponsResponse.Models
                    .Where(c => c.IdParceiro == g.Key)
                    .Select(c => c.Id)
                    .ToHashSet();

                return new PartnerMetricItemDto
                {
                    ParceiroId = g.Key,
                    ParceiroNome = nome ?? "Parceiro",
                    CouponsCount = couponIds.Count,
                    PurchasesCount = g.Count(),
                    RevenueTotal = g.Sum(c => c.Preco ?? 0),
                    PartnerTotal = g.Sum(c => c.ValorParceiro ?? 0)
                };
            })
            .OrderByDescending(p => p.PurchasesCount)
            .ToList();

        return new CouponMetricsSummaryDto
        {
            ByCoupon = byCoupon,
            ByPartner = byPartner,
            TotalPurchasesWithCoupon = comprasConcluidas.Count,
            TotalRevenueWithCoupon = comprasConcluidas.Sum(c => c.Preco ?? 0),
            TotalPartnerPayout = comprasConcluidas.Sum(c => c.ValorParceiro ?? 0)
        };
    }

    private static bool IsPurchaseCompleted(string? status) =>
        status is "concluida" or "completed";

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

    public async Task<List<UserProfile>> SearchUsersAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length < 2)
        {
            return new List<UserProfile>();
        }

        limit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var pattern = $"%{trimmed}%";
        var selectFields =
            "id, email, nome, cpf, data_nascimento, cidade, pais, plano, criado_em, ultima_analise, atualizado_em, email_verificado, codigo_verificacao, codigo_verificacao_expira_em, tipo_usuario";

        try
        {
            var emailResponse = await _client!
                .From<PerfilUsuarioRow>()
                .Select(selectFields)
                .Filter("email", Operator.ILike, pattern)
                .Limit(limit)
                .Get();

            var nameResponse = await _client
                .From<PerfilUsuarioRow>()
                .Select(selectFields)
                .Filter("nome", Operator.ILike, pattern)
                .Limit(limit)
                .Get();

            var merged = new Dictionary<string, PerfilUsuarioRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in emailResponse.Models.Concat(nameResponse.Models))
            {
                if (!string.IsNullOrWhiteSpace(row.Id))
                {
                    merged[row.Id] = row;
                }
            }

            var profiles = new List<UserProfile>();
            foreach (var row in merged.Values.Take(limit))
            {
                var profile = await MapProfileToEnglishAsync(row, cancellationToken);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            return profiles
                .OrderBy(p => p.Email, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuários");
            return new List<UserProfile>();
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

        var query = _client!
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId);

        foreach (var (key, value) in updates)
        {
            var column = ProfileUpdateKeyMap.GetValueOrDefault(key, key);
            query = column switch
            {
                "nome" => query.Set(x => x.Nome, value?.ToString()),
                "email" => query.Set(x => x.Email, value?.ToString()),
                "cpf" => query.Set(x => x.Cpf, value?.ToString()),
                "data_nascimento" => query.Set(x => x.DataNascimento, FormatProfileDateOfBirth(value)),
                "cidade" => query.Set(x => x.Cidade, value?.ToString()),
                "pais" => query.Set(x => x.Pais, value?.ToString()),
                "plano" => query.Set(x => x.Plano, value?.ToString()),
                "ultima_analise" => query.Set(x => x.UltimaAnalise, ParseDateTimeOffset(value)),
                "atualizado_em" => query.Set(x => x.AtualizadoEm, ParseDateTimeOffset(value)),
                "email_verificado" => query.Set(x => x.EmailVerificado,
                    value is bool b ? b : Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
                "codigo_verificacao" => query.Set(x => x.CodigoVerificacao, value?.ToString()),
                "codigo_verificacao_expira_em" => query.Set(x => x.CodigoVerificacaoExpiraEm, ParseDateTimeOffset(value)),
                "tipo_usuario" => query.Set(x => x.TipoUsuario, value?.ToString()),
                "hash_senha" => query.Set(x => x.HashSenha, value?.ToString()),
                _ => query
            };
        }

        var response = await query
            .Set(x => x.AtualizadoEm, DateTimeOffset.UtcNow)
            .Update();

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

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId)
            .Set(x => x.CodigoVerificacao, code)
            .Set(x => x.CodigoVerificacaoExpiraEm, expiresAt)
            .Set(x => x.AtualizadoEm, DateTimeOffset.UtcNow)
            .Update();

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

        profile.EmailVerificado = true;
        profile.CodigoVerificacao = null;
        profile.CodigoVerificacaoExpiraEm = null;
        profile.AtualizadoEm = now;

        var updatedResponse = await _client
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, profile.Id)
            .Update(profile);

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

        profile.CodigoVerificacao = null;
        profile.CodigoVerificacaoExpiraEm = null;
        profile.AtualizadoEm = now;

        var updatedResponse = await _client
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, profile.Id)
            .Update(profile);

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
        string? partnerId = null,
        decimal? partnerPercent = null,
        decimal? partnerAmount = null,
        string? analysisId = null,
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
            IdAnalise = analysisId,
            IdCupom = couponId,
            NomeCupom = string.IsNullOrEmpty(couponName) ? null : couponName,
            PorcentagemDescontoAplicado = discountPercent,
            PrecoOriginal = originalPrice,
            IdParceiro = partnerId,
            PorcentagemParceiroAplicada = partnerPercent,
            ValorParceiro = partnerAmount
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
                    Id = Guid.NewGuid().ToString(),
                    IdCompra = purchaseId,
                    IdUsuario = userId,
                    Usado = false,
                    TipoAcao = "analise",
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

    public async Task<Purchase> CreatePendingPurchaseAsync(
        string userId,
        string planId,
        string planName,
        int creditsAmount,
        decimal price,
        string paymentMethod = "kiwify",
        string? paymentId = null,
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
            Moeda = "BRL",
            Status = "pendente",
            MetodoPagamento = paymentMethod,
            IdPagamento = paymentId ?? $"kiwify_pending_{Guid.NewGuid():N}",
            CriadoEm = now,
            AtualizadoEm = now,
            TipoServico = "analysis_plan"
        };

        var purchaseResponse = await _client!
            .From<CompraRow>()
            .Insert(insert);

        var purchase = purchaseResponse.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Compra pendente não retornada após insert");

        return MapPurchaseToEnglish(purchase)!;
    }

    public async Task<List<Purchase>> GetPendingPurchasesAsync(
        string? userId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        limit = Math.Clamp(limit, 1, 200);
        var query = _client!
            .From<CompraRow>()
            .Select("*")
            .Filter("status", Operator.Equals, "pendente")
            .Order("criado_em", Ordering.Descending)
            .Limit(limit);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Filter("id_usuario", Operator.Equals, userId.Trim());
        }

        var response = await query.Get();
        return response.Models
            .Select(MapPurchaseToEnglish)
            .Where(p => p != null)
            .Cast<Purchase>()
            .ToList();
    }

    public async Task UpdatePurchaseStatusAsync(
        string purchaseId,
        string status,
        string? paymentId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var updates = new CompraRow
        {
            Id = purchaseId,
            Status = status,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            updates.IdPagamento = paymentId.Trim();
        }

        await _client!
            .From<CompraRow>()
            .Filter("id", Operator.Equals, purchaseId)
            .Update(updates);
    }

    public async Task MarkPendingPurchasesSubstitutedAsync(
        string userId,
        string planId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<CompraRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("id_plano", Operator.Equals, planId)
            .Filter("status", Operator.Equals, "pendente")
            .Get();

        foreach (var row in response.Models)
        {
            await _client
                .From<CompraRow>()
                .Filter("id", Operator.Equals, row.Id)
                .Update(new CompraRow
                {
                    Id = row.Id,
                    Status = "substituida",
                    AtualizadoEm = DateTimeOffset.UtcNow
                });
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

        var credits = creditsResponse.Models
            .Where(c => !string.Equals(c.TipoAcao, "curriculo_ingles", StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    public async Task<List<PurchaseBuyerDto>> GetDistinctPurchaseBuyersAsync(
        int limit = 300,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        limit = Math.Clamp(limit, 1, 500);

        var response = await _client!
            .From<CompraRow>()
            .Select("id_usuario, criado_em")
            .Order("criado_em", Ordering.Descending)
            .Limit(5000)
            .Get();

        var grouped = response.Models
            .Where(row => !string.IsNullOrWhiteSpace(row.IdUsuario))
            .GroupBy(row => row.IdUsuario!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                UserId = group.Key,
                PurchasesCount = group.Count(),
                LastPurchaseAt = group
                    .Select(row => row.CriadoEm)
                    .Where(date => date.HasValue)
                    .Max()
            })
            .OrderByDescending(item => item.LastPurchaseAt ?? DateTimeOffset.MinValue)
            .Take(limit)
            .ToList();

        var buyers = new List<PurchaseBuyerDto>();
        foreach (var item in grouped)
        {
            var profile = await GetUserProfileAsync(item.UserId, cancellationToken);
            buyers.Add(new PurchaseBuyerDto
            {
                Id = item.UserId,
                Email = profile?.Email,
                Name = profile?.Name,
                Credits = profile?.Credits ?? 0,
                PurchasesCount = item.PurchasesCount,
                LastPurchaseAt = item.LastPurchaseAt
            });
        }

        return buyers
            .OrderBy(buyer => buyer.Name ?? buyer.Email ?? buyer.Id, StringComparer.OrdinalIgnoreCase)
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
            ApprovedRevenue = purchases
                .Where(p => p.Status is "concluida" or "completed")
                .Sum(p => Convert.ToDouble(p.Price ?? 0)),
            PendingRevenue = purchases
                .Where(p => p.Status is "pendente" or "pending")
                .Sum(p => Convert.ToDouble(p.Price ?? 0)),
            TotalCreditsSold = purchases.Sum(p => p.CreditsAmount),
            CompletedPurchases = purchases.Count(p =>
                p.Status is "concluida" or "completed"),
            PendingPurchases = purchases.Count(p =>
                p.Status is "pendente" or "pending"),
            CancelledPurchases = purchases.Count(p =>
                p.Status is "cancelada" or "cancelled"),
            UniqueBuyers = purchases
                .Select(p => p.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
        };
    }

    public async Task<IReadOnlyList<DailyUsageDto>> GetDailyUsageAsync(
        int days,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        days = Math.Clamp(days, 1, 365);
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-days + 1);
        var startIso = start.ToString("yyyy-MM-dd");

        var purchasesResponse = await _client!
            .From<CompraRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var profilesResponse = await _client
            .From<PerfilUsuarioRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var analysesResponse = await _client
            .From<AnaliseCurriculoRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var buckets = new Dictionary<string, DailyUsageDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            buckets[key] = new DailyUsageDto { Date = key };
        }

        foreach (var row in purchasesResponse.Models)
        {
            var purchase = MapPurchaseToEnglish(row);
            if (purchase?.CreatedAt == null)
            {
                continue;
            }

            var status = purchase.Status?.ToLowerInvariant();
            if (status is not ("concluida" or "completed"))
            {
                continue;
            }

            var key = purchase.CreatedAt.Value.UtcDateTime.ToString("yyyy-MM-dd");
            if (!buckets.TryGetValue(key, out var bucket))
            {
                continue;
            }

            bucket.Revenue += purchase.Price ?? 0;
        }

        foreach (var row in profilesResponse.Models)
        {
            if (row.CriadoEm == null)
            {
                continue;
            }

            var key = row.CriadoEm.Value.UtcDateTime.ToString("yyyy-MM-dd");
            if (buckets.TryGetValue(key, out var bucket))
            {
                bucket.Registrations++;
            }
        }

        foreach (var row in analysesResponse.Models)
        {
            if (row.CriadoEm == null)
            {
                continue;
            }

            var key = row.CriadoEm.Value.UtcDateTime.ToString("yyyy-MM-dd");
            if (buckets.TryGetValue(key, out var bucket))
            {
                bucket.Analyses++;
            }
        }

        return buckets.Values
            .OrderBy(x => x.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyUsageDto>> GetMonthlyUsageAsync(
        int months,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        months = Math.Clamp(months, 1, 24);
        var end = DateTime.UtcNow;
        var start = new DateTime(end.Year, end.Month, 1).AddMonths(-(months - 1));
        var startIso = start.ToString("yyyy-MM-dd");

        var purchasesResponse = await _client!
            .From<CompraRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var profilesResponse = await _client
            .From<PerfilUsuarioRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var analysesResponse = await _client
            .From<AnaliseCurriculoRow>()
            .Filter("criado_em", Operator.GreaterThanOrEqual, startIso)
            .Get();

        var buckets = new Dictionary<string, MonthlyUsageDto>();
        for (var d = new DateTime(start.Year, start.Month, 1); d <= end; d = d.AddMonths(1))
        {
            var key = $"{d.Year}-{d.Month:D2}";
            buckets[key] = new MonthlyUsageDto { Month = key };
        }

        foreach (var row in purchasesResponse.Models)
        {
            var purchase = MapPurchaseToEnglish(row);
            if (purchase?.CreatedAt == null)
            {
                continue;
            }

            var status = purchase.Status?.ToLowerInvariant();
            if (status is not ("concluida" or "completed"))
            {
                continue;
            }

            var key = $"{purchase.CreatedAt.Value.UtcDateTime:yyyy-MM}";
            if (!buckets.TryGetValue(key, out var bucket))
            {
                continue;
            }

            bucket.Revenue += purchase.Price ?? 0;
        }

        foreach (var row in profilesResponse.Models)
        {
            if (row.CriadoEm == null)
            {
                continue;
            }

            var key = $"{row.CriadoEm.Value.UtcDateTime:yyyy-MM}";
            if (buckets.TryGetValue(key, out var bucket))
            {
                bucket.Registrations++;
            }
        }

        foreach (var row in analysesResponse.Models)
        {
            if (row.CriadoEm == null)
            {
                continue;
            }

            var key = $"{row.CriadoEm.Value.UtcDateTime:yyyy-MM}";
            if (buckets.TryGetValue(key, out var bucket))
            {
                bucket.Analyses++;
            }
        }

        return buckets.Values
            .OrderBy(x => x.Month)
            .ToList();
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

        var response = await _client!
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId)
            .Set(x => x.CodigoVerificacao, token)
            .Set(x => x.CodigoVerificacaoExpiraEm, expiresAt)
            .Set(x => x.AtualizadoEm, DateTimeOffset.UtcNow)
            .Update();

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
            profile.EmailVerificado = true;
            profile.CodigoVerificacao = null;
            profile.CodigoVerificacaoExpiraEm = null;
            profile.AtualizadoEm = now;

            var updatedResponse = await _client
                .From<PerfilUsuarioRow>()
                .Filter("id", Operator.Equals, profile.Id)
                .Update(profile);

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
                row.DataNascimento = FormatProfileDateOfBirth(value);
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

    private static string? FormatProfileDateOfBirth(object? value) =>
        value switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

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

    public async Task<PricingConfigDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return null;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<ConfigPrecosRow>()
                .Select("*")
                .Filter("id", Operator.Equals, ConfigPrecosRow.DefaultId)
                .Get();

            var row = response.Models.FirstOrDefault();
            return row == null ? null : MapPricingConfigRow(row);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler config_precos");
            return null;
        }
    }

    public async Task SaveAsync(PricingConfigDto config, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var row = new ConfigPrecosRow
        {
            Id = ConfigPrecosRow.DefaultId,
            CreditUnitPriceBrl = config.CreditUnitPriceBRL,
            SingleDiscountPercent = config.SingleDiscountPercent,
            Pack3DiscountPercent = config.Pack3DiscountPercent,
            Pack5DiscountPercent = config.Pack5DiscountPercent,
            EnglishPriceBrl = config.EnglishPriceBRL,
            EnglishBundlePriceBrl = config.EnglishBundlePriceBRL,
            TransactionFeeBrl = config.TransactionFeeBRL,
            SinglePriceOverride = config.SinglePriceOverride,
            Pack3PriceOverride = config.Pack3PriceOverride,
            Pack5PriceOverride = config.Pack5PriceOverride,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        await _client!
            .From<ConfigPrecosRow>()
            .Upsert(row);
    }

    private static PricingConfigDto MapPricingConfigRow(ConfigPrecosRow row) => new()
    {
        CreditUnitPriceBRL = row.CreditUnitPriceBrl,
        SingleDiscountPercent = row.SingleDiscountPercent,
        Pack3DiscountPercent = row.Pack3DiscountPercent,
        Pack5DiscountPercent = row.Pack5DiscountPercent,
        EnglishPriceBRL = row.EnglishPriceBrl,
        EnglishBundlePriceBRL = row.EnglishBundlePriceBrl,
        TransactionFeeBRL = row.TransactionFeeBrl,
        SinglePriceOverride = row.SinglePriceOverride,
        Pack3PriceOverride = row.Pack3PriceOverride,
        Pack5PriceOverride = row.Pack5PriceOverride
    };

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

    public async Task DeleteUserAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        await _client!
            .From<CreditoRow>()
            .Filter("id_usuario", Operator.Equals, userId)
            .Delete();

        await _client!
            .From<CompraRow>()
            .Filter("id_usuario", Operator.Equals, userId)
            .Delete();

        await _client!
            .From<IndicacaoParceiroRow>()
            .Filter("id_usuario", Operator.Equals, userId)
            .Delete();

        await _client!
            .From<PerfilUsuarioRow>()
            .Filter("id", Operator.Equals, userId)
            .Delete();
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

        var purchasesResponse = await _client
            .From<CompraRow>()
            .Select("preco,status")
            .Get();

        var estimatedRevenue = purchasesResponse.Models
            .Where(p => p.Status is "concluida" or "completed")
            .Sum(p => p.Preco ?? 0);

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
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(resumeId))
        {
            return false;
        }

        await EnsureInitializedAsync(cancellationToken);
        var query = _client
            .From<SimulacaoEntrevistaRow>()
            .Select("id")
            .Filter("id_curriculo", Operator.Equals, resumeId);

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Filter("id_usuario", Operator.Equals, userId);
        }

        var response = await query.Limit(1).Get();

        return response.Models.Count > 0;
    }

    public async Task<bool> GrantEnglishPaidAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        return await MarkServiceUsedAsync(analysisId, AnalysisBundledServiceKeys.CurriculoInglesPago, cancellationToken);
    }

    public async Task<bool> HasEnglishPaidAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(analysisId))
        {
            return false;
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<AnaliseCurriculoRow>()
            .Select("servicos_utilizados")
            .Filter("id", Operator.Equals, analysisId)
            .Get();

        var row = response.Models.FirstOrDefault();
        if (row == null)
        {
            return false;
        }

        var status = AnalysisServicesStatusHelper.ParseStatus(row.ServicosUtilizados);
        return status.GetValueOrDefault(AnalysisBundledServiceKeys.CurriculoInglesPago);
    }

    public async Task TryGrantBundledEnglishFromCreditAsync(
        string userId,
        string creditId,
        string analysisId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null ||
            string.IsNullOrEmpty(userId) ||
            string.IsNullOrEmpty(creditId) ||
            string.IsNullOrEmpty(analysisId))
        {
            return;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var creditResponse = await _client
                .From<CreditoRow>()
                .Select("id_compra")
                .Filter("id", Operator.Equals, creditId)
                .Filter("id_usuario", Operator.Equals, userId)
                .Get();

            var credit = creditResponse.Models.FirstOrDefault();
            if (string.IsNullOrEmpty(credit?.IdCompra))
            {
                return;
            }

            var pendingEnglish = await FindPendingBundledEnglishPurchaseAsync(credit.IdCompra, cancellationToken);
            if (pendingEnglish == null)
            {
                return;
            }

            await GrantEnglishPaidAsync(analysisId, cancellationToken);
            await _client
                .From<CompraRow>()
                .Filter("id", Operator.Equals, pendingEnglish.Id)
                .Set(x => x.IdAnalise, analysisId)
                .Update();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível aplicar inglês do bundle na análise {AnalysisId}", analysisId);
        }
    }

    public async Task<CompraRow?> FindPendingBundledEnglishPurchaseAsync(
        string parentPurchaseId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(parentPurchaseId))
        {
            return null;
        }

        await EnsureInitializedAsync(cancellationToken);
        var response = await _client
            .From<CompraRow>()
            .Select("*")
            .Filter("id_compra_pai", Operator.Equals, parentPurchaseId)
            .Filter("id_plano", Operator.Equals, "english")
            .Filter("tipo_servico", Operator.Equals, "curriculo_ingles")
            .Get();

        return response.Models.FirstOrDefault(p => string.IsNullOrEmpty(p.IdAnalise));
    }

    public async Task<int> GetPendingEnglishCreditsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var response = await _client
                .From<CompraRow>()
                .Select("id,id_analise")
                .Filter("id_usuario", Operator.Equals, userId)
                .Filter("id_plano", Operator.Equals, "english")
                .Filter("tipo_servico", Operator.Equals, "curriculo_ingles")
                .Filter("status", Operator.Equals, "concluida")
                .Get();

            return response.Models.Count(p => string.IsNullOrEmpty(p.IdAnalise));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao contar créditos de inglês pendentes para usuário {UserId}", userId);
            return 0;
        }
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

        var hasInterview = await HasInterviewForResumeAsync(row.IdCurriculo ?? "", row.IdUsuario, cancellationToken);
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
            await HasInterviewForResumeAsync(row.IdCurriculo, row.IdUsuario, cancellationToken);
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

    public async Task<SimulacaoEntrevistaRow?> GetLatestInterviewForResumeAsync(
        string userId,
        string resumeId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var response = await _client!
            .From<SimulacaoEntrevistaRow>()
            .Select("*")
            .Filter("id_usuario", Operator.Equals, userId)
            .Filter("id_curriculo", Operator.Equals, resumeId)
            .Order("criado_em", Ordering.Descending)
            .Limit(1)
            .Get();

        return response.Models.FirstOrDefault();
    }

    public async Task SaveStructuredPhaseAsync(
        string simulationId,
        int phaseIndex,
        string interviewerScript,
        string candidateAnswer,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var orderBase = phaseIndex * 2 + 1;
        var questionInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "pergunta",
            Conteudo = interviewerScript,
            Ordem = orderBase,
            DadosExtras = new Dictionary<string, object?>
            {
                ["phaseIndex"] = phaseIndex,
                ["structured"] = true
            }
        };

        var answerInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "resposta",
            Conteudo = candidateAnswer,
            Ordem = orderBase + 1,
            DadosExtras = new Dictionary<string, object?>
            {
                ["phaseIndex"] = phaseIndex,
                ["structured"] = true
            }
        };

        await _client!.From<MensagemEntrevistaInsert>().Insert(questionInsert);
        await _client.From<MensagemEntrevistaInsert>().Insert(answerInsert);
    }

    public async Task SaveStructuredWrittenAnswersAsync(
        string simulationId,
        IReadOnlyList<string> questions,
        IReadOnlyList<string> answers,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var count = Math.Min(questions.Count, answers.Count);
        for (var i = 0; i < count; i++)
        {
            await SaveStructuredPhaseAsync(
                simulationId,
                i,
                questions[i] ?? "",
                answers[i] ?? "",
                cancellationToken);
        }
    }

    public async Task SaveStructuredFeedbackAsync(
        string simulationId,
        string feedbackScript,
        InterviewEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var feedbackInsert = new MensagemEntrevistaInsert
        {
            IdSimulacao = simulationId,
            Tipo = "feedback",
            Conteudo = JsonSerializer.Serialize(new
            {
                script = feedbackScript,
                evaluation
            }),
            Feedback = evaluation.Feedback,
            Ordem = 100,
            DadosExtras = new Dictionary<string, object?>
            {
                ["structured"] = true,
                ["score"] = evaluation.Score,
                ["strengths"] = evaluation.Strengths,
                ["improvements"] = evaluation.Improvements,
                ["videoScript"] = feedbackScript
            }
        };

        await _client!.From<MensagemEntrevistaInsert>().Insert(feedbackInsert);
    }

    public async Task UpdateInterviewQuestionsAsync(
        string simulationId,
        List<string> questions,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var update = new SimulacaoEntrevistaRow
        {
            Id = simulationId,
            PerguntasFeitas = questions,
            AtualizadoEm = DateTimeOffset.UtcNow
        };

        await _client!
            .From<SimulacaoEntrevistaRow>()
            .Filter("id", Operator.Equals, simulationId)
            .Update(update);
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

    public async Task<PartnerReferralDto?> GetPartnerReferralByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _client
                .From<IndicacaoParceiroRow>()
                .Select("*")
                .Filter("id_usuario", Operator.Equals, userId)
                .Get();

            var row = response.Models.FirstOrDefault();
            if (row == null || string.IsNullOrEmpty(row.IdCupom))
            {
                return null;
            }

            var coupon = await GetCouponByIdAsync(row.IdCupom, cancellationToken);
            string? partnerName = null;
            if (!string.IsNullOrEmpty(row.IdParceiro))
            {
                var partnerResponse = await _client
                    .From<ParceiroRow>()
                    .Select("nome")
                    .Filter("id", Operator.Equals, row.IdParceiro)
                    .Get();
                partnerName = partnerResponse.Models.FirstOrDefault()?.Nome;
            }

            return new PartnerReferralDto
            {
                CouponId = row.IdCupom,
                CouponCode = row.CodigoCupom ?? coupon?.Nome ?? string.Empty,
                DiscountPercent = coupon?.PorcentagemDesconto ?? 0,
                PartnerId = row.IdParceiro,
                PartnerName = partnerName,
                LinkedAt = row.CriadoEm
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar indicação de parceiro do usuário {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> UserHasPartnerReferralAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var referral = await GetPartnerReferralByUserIdAsync(userId, cancellationToken);
        return referral != null;
    }

    public async Task RegisterPartnerReferralAsync(
        string userId,
        string couponCode,
        CancellationToken cancellationToken = default)
    {
        if (_client == null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(couponCode))
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken);

        if (await UserHasPartnerReferralAsync(userId, cancellationToken))
        {
            return;
        }

        var coupon = await GetCouponByCodeAsync(couponCode, cancellationToken);
        if (coupon == null)
        {
            throw new InvalidOperationException("Cupom inválido ou inativo.");
        }

        var insert = new IndicacaoParceiroInsert
        {
            IdUsuario = userId,
            IdCupom = coupon.Id,
            CodigoCupom = coupon.Nome ?? couponCode.Trim().ToUpperInvariant(),
            IdParceiro = coupon.IdParceiro,
            CriadoEm = DateTimeOffset.UtcNow
        };

        try
        {
            await _client.From<IndicacaoParceiroInsert>().Insert(insert);
        }
        catch (PostgrestException ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Usuário {UserId} já possui indicação de parceiro", userId);
        }
    }

    public async Task<List<PartnerReferralAdminDto>> ListPartnerReferralsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new List<PartnerReferralAdminDto>();
        }

        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var referralsResponse = await _client
                .From<IndicacaoParceiroRow>()
                .Select("*")
                .Order("criado_em", Postgrest.Constants.Ordering.Descending)
                .Get();

            if (referralsResponse.Models.Count == 0)
            {
                return new List<PartnerReferralAdminDto>();
            }

            var userIds = referralsResponse.Models
                .Select(r => r.IdUsuario)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var usersById = new Dictionary<string, PerfilUsuarioRow>();
            foreach (var userId in userIds)
            {
                var userResponse = await _client
                    .From<PerfilUsuarioRow>()
                    .Select("id, nome, email, cpf, criado_em")
                    .Filter("id", Operator.Equals, userId)
                    .Get();
                var user = userResponse.Models.FirstOrDefault();
                if (user != null)
                {
                    usersById[user.Id] = user;
                }
            }

            var parceirosResponse = await _client.From<ParceiroRow>().Select("id, nome").Get();
            var parceirosById = parceirosResponse.Models.ToDictionary(p => p.Id, p => p.Nome ?? string.Empty);

            var cuponsResponse = await _client.From<CupomRow>().Select("id, nome, porcentagem_desconto").Get();
            var cuponsById = cuponsResponse.Models.ToDictionary(c => c.Id, c => c);

            return referralsResponse.Models.Select(row =>
            {
                usersById.TryGetValue(row.IdUsuario ?? string.Empty, out var user);
                cuponsById.TryGetValue(row.IdCupom ?? string.Empty, out var cupom);
                var partnerName = row.IdParceiro != null && parceirosById.TryGetValue(row.IdParceiro, out var pn)
                    ? pn
                    : null;

                return new PartnerReferralAdminDto
                {
                    Id = row.Id,
                    UserId = row.IdUsuario ?? string.Empty,
                    UserName = user?.Nome ?? "—",
                    UserEmail = user?.Email,
                    UserCpf = user?.Cpf,
                    UserCreatedAt = user?.CriadoEm,
                    CouponId = row.IdCupom ?? string.Empty,
                    CouponCode = row.CodigoCupom ?? cupom?.Nome ?? string.Empty,
                    DiscountPercent = cupom?.PorcentagemDesconto ?? 0,
                    PartnerId = row.IdParceiro,
                    PartnerName = partnerName,
                    LinkedAt = row.CriadoEm
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar indicações de parceiro");
            return new List<PartnerReferralAdminDto>();
        }
    }

    public async Task<Dictionary<string, int>> CountReferralsByCouponAsync(
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new Dictionary<string, int>();
        }

        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _client.From<IndicacaoParceiroRow>().Select("id_cupom").Get();
            return response.Models
                .Where(r => !string.IsNullOrEmpty(r.IdCupom))
                .GroupBy(r => r.IdCupom!)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao contar indicações por cupom");
            return new Dictionary<string, int>();
        }
    }

    public async Task<KiwifyWebhookLogDto> CreateKiwifyWebhookLogAsync(
        CreateKiwifyWebhookLogRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await EnsureInitializedAsync(cancellationToken);

        var row = new KiwifyWebhookLogRow
        {
            Id = Guid.NewGuid().ToString(),
            PayloadRecebido = TruncateLogText(request.PayloadRecebido, 100_000),
            PayloadParseado = TruncateLogText(request.PayloadParseado, 100_000),
            OrderId = request.OrderId,
            OrderRef = request.OrderRef,
            EventType = request.EventType,
            PaymentStatus = request.PaymentStatus,
            Processed = request.Processed,
            AlreadyFulfilled = request.AlreadyFulfilled,
            Credits = request.Credits,
            IdUsuario = request.UserId,
            HttpStatus = request.HttpStatus,
            ApiVersion = request.ApiVersion,
            Message = TruncateLogText(request.Message, 4000),
            RespostaJson = TruncateLogText(request.RespostaJson, 100_000),
            Erro = TruncateLogText(request.Erro, 8000),
            EstagioFalha = request.FailureStage,
            DetalhesProcessamento = TruncateLogText(request.ProcessingDetails, 20_000),
            CriadoEm = DateTimeOffset.UtcNow
        };

        var response = await _client!
            .From<KiwifyWebhookLogRow>()
            .Insert(row);

        var saved = response.Models.FirstOrDefault() ?? row;
        return MapKiwifyWebhookLog(saved);
    }

    public Task<KiwifyWebhookLogDto> CreateAsync(
        CreateKiwifyWebhookLogRequest request,
        CancellationToken cancellationToken = default) =>
        CreateKiwifyWebhookLogAsync(request, cancellationToken);

    public async Task<List<KiwifyWebhookLogDto>> ListAsync(
        string? orderId = null,
        string? orderRef = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return new List<KiwifyWebhookLogDto>();
        }

        await EnsureInitializedAsync(cancellationToken);
        limit = Math.Clamp(limit, 1, 200);

        try
        {
            var query = _client
                .From<KiwifyWebhookLogRow>()
                .Select("*")
                .Order("criado_em", Ordering.Descending)
                .Limit(limit);

            if (!string.IsNullOrWhiteSpace(orderRef))
            {
                query = query.Filter("order_ref", Operator.Equals, orderRef.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(orderId))
            {
                query = query.Filter("order_id", Operator.Equals, orderId.Trim());
            }

            var response = await query.Get(cancellationToken);
            return response.Models.Select(MapKiwifyWebhookLog).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar logs de webhook Kiwify");
            return new List<KiwifyWebhookLogDto>();
        }
    }

    private static KiwifyWebhookLogDto MapKiwifyWebhookLog(KiwifyWebhookLogRow row) =>
        new()
        {
            Id = row.Id,
            PayloadRecebido = row.PayloadRecebido,
            PayloadParseado = row.PayloadParseado,
            OrderId = row.OrderId,
            OrderRef = row.OrderRef,
            EventType = row.EventType,
            PaymentStatus = row.PaymentStatus,
            Processed = row.Processed,
            AlreadyFulfilled = row.AlreadyFulfilled,
            Credits = row.Credits,
            UserId = row.IdUsuario,
            HttpStatus = row.HttpStatus,
            ApiVersion = row.ApiVersion,
            Message = row.Message,
            RespostaJson = row.RespostaJson,
            Erro = row.Erro,
            FailureStage = row.EstagioFalha,
            ProcessingDetails = row.DetalhesProcessamento,
            CreatedAt = row.CriadoEm
        };

    private static string? TruncateLogText(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
