using Postgrest.Attributes;
using Postgrest.Models;

namespace CurriculosProIA.Repository.Persistence;

[Table("perfis_usuarios")]
public class PerfilUsuarioRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("nome")]
    public string? Nome { get; set; }

    [Column("cpf")]
    public string? Cpf { get; set; }

    [Column("data_nascimento")]
    public string? DataNascimento { get; set; }

    [Column("cidade")]
    public string? Cidade { get; set; }

    [Column("pais")]
    public string? Pais { get; set; }

    [Column("plano")]
    public string? Plano { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [Column("ultima_analise")]
    public DateTimeOffset? UltimaAnalise { get; set; }

    [Column("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }

    [Column("email_verificado")]
    public bool? EmailVerificado { get; set; }

    [Column("codigo_verificacao")]
    public string? CodigoVerificacao { get; set; }

    [Column("codigo_verificacao_expira_em")]
    public DateTimeOffset? CodigoVerificacaoExpiraEm { get; set; }

    [Column("tipo_usuario")]
    public string? TipoUsuario { get; set; }

    [Column("hash_senha")]
    public string? HashSenha { get; set; }
}

[Table("compras")]
public class CompraRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("id_usuario")]
    public string? IdUsuario { get; set; }

    [Column("id_plano")]
    public string? IdPlano { get; set; }

    [Column("nome_plano")]
    public string? NomePlano { get; set; }

    [Column("quantidade_creditos")]
    public int? QuantidadeCreditos { get; set; }

    [Column("preco")]
    public decimal? Preco { get; set; }

    [Column("moeda")]
    public string? Moeda { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("metodo_pagamento")]
    public string? MetodoPagamento { get; set; }

    [Column("id_pagamento")]
    public string? IdPagamento { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }

    [Column("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }

    [Column("id_compra_pai")]
    public string? IdCompraPai { get; set; }

    [Column("tipo_servico")]
    public string? TipoServico { get; set; }

    [Column("id_cupom")]
    public string? IdCupom { get; set; }

    [Column("nome_cupom")]
    public string? NomeCupom { get; set; }

    [Column("porcentagem_desconto_aplicado")]
    public decimal? PorcentagemDescontoAplicado { get; set; }

    [Column("preco_original")]
    public decimal? PrecoOriginal { get; set; }
}

[Table("creditos")]
public class CreditoRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("id_compra")]
    public string? IdCompra { get; set; }

    [Column("id_usuario")]
    public string? IdUsuario { get; set; }

    [Column("usado")]
    public bool? Usado { get; set; }

    [Column("usado_em")]
    public DateTimeOffset? UsadoEm { get; set; }

    [Column("tipo_acao")]
    public string? TipoAcao { get; set; }

    [Column("nome_arquivo_curriculo")]
    public string? NomeArquivoCurriculo { get; set; }

    [Column("id_site_vagas")]
    public string? IdSiteVagas { get; set; }

    [Column("criado_em")]
    public DateTimeOffset? CriadoEm { get; set; }
}

[Table("cupons")]
public class CupomRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("nome")]
    public string? Nome { get; set; }

    [Column("porcentagem_desconto")]
    public decimal? PorcentagemDesconto { get; set; }

    [Column("ativo")]
    public bool? Ativo { get; set; }
}

[Table("cupom_uso")]
public class CupomUsoRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string? Id { get; set; }

    [Column("id_cupom")]
    public string? IdCupom { get; set; }

    [Column("cpf_normalizado")]
    public string? CpfNormalizado { get; set; }
}

[Table("cupom_uso")]
public class CupomUsoInsert : BaseModel
{
    [Column("id_cupom")]
    public string IdCupom { get; set; } = string.Empty;

    [Column("cpf_normalizado")]
    public string CpfNormalizado { get; set; } = string.Empty;
}

[Table("app_configuracoes")]
public class AppConfiguracaoRow : BaseModel
{
    [PrimaryKey("chave", false)]
    [Column("chave")]
    public string Chave { get; set; } = string.Empty;

    [Column("valor")]
    public string Valor { get; set; } = string.Empty;

    [Column("atualizado_em")]
    public DateTimeOffset? AtualizadoEm { get; set; }
}

[Table("sites_vagas")]
public class SiteVagasRow : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("nome")]
    public string? Nome { get; set; }

    [Column("url_base")]
    public string? UrlBase { get; set; }

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("ativo")]
    public bool? Ativo { get; set; }

    [Column("palavras_chave_padrao")]
    public List<string>? PalavrasChavePadrao { get; set; }

    [Column("caracteristicas")]
    public Dictionary<string, object>? Caracteristicas { get; set; }
}
