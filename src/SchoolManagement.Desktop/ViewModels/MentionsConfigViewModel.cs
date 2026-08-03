using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Mentions.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Paramétrage des mentions honorifiques selon les pourcentages.</summary>
public partial class MentionsConfigViewModel : ViewModelBase
{
    private readonly IMentionsApiService _api;

    public MentionsConfigViewModel(IMentionsApiService api)
    {
        _api = api;
    }

    public ObservableCollection<ResultMentionDto> Mentions { get; } = [];

    [ObservableProperty] private ResultMentionDto? _selectedMention;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _minPercentageText = "55";
    [ObservableProperty] private string _maxPercentageText = "69";
    [ObservableProperty] private string _sortOrderText = "1";
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isEditing;

    public string FormTitle => IsEditing ? "Modifier la mention" : "Nouvelle mention";

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(FormTitle));

    partial void OnSelectedMentionChanged(ResultMentionDto? value)
    {
        if (value is null)
        {
            ResetForm();
            return;
        }

        IsEditing = true;
        Label = value.Label;
        MinPercentageText = value.MinPercentageInclusive.ToString("0.##", CultureInfo.CurrentCulture);
        MaxPercentageText = value.MaxPercentageInclusive.ToString("0.##", CultureInfo.CurrentCulture);
        SortOrderText = value.SortOrder.ToString(CultureInfo.InvariantCulture);
        IsActive = value.IsActive;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _api.GetAllAsync();
            Mentions.Clear();
            foreach (var item in list.OrderBy(m => m.SortOrder).ThenBy(m => m.MinPercentageInclusive))
            {
                Mentions.Add(item);
            }

            StatusMessage = $"{Mentions.Count} mention(s) configurée(s).";
            if (SelectedMention is not null)
            {
                SelectedMention = Mentions.FirstOrDefault(m => m.Id == SelectedMention.Id);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewMention()
    {
        SelectedMention = null;
        ResetForm();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryParseForm(out var min, out var max, out var order))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEditing && SelectedMention is not null)
            {
                await _api.UpdateAsync(SelectedMention.Id, new UpdateResultMentionRequest(
                    Label.Trim(), min, max, order, IsActive));
                StatusMessage = "Mention mise à jour.";
            }
            else
            {
                await _api.CreateAsync(new CreateResultMentionRequest(
                    Label.Trim(), min, max, order, IsActive));
                StatusMessage = "Mention créée.";
            }

            await LoadAsync();
            ResetForm();
            SelectedMention = null;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedMention is null)
        {
            StatusMessage = "Sélectionnez une mention à supprimer.";
            return;
        }

        if (MessageBox.Show(
                $"Supprimer la mention « {SelectedMention.Label} » ?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _api.DeleteAsync(SelectedMention.Id);
            StatusMessage = "Mention supprimée.";
            SelectedMention = null;
            await LoadAsync();
            ResetForm();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryParseForm(out decimal min, out decimal max, out int order)
    {
        min = 0;
        max = 0;
        order = 0;

        if (string.IsNullOrWhiteSpace(Label))
        {
            StatusMessage = "Le libellé est obligatoire.";
            return false;
        }

        if (!decimal.TryParse(MinPercentageText, NumberStyles.Number, CultureInfo.CurrentCulture, out min)
            || !decimal.TryParse(MaxPercentageText, NumberStyles.Number, CultureInfo.CurrentCulture, out max))
        {
            StatusMessage = "Indiquez des pourcentages valides (ex. 55 et 69).";
            return false;
        }

        if (!int.TryParse(SortOrderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out order))
        {
            StatusMessage = "L'ordre d'affichage doit être un nombre entier.";
            return false;
        }

        return true;
    }

    private void ResetForm()
    {
        IsEditing = false;
        Label = string.Empty;
        MinPercentageText = "55";
        MaxPercentageText = "69";
        SortOrderText = (Mentions.Count + 1).ToString(CultureInfo.InvariantCulture);
        IsActive = true;
    }
}
