using Vogel.Shared.DTOs;
using Vogel.Shared.Models;
using Vogel.Shared.Services;

namespace Vogel.Server.Services;

/// <summary>
/// Implementación local de <see cref="IPrivilegeAdminProviderService"/> basada en <see cref="IMockDataService"/>.
/// </summary>
public sealed class MockPrivilegeAdminService : IPrivilegeAdminProviderService
{
    private const string MockSystemCode = "VOGEL-MOCK";
    private const int MockSystemId = 1;

    private readonly IMockDataService _mockDataService;
    private readonly ILogger<MockPrivilegeAdminService> _logger;

    public MockPrivilegeAdminService(
        IMockDataService mockDataService,
        ILogger<MockPrivilegeAdminService> logger)
    {
        _mockDataService = mockDataService;
        _logger = logger;
    }

    public async Task<PrivilegeAdminListResult> ListPrivilegesAsync(
        string token,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PrivilegeAdminListResult(
                false,
                StatusCodes.Status401Unauthorized,
                null,
                new BackendErrorDto(StatusCodes.Status401Unauthorized, "Token de sesión no proporcionado."));
        }

        var rootPrivileges = await _mockDataService.GetRootPrivilegesAsync();
        var visited = new HashSet<int>();
        var queue = new Queue<Privilege>();
        var allPrivileges = new List<Privilege>();

        foreach (var root in rootPrivileges)
        {
            if (visited.Add(root.Id))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            allPrivileges.Add(current);

            var children = await _mockDataService.GetSubPrivilegesAsync(current.Id);
            foreach (var child in children)
            {
                if (visited.Add(child.Id))
                {
                    queue.Enqueue(child);
                }
            }
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToUpperInvariant();

        var items = allPrivileges
            .Select(p => new PrivilegeAdminItemDto
            {
                IdPrivilege = p.Id,
                IdSystem = MockSystemId,
                IdParent = p.IdParent,
                Codename = p.Codename,
                Title = p.Title,
                GroupName = p.Group ?? string.Empty,
                Action = p.Action,
                Icon = p.Icon,
                Status = "A"
            })
            .Where(item => normalizedStatus is null or "ALL" ||
                           string.Equals(item.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var response = new PrivilegeAdminListResponseDto
        {
            SystemCode = MockSystemCode,
            IdSystem = MockSystemId,
            Items = items,
            Total = items.Count
        };

        _logger.LogInformation(
            "Consulta administrativa de privilegios (Mock BFF) exitosa ({Count} registros).",
            response.Items.Count);

        return new PrivilegeAdminListResult(true, StatusCodes.Status200OK, response, null);
    }
}
