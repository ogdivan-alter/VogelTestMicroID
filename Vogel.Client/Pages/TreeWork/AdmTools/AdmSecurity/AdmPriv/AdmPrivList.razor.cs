using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;
using Vogel.Shared.Models;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind de la vista List de Administración de Privilegios.
/// Renderiza los privilegios filtrados en ListViewFrame respetando permisos RBAC de adm_priv,
/// se suscribe reactivamente a AdmPrivState y coordina la navegación a edición/detalle (activos)
/// y la reactivación lógica (inactivos).
/// </summary>
public partial class AdmPrivList : ComponentBase, IDisposable
{
    private const string ModulePrivilegeCodename = "adm_priv";

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public BlockWork? BlockWork { get; set; }

    protected bool CanRead => SessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Read);

    protected bool CanCreate => SessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Create);

    protected bool CanModify => SessionState.HasPrivilegeAccess(ModulePrivilegeCodename, PrivilegeAction.Modify);

    /// <summary>
    /// Identificador del privilegio que actualmente se encuentra en proceso de reactivación
    /// para bloquear clics duplicados en esa fila.
    /// </summary>
    protected int? ReactivatingPrivilegeId { get; private set; }

    protected bool HasSuccessMessage => !string.IsNullOrWhiteSpace(AdmPrivState.SuccessMessage);

    protected bool HasErrorMessage => !string.IsNullOrWhiteSpace(AdmPrivState.ErrorMessage);

    protected override void OnInitialized()
    {
        AdmPrivState.OnStateChanged += HandleStateChanged;
    }

    protected bool IsActive(PrivilegeAdminItemDto item)
    {
        return item is not null && string.Equals(item.Status, "A", StringComparison.OrdinalIgnoreCase);
    }

    protected bool IsInactive(PrivilegeAdminItemDto item)
    {
        return item is not null && string.Equals(item.Status, "I", StringComparison.OrdinalIgnoreCase);
    }

    protected bool IsReactivating(int idPrivilege)
    {
        return ReactivatingPrivilegeId == idPrivilege;
    }

    protected Task HandlePageChangedAsync(int page)
    {
        AdmPrivState.SetPage(page);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Almacena en memoria el identificador del privilegio activo y navega a la ruta limpia /admsec/priv/edit.
    /// </summary>
    protected void HandleEditClick(PrivilegeAdminItemDto item)
    {
        if (!CanModify || !CanRead || !IsActive(item))
        {
            return;
        }

        AdmPrivState.ClearMessages();
        AdmPrivState.SetSelectedEditPrivilegeId(item.IdPrivilege);
        NavigationManager.NavigateTo("/admsec/priv/edit");
    }

    /// <summary>
    /// Almacena en memoria el identificador del privilegio activo y navega a /admsec/priv/edit en modo solo lectura.
    /// </summary>
    protected void HandleViewClick(PrivilegeAdminItemDto item)
    {
        if (!CanRead || !IsActive(item))
        {
            return;
        }

        AdmPrivState.ClearMessages();
        AdmPrivState.SetSelectedEditPrivilegeId(item.IdPrivilege);
        NavigationManager.NavigateTo("/admsec/priv/edit");
    }

    /// <summary>
    /// Reactiva lógicamente un privilegio inactivo contra el BFF con protección contra doble clic por fila.
    /// </summary>
    protected async Task HandleReactivateClickAsync(PrivilegeAdminItemDto item)
    {
        if (!CanCreate || !IsInactive(item) || ReactivatingPrivilegeId.HasValue)
        {
            return;
        }

        ReactivatingPrivilegeId = item.IdPrivilege;
        StateHasChanged();

        try
        {
            await AdmPrivState.ReactivateAsync(item.IdPrivilege);
        }
        finally
        {
            ReactivatingPrivilegeId = null;
            StateHasChanged();
        }
    }

    protected void DismissAlert()
    {
        AdmPrivState.ClearMessages();
    }

    private void HandleStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AdmPrivState.OnStateChanged -= HandleStateChanged;
    }
}
