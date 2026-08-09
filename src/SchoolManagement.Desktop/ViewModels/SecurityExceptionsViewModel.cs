using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SecurityExceptionsViewModel : ViewModelBase
{
    private readonly ISecurityAdminApiService _security;

    public SecurityExceptionsViewModel(ISecurityAdminApiService security)
    {
        _security = security;
        NewValidFrom = DateTime.Today;
        _ = InitializeAsync();
    }

    public ObservableCollection<SecurityExceptionDto> Exceptions { get; } = [];
    public ObservableCollection<SecurityUserDto> Users { get; } = [];
    public ObservableCollection<PermissionCatalogItemDto> Permissions { get; } = [];

    public Array EffectChoices { get; } = Enum.GetValues(typeof(PermissionExceptionEffect));

    [ObservableProperty] private SecurityExceptionDto? _selectedException;
    [ObservableProperty] private Guid? _filterUserId;
    [ObservableProperty] private Guid _newUserId;
    [ObservableProperty] private Guid _newPermissionId;
    [ObservableProperty] private PermissionExceptionEffect _newEffect = PermissionExceptionEffect.Grant;
    [ObservableProperty] private DateTime _newValidFrom;
    [ObservableProperty] private DateTime? _newValidTo;
    [ObservableProperty] private string? _newReason;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var users = await _security.GetUsersAsync();
            Users.Clear();
            foreach (var u in users) Users.Add(u);
            if (Users.Count > 0) NewUserId = Users[0].Id;

            var catalog = await _security.GetPermissionCatalogAsync();
            Permissions.Clear();
            foreach (var p in catalog.Where(c => c.IsActive).OrderBy(c => c.DisplayName))
                Permissions.Add(p);
            if (Permissions.Count > 0) NewPermissionId = Permissions[0].Id;

            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _security.GetExceptionsAsync(FilterUserId);
            Exceptions.Clear();
            foreach (var e in list.OrderByDescending(x => x.ValidFrom))
                Exceptions.Add(e);
            StatusMessage = null;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (NewUserId == Guid.Empty || NewPermissionId == Guid.Empty)
        {
            StatusMessage = "Utilisateur et permission requis.";
            return;
        }

        IsBusy = true;
        try
        {
            await _security.CreateExceptionAsync(new CreateSecurityExceptionRequest(
                NewUserId,
                NewPermissionId,
                NewEffect,
                NewValidFrom,
                NewValidTo,
                NewReason));
            NewReason = null;
            NewValidTo = null;
            StatusMessage = "Exception créée.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (SelectedException is null) return;
        IsBusy = true;
        try
        {
            await _security.CloseExceptionAsync(SelectedException.Id);
            StatusMessage = "Exception clôturée.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
