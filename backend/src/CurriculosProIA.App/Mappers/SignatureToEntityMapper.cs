using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Signatures.Auth;

namespace CurriculosProIA.App.Mappers;

public static class SignatureToEntityMapper
{
    public static Dictionary<string, object?> ToProfileUpdates(UpdateProfileSignature signature)
    {
        var updates = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(signature.Name))
            updates["name"] = signature.Name.Trim();
        if (!string.IsNullOrWhiteSpace(signature.Email))
            updates["email"] = signature.Email.Trim();
        if (!string.IsNullOrWhiteSpace(signature.Cpf))
            updates["cpf"] = signature.Cpf.Trim();
        if (!string.IsNullOrWhiteSpace(signature.DateOfBirth))
            updates["date_of_birth"] = signature.DateOfBirth.Trim();
        if (!string.IsNullOrWhiteSpace(signature.City))
            updates["city"] = signature.City.Trim();
        if (!string.IsNullOrWhiteSpace(signature.Country))
            updates["country"] = signature.Country.Trim();
        return updates;
    }
}
