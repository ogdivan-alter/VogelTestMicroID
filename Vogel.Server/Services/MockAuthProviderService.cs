using Microsoft.Extensions.DependencyInjection;
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
    private readonly MockSessionStore _sessionStore;
    private readonly ILogger<MockAuthProviderService> _logger;

    [ActivatorUtilitiesConstructor]
    public MockAuthProviderService(
        IMockDataService mockDataService,
        MockSessionStore sessionStore,
        ILogger<MockAuthProviderService> logger)
    {
        _mockDataService = mockDataService;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>
    /// Constructor de compatibilidad para pruebas unitarias heredadas.
    /// </summary>
    public MockAuthProviderService(
        IMockDataService mockDataService,
        ILogger<MockAuthProviderService> logger)
        : this(mockDataService, new MockSessionStore(), logger)
    {
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
        _sessionStore.RegisterSession(mockToken, mockResult.User.Username);
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
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthPrivilegesResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        string? resolvedUsername = null;
        if (_sessionStore.TryGetUsername(token, out var sessionUsername))
        {
            resolvedUsername = sessionUsername;
        }
        else if (!string.IsNullOrWhiteSpace(username))
        {
            resolvedUsername = username.Trim();
        }

        if (string.IsNullOrWhiteSpace(resolvedUsername))
        {
            _logger.LogWarning("Consulta de privilegios (Mock BFF) rechazada: sesión no válida o expirada.");
            return new AuthPrivilegesResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Sesión no válida o expirada."));
        }

        var user = await _mockDataService.GetUserByUsernameAsync(resolvedUsername);
        if (user is null)
        {
            _logger.LogWarning(
                "Consulta de privilegios (Mock BFF) rechazada: usuario '{Username}' no encontrado.",
                SanitizeForLog(resolvedUsername));
            return new AuthPrivilegesResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Sesión no válida o expirada."));
        }

        var privilegeItems = (user.Privileges ?? new List<Privilege>()).Select(p => new BackendPrivilegeItemDto(
            p.IdParent,
            p.Id,
            p.Codename,
            p.Group ?? string.Empty,
            p.Title,
            p.Action,
            p.Icon ?? string.Empty,
            new BackendAccessDto(
                p.Access?.Read ?? false,
                p.Access?.Modify ?? false,
                p.Access?.Create ?? false,
                p.Access?.Delete ?? false,
                p.Access?.Approve ?? false,
                p.Access?.Validate ?? false,
                p.Access?.Execute ?? false)
        )).ToList();

        var responseDto = new BackendPrivilegesResponseDto(user.Username, privilegeItems);
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
