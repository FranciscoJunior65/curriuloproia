using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile?> MapProfileToEnglishAsync(PerfilUsuarioRow? profile, CancellationToken cancellationToken = default);
    Task<UserProfile> GetOrCreateUserProfileAsync(
        string userId, string email, string name = "", string? passwordHash = null,
        bool emailVerified = false, string? verificationCode = null, string? cpf = null,
        CancellationToken cancellationToken = default);
    Task<UserProfile?> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetUserProfileByEmailAsync(string email, bool includePassword = false, CancellationToken cancellationToken = default);
    Task<List<UserProfile>> SearchUsersAsync(string query, int limit = 20, CancellationToken cancellationToken = default);
    Task<List<AdminUserListItemDto>> ListUsersAsync(int limit = 300, int offset = 0, string? search = null, CancellationToken cancellationToken = default);
    Task<bool> VerifyUserPasswordAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<UserProfile> UpdateUserProfileAsync(string userId, Dictionary<string, object?> updates, CancellationToken cancellationToken = default);
    Task<UserProfile> UpdateVerificationCodeAsync(string userId, string code, int expiresInMinutes = 15, CancellationToken cancellationToken = default);
    Task<UserProfile> VerifyEmailCodeAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<UserProfile> VerifyLoginCodeAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<AddCreditsResultDto> AddCreditsToUserAsync(string userId, int amount, CancellationToken cancellationToken = default);
    Task<DeductCreditsResultDto> DeductCreditsFromUserAsync(string userId, int amount = 1, CancellationToken cancellationToken = default);
    Task<UserProfile> UpdateVerificationTokenAsync(string userId, string token, int expiresInHours = 1, CancellationToken cancellationToken = default);
    Task<UserProfile> VerifyEmailTokenAsync(string? email, string token, CancellationToken cancellationToken = default);
    Task<UserProfile> GetUserByResetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task DeleteUserAccountAsync(string userId, CancellationToken cancellationToken = default);
}
