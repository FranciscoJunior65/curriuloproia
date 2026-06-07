using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface ISimliService
{
    SimliConfigDto GetConfig();
    Task<SimliSessionResponseDto> CreateSessionAsync(
        string? faceId,
        string? personaInitials,
        CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeSpeechMp3Async(
        string text,
        string? voice,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dictionary<string, object>>> GetIceServersAsync(
        CancellationToken cancellationToken = default);
}
