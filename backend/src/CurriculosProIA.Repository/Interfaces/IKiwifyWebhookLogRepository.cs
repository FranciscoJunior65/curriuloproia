using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface IKiwifyWebhookLogRepository
{
    Task<KiwifyWebhookLogDto> CreateAsync(
        CreateKiwifyWebhookLogRequest request,
        CancellationToken cancellationToken = default);

    Task<List<KiwifyWebhookLogDto>> ListAsync(
        string? orderId = null,
        string? orderRef = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
