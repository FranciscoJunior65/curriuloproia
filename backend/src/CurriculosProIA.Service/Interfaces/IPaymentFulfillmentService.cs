using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IPaymentFulfillmentService
{
    Task<FulfillOrderResult> FulfillPaidOrderAsync(FulfillOrderRequest request, CancellationToken cancellationToken = default);
    Task<FulfillOrderResult> FulfillFreeCheckoutAsync(FulfillOrderRequest request, CancellationToken cancellationToken = default);
}
