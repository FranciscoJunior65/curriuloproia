using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IPaymentCheckoutService
{
    Task<CheckoutContext> BuildCheckoutContextAsync(
        string planId,
        string userId,
        string? couponCode = null,
        string? cpf = null,
        CancellationToken cancellationToken = default);
}
