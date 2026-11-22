using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación local de <see cref="IAuthProviderService"/> basada en <see cref="IMockDataService"/>.
/// </summary>
public sealed class MockAuthProviderService : IAuthProviderService
{
    private readonly IMockDataService _mockDataService;
    private readonly ILogger<MockAuthProviderService> _logger;

    public MockAuthProviderService(
        IMockDataService mockDataService,
        ILogger<MockAuthProviderService> logger)
    {
        _mockDataService = mockDataService;
        _logger = logger;
    }

    public async Task<AuthLoginResult> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var safeUsername = SanitizeForLog(request.Username);
        var mockResult = await _mockDataService.AuthenticateAsync(request);

        if (!mockResult.Success || mockResult.User == null)
        {
            _logger.LogWarning("Autenticación fallida (Mock BFF) para usuario '{Username}'.", safeUsername);
            return new AuthLoginResult(
                false,
                StatusCodes.Status401Unauthorized,
                mockResult.Message ?? "Usuario o Clave incorrectos",
                null,
                null);
        }

        var mockToken = $"vogel.mock_session_token.{Guid.NewGuid():N}";
        _logger.LogInformation("Autenticación exitosa (Mock BFF) para usuario '{Username}'.", safeUsername);

        return new AuthLoginResult(
            true,
            StatusCodes.Status200OK,
            string.Empty,
            mockResult.User,
            mockToken);
    }

    public async Task<AuthPrivilegesResult> GetPrivilegesAsync(
        string token,
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();
        var allPrivileges = new List<Privilege>(rootPrivileges);
        foreach (var root in rootPrivileges)
        {
            var subs = await _mockDataService.GetSubPrivilegesAsync(root.Id);
            allPrivileges.AddRange(subs);
        }

        var privilegeItems = allPrivileges.Select(p => new BackendPrivilegeItemDto(
            p.IdParent,
            p.Id,
            p.Codename,
            p.Group ?? string.Empty,
            p.Title,
            p.Action,
            p.Icon ?? string.Empty,
            new BackendAccessDto(
                p.Access?.Read ?? true,
                p.Access?.Modify ?? false,
                p.Access?.Create ?? false,
                p.Access?.Delete ?? false,
                p.Access?.Approve ?? false,
                p.Access?.Validate ?? false,
                p.Access?.Execute ?? false)
        )).ToList();

        var responseDto = new BackendPrivilegesResponseDto("mock_user", privilegeItems);
        return new AuthPrivilegesResult(true, StatusCodes.Status200OK, responseDto, null);
    }

    public Task<AuthChangePasswordResult> ChangePasswordAsync(
        string? token,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AuthChangePasswordResult(
            true,
            StatusCodes.Status200OK,
            new ChangePasswordResponseDto
            {
                Success = true,
                Message = "Contraseña actualizada exitosamente (Modo Mock)."
            }));
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
