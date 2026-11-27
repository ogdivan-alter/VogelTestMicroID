using System.Net;
using Microsoft.AspNetCore.Components;
using Vogel.Client.Services;
using Vogel.Shared.DTOs;
using Vogel.Shared.Enums;

namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv;

/// <summary>
/// Code-behind del formulario reutilizable de Administración de Privilegios
/// para creación (/admsec/priv/create) y detalle/edición/inactivación (/admsec/priv/edit).
/// </summary>
public partial class AdmPrivForm : ComponentBase, IDisposable
{
    protected const int MaxCodenameLength = 100;
    protected const int MaxGroupNameLength = 100;
    protected const int MaxTitleLength = 150;
    protected const int MaxActionLength = 250;
    protected const int MaxIconLength = 100;
    protected const int MaxDelDescriptionLength = 500;

    [Inject]
    protected SessionState SessionState { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected IAdmPrivStateService AdmPrivState { get; set; } = default!;

    [Inject]
    protected ISecureSessionStorageService SecureStorage { get; set; } = default!;

    protected bool IsEditMode { get; private set; }
    protected bool IsLoadingInitial { get; private set; }
    protected bool IsSaving { get; private set; }
    protected bool IsDeactivating { get; private set; }
    protected bool IsDeactivated { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected bool ShowDeactivateConfirm { get; private set; }
    protected bool ShowExitConfirm { get; private set; }

    protected string? SuccessMessage { get; private set; }
    protected string? ErrorMessage { get; private set; }

    protected int IdPrivilege { get; private set; }
    protected int EffectiveIdSystem { get; private set; }
    protected string Status { get; private set; } = "A";

    protected string Codename { get; set; } = string.Empty;
    protected string Title { get; set; } = string.Empty;
    protected string GroupName { get; set; } = string.Empty;
    protected string ActionPath { get; set; } = string.Empty;
    protected int IdParent { get; set; }
    protected string Icon { get; set; } = string.Empty;
    protected string DelDescription { get; set; } = string.Empty;

    private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.Ordinal);
    private string? _lastInitializedRouteKey;

    private const string ModuleRouteAction = "/admsec/priv";

    protected bool CanRead => HasModuleAccess(PrivilegeAction.Read);

    protected bool CanCreate => HasModuleAccess(PrivilegeAction.Create);

    protected bool CanModify => HasModuleAccess(PrivilegeAction.Modify);

    protected bool CanDelete => HasModuleAccess(PrivilegeAction.Delete);

    private bool HasModuleAccess(PrivilegeAction action) =>
        SessionState.HasPrivilegeAccessByAction(ModuleRouteAction, action) ||
        SessionState.HasPrivilegeAccess(AdmPrivState.ModuleCodename, action);

    protected bool IsReadOnlyView => IsEditMode && !CanModify;

    protected string HeaderTitle =>
        !IsEditMode
            ? "Crear Privilegio"
            : (IsReadOnlyView ? "Detalle de Privilegio" : "Editar Privilegio");

    protected string PageTitleText => $"{HeaderTitle} — Vogel";

    protected string SubmitButtonText => IsEditMode ? "Guardar Cambios" : "Crear Privilegio";

    protected bool RequiresExitConfirmation =>
        !IsReadOnlyView && !IsDeactivated && !LoadFailed && !IsLoadingInitial && CanSubmitForm;

    protected bool IsFormDisabled => IsSaving || IsDeactivating || IsDeactivated || LoadFailed || IsReadOnlyView;

    protected bool CanSubmitForm => !LoadFailed && !IsDeactivated && (IsEditMode ? CanModify : CanCreate);

    protected bool CanShowDeactivateSection => IsEditMode && CanDelete && !IsDeactivated && !LoadFailed;

    protected string StatusDisplayText =>
        string.Equals(Status, "A", StringComparison.OrdinalIgnoreCase) ? "Activo" : "Inactivo";

    protected string SystemDisplayText
    {
        get
        {
            var code = string.IsNullOrWhiteSpace(AdmPrivState.SystemCode) ? "MICROID" : AdmPrivState.SystemCode;
            var sysId = EffectiveIdSystem > 0 ? EffectiveIdSystem : AdmPrivState.IdSystem;
            return sysId > 0 ? $"{code} (ID: {sysId})" : code;
        }
    }

    protected IEnumerable<PrivilegeAdminItemDto> AvailableParentPrivileges =>
        AdmPrivState.AllItems
            .Where(p => string.Equals(p.Status, "A", StringComparison.OrdinalIgnoreCase)
                        && (!IsEditMode || p.IdPrivilege != IdPrivilege))
            .OrderBy(p => p.IdPrivilege);

    protected override async Task OnParametersSetAsync()
    {
        if (!SessionState.IsAuthenticated)
        {
            AdmPrivState.ClearSelectedEditPrivilegeId();
            NavigationManager.NavigateTo("/login", replace: true);
            return;
        }

        var relativePath = GetNormalizedRelativePath();
        var editRoute = string.Equals(relativePath, "admsec/priv/edit", StringComparison.OrdinalIgnoreCase);

        if (!editRoute && !CanCreate)
        {
            AdmPrivState.ClearSelectedEditPrivilegeId();
            AdmPrivState.SetErrorMessage("No cuenta con permisos para crear privilegios.");
            NavigationManager.NavigateTo("/admsec/priv", replace: true);
            return;
        }

        if (editRoute && !CanRead)
        {
            AdmPrivState.ClearSelectedEditPrivilegeId();
            AdmPrivState.SetErrorMessage("No cuenta con permisos para consultar o editar privilegios.");
            NavigationManager.NavigateTo("/admsec/priv", replace: true);
            return;
        }

        var selectedId = editRoute ? AdmPrivState.SelectedEditPrivilegeId : null;
        var routeKey = editRoute ? $"edit:{selectedId?.ToString() ?? "none"}" : "create";

        if (string.Equals(_lastInitializedRouteKey, routeKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastInitializedRouteKey = routeKey;
        IsEditMode = editRoute;
        SuccessMessage = null;
        ErrorMessage = null;
        _fieldErrors.Clear();
        ShowDeactivateConfirm = false;
        ShowExitConfirm = false;
        DelDescription = string.Empty;
        LoadFailed = false;
        IsDeactivated = false;

        if (IsEditMode)
        {
            if (!selectedId.HasValue || selectedId.Value <= 0)
            {
                AdmPrivState.ClearSelectedEditPrivilegeId();
                NavigationManager.NavigateTo("/admsec/priv", replace: true);
                return;
            }

            await LoadEditDataAsync(selectedId.Value);
        }
        else
        {
            AdmPrivState.ClearSelectedEditPrivilegeId();
            await LoadCreateDataAsync();
        }
    }

    private async Task LoadCreateDataAsync()
    {
        IsLoadingInitial = true;
        try
        {
            if (CanRead && AdmPrivState.AllItems.Count == 0)
            {
                await AdmPrivState.LoadAsync();
            }

            IdPrivilege = 0;
            EffectiveIdSystem = AdmPrivState.IdSystem;
            Status = "A";
            ResetCreateFormFields();
        }
        finally
        {
            IsLoadingInitial = false;
        }
    }

    private async Task LoadEditDataAsync(int privilegeId)
    {
        IsLoadingInitial = true;
        try
        {
            if (AdmPrivState.AllItems.Count == 0)
            {
                await AdmPrivState.LoadAsync();
            }

            var detailResult = await AdmPrivState.GetByIdAsync(privilegeId);
            if (!detailResult.Success || detailResult.Data is null)
            {
                if (detailResult.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    await HandleUnauthorizedAsync();
                    return;
                }

                LoadFailed = true;
                ErrorMessage = detailResult.ErrorMessage ?? "No fue posible cargar el detalle del privilegio seleccionado.";
                return;
            }

            ApplyPrivilegeToForm(detailResult.Data);
        }
        finally
        {
            IsLoadingInitial = false;
        }
    }

    private void ApplyPrivilegeToForm(PrivilegeAdminItemDto item)
    {
        IdPrivilege = item.IdPrivilege;
        EffectiveIdSystem = item.IdSystem > 0 ? item.IdSystem : AdmPrivState.IdSystem;
        Status = string.IsNullOrWhiteSpace(item.Status) ? "A" : item.Status;
        Codename = item.Codename ?? string.Empty;
        Title = item.Title ?? string.Empty;
        GroupName = item.GroupName ?? string.Empty;
        ActionPath = item.Action ?? string.Empty;
        IdParent = item.IdParent >= 0 ? item.IdParent : 0;
        Icon = item.Icon ?? string.Empty;
        IsDeactivated = !string.Equals(Status, "A", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetCreateFormFields()
    {
        Codename = string.Empty;
        Title = string.Empty;
        GroupName = string.Empty;
        ActionPath = string.Empty;
        IdParent = 0;
        Icon = string.Empty;
        DelDescription = string.Empty;
        _fieldErrors.Clear();
    }

    protected async Task HandleSubmitAsync()
    {
        if (!CanSubmitForm || IsSaving || IsDeactivating || IsDeactivated || LoadFailed)
        {
            return;
        }

        if ((IsEditMode && !CanModify) || (!IsEditMode && !CanCreate))
        {
            return;
        }

        SuccessMessage = null;
        ErrorMessage = null;

        if (!ValidateForm())
        {
            ErrorMessage = "Por favor corrija los campos marcados antes de continuar.";
            return;
        }

        var trimmedCodename = Codename.Trim();
        var trimmedTitle = Title.Trim();
        var trimmedGroupName = GroupName.Trim();
        var trimmedAction = ActionPath.Trim();
        var trimmedIcon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();

        IsSaving = true;
        try
        {
            if (!IsEditMode)
            {
                var createRequest = new CreatePrivilegeRequestDto
                {
                    IdParent = IdParent,
                    Codename = trimmedCodename,
                    GroupName = trimmedGroupName,
                    Title = trimmedTitle,
                    Action = trimmedAction,
                    Icon = trimmedIcon
                };

                var result = await AdmPrivState.CreateAsync(createRequest);
                if (!result.Success || result.Data is null)
                {
                    if (result.StatusCode == (int)HttpStatusCode.Unauthorized)
                    {
                        await HandleUnauthorizedAsync();
                        return;
                    }

                    ErrorMessage = result.ErrorMessage ?? "No fue posible crear el privilegio.";
                    return;
                }

                EffectiveIdSystem = result.Data.IdSystem > 0 ? result.Data.IdSystem : AdmPrivState.IdSystem;
                SuccessMessage = $"Privilegio creado exitosamente (ID: {result.Data.IdPrivilege}).";
                ResetCreateFormFields();
            }
            else
            {
                var updateRequest = new UpdatePrivilegeRequestDto
                {
                    IdParent = IdParent,
                    Codename = trimmedCodename,
                    GroupName = trimmedGroupName,
                    Title = trimmedTitle,
                    Action = trimmedAction,
                    Icon = trimmedIcon
                };

                var result = await AdmPrivState.UpdateAsync(IdPrivilege, updateRequest);
                if (!result.Success || result.Data is null)
                {
                    if (result.StatusCode == (int)HttpStatusCode.Unauthorized)
                    {
                        await HandleUnauthorizedAsync();
                        return;
                    }

                    ErrorMessage = result.ErrorMessage ?? "No fue posible actualizar el privilegio.";
                    return;
                }

                Codename = trimmedCodename;
                Title = trimmedTitle;
                GroupName = trimmedGroupName;
                ActionPath = trimmedAction;
                Icon = trimmedIcon ?? string.Empty;
                SuccessMessage = "Los cambios del privilegio se han guardado exitosamente.";
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task HandleUnauthorizedAsync()
    {
        AdmPrivState.ClearSelectedEditPrivilegeId();
        await SecureStorage.ClearAsync();
        SessionState.ClearUser();
        NavigationManager.NavigateTo("/login", replace: true);
    }

    protected void RequestDeactivateConfirmation()
    {
        if (!CanDelete || !CanShowDeactivateSection || IsSaving || IsDeactivating)
        {
            return;
        }

        _fieldErrors.Remove("DelDescription");
        ErrorMessage = null;

        if (!string.IsNullOrEmpty(DelDescription) && DelDescription.Trim().Length > MaxDelDescriptionLength)
        {
            _fieldErrors["DelDescription"] = $"El motivo de inactivación no puede exceder los {MaxDelDescriptionLength} caracteres.";
            ErrorMessage = _fieldErrors["DelDescription"];
            return;
        }

        ShowDeactivateConfirm = true;
    }

    protected void CancelDeactivateConfirmation()
    {
        ShowDeactivateConfirm = false;
        ShowExitConfirm = false;
    }

    protected async Task HandleConfirmDeactivateAsync()
    {
        if (!CanDelete || !CanShowDeactivateSection || IsDeactivating || IdPrivilege <= 0)
        {
            return;
        }

        _fieldErrors.Remove("DelDescription");
        ErrorMessage = null;
        SuccessMessage = null;

        var trimmedReason = string.IsNullOrWhiteSpace(DelDescription) ? null : DelDescription.Trim();
        if (trimmedReason is { Length: > MaxDelDescriptionLength })
        {
            _fieldErrors["DelDescription"] = $"El motivo de inactivación no puede exceder los {MaxDelDescriptionLength} caracteres.";
            ErrorMessage = _fieldErrors["DelDescription"];
            ShowDeactivateConfirm = false;
        ShowExitConfirm = false;
            return;
        }

        IsDeactivating = true;
        try
        {
            var payload = trimmedReason is not null
                ? new DeactivatePrivilegeRequestDto { DelDescription = trimmedReason }
                : null;

            var result = await AdmPrivState.DeactivateAsync(IdPrivilege, payload);
            if (!result.Success || result.Data is null)
            {
                if (result.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    await HandleUnauthorizedAsync();
                    return;
                }

                ShowDeactivateConfirm = false;
        ShowExitConfirm = false;
                ErrorMessage = result.ErrorMessage ?? "No fue posible inactivar el privilegio.";
                return;
            }

            Status = string.IsNullOrWhiteSpace(result.Data.Status) ? "I" : result.Data.Status;
            IsDeactivated = true;
            ShowDeactivateConfirm = false;
        ShowExitConfirm = false;

            SuccessMessage = result.Data.CascadedRolePrivileges > 0
                ? $"Privilegio inactivado exitosamente (ID: {result.Data.IdPrivilege}). Se trasladaron {result.Data.CascadedRolePrivileges} asignaciones en roles al histórico."
                : $"Privilegio inactivado exitosamente (ID: {result.Data.IdPrivilege}).";
        }
        finally
        {
            IsDeactivating = false;
        }
    }

    protected void HandleExitRequest()
    {
        if (IsSaving || IsDeactivating)
        {
            return;
        }

        if (!RequiresExitConfirmation)
        {
            ConfirmExit();
            return;
        }

        ShowExitConfirm = true;
    }

    protected void ConfirmExit()
    {
        ShowExitConfirm = false;
        AdmPrivState.ClearSelectedEditPrivilegeId();
        NavigationManager.NavigateTo("/admsec/priv");
    }

    protected void CancelExitConfirmation()
    {
        ShowExitConfirm = false;
    }

    protected void ClearSuccessMessage() => SuccessMessage = null;

    protected void ClearErrorMessage() => ErrorMessage = null;

    protected bool HasFieldError(string fieldName) => _fieldErrors.ContainsKey(fieldName);

    protected string? GetFieldError(string fieldName) =>
        _fieldErrors.TryGetValue(fieldName, out var message) ? message : null;

    private bool ValidateForm()
    {
        _fieldErrors.Clear();

        if (string.IsNullOrWhiteSpace(Codename))
        {
            _fieldErrors["Codename"] = "El campo codename es obligatorio.";
        }
        else if (Codename.Trim().Length > MaxCodenameLength)
        {
            _fieldErrors["Codename"] = $"El campo codename no puede exceder los {MaxCodenameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            _fieldErrors["Title"] = "El campo title es obligatorio.";
        }
        else if (Title.Trim().Length > MaxTitleLength)
        {
            _fieldErrors["Title"] = $"El campo title no puede exceder los {MaxTitleLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(GroupName))
        {
            _fieldErrors["GroupName"] = "El campo group_name es obligatorio.";
        }
        else if (GroupName.Trim().Length > MaxGroupNameLength)
        {
            _fieldErrors["GroupName"] = $"El campo group_name no puede exceder los {MaxGroupNameLength} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(ActionPath))
        {
            _fieldErrors["Action"] = "El campo action es obligatorio.";
        }
        else if (ActionPath.Trim().Length > MaxActionLength)
        {
            _fieldErrors["Action"] = $"El campo action no puede exceder los {MaxActionLength} caracteres.";
        }

        if (IdParent < 0)
        {
            _fieldErrors["IdParent"] = "El privilegio padre debe ser mayor o igual a 0.";
        }
        else if (IsEditMode && IdParent == IdPrivilege)
        {
            _fieldErrors["IdParent"] = "Un privilegio no puede ser su propio padre jerárquico.";
        }

        if (!string.IsNullOrWhiteSpace(Icon) && Icon.Trim().Length > MaxIconLength)
        {
            _fieldErrors["Icon"] = $"El campo icon no puede exceder los {MaxIconLength} caracteres.";
        }

        return _fieldErrors.Count == 0;
    }

    private string GetNormalizedRelativePath()
    {
        var relative = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var queryIndex = relative.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            relative = relative[..queryIndex];
        }

        return relative.Trim('/');
    }

    public void Dispose()
    {
        AdmPrivState.ClearSelectedEditPrivilegeId();
    }
}
