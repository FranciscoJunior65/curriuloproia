namespace CurriculosProIA.Domain.Signatures.Auth;

public class DeleteAccountSignature
{
    /// <summary>Senha atual (obrigatória se a conta possuir senha).</summary>
    public string? Password { get; set; }

    /// <summary>Deve ser exatamente "EXCLUIR" para confirmar a exclusão.</summary>
    public string? Confirmation { get; set; }
}
