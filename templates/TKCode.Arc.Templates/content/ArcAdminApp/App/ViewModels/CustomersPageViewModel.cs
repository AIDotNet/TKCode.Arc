using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArcStarterApp.Models;
using ArcStarterApp.Services;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The customer table: the search and the status filter narrow <see cref="Rows"/>, which shows one page of
/// <see cref="PageSize"/> at a time; <see cref="Selected"/> is the customer the detail drawer shows.
/// </summary>
public sealed partial class CustomersPageViewModel : ObservableObject
{
    /// <summary>The status filter's "every status" value.</summary>
    public const string AnyStatus = "all";

    public const int PageSize = 12;

    private readonly ICustomerService _service;
    private IReadOnlyList<Customer> _all = [];
    private List<Customer> _matches = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _status = AnyStatus;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(SelectedName), nameof(SelectedCompany))]
    private Customer? _selected;

    public CustomersPageViewModel(ICustomerService service)
    {
        _service = service;
    }

    /// <summary>The customers on the current page.</summary>
    public ObservableCollection<Customer> Rows { get; } = new();

    /// <summary>The drawer's title and description (empty while no customer is selected).</summary>
    public string SelectedName => Selected?.Name ?? string.Empty;

    public string SelectedCompany => Selected?.Company ?? string.Empty;

    /// <summary>Whether the detail drawer is open (closing it clears <see cref="Selected"/>).</summary>
    public bool IsDetailOpen
    {
        get => Selected is not null;
        set
        {
            if (!value)
            {
                Selected = null;
            }
        }
    }

    public async Task LoadAsync()
    {
        _all = await _service.GetCustomersAsync(CancellationToken.None);
        IsLoading = false;
        Refresh();
    }

    [RelayCommand]
    private void Open(Customer customer) => Selected = customer;

    [RelayCommand]
    private void ClearFilters()
    {
        Search = string.Empty;
        Status = AnyStatus;
    }

    partial void OnSearchChanged(string value) => Refresh();

    partial void OnStatusChanged(string value) => Refresh();

    partial void OnPageChanged(int value) => ShowPage();

    private void Refresh()
    {
        var query = Search.Trim();
        _matches = _all.Where(c =>
                (Status == AnyStatus || c.Status.ToString() == Status) &&
                (query.Length == 0 ||
                 c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 c.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 c.Company.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        PageCount = Math.Max(1, (_matches.Count + PageSize - 1) / PageSize);
        if (Page != 1)
        {
            Page = 1; // shows the page through OnPageChanged
        }
        else
        {
            ShowPage();
        }
        Summary = _matches.Count == _all.Count ? $"{_all.Count} customers" : $"{_matches.Count} of {_all.Count} customers";
    }

    private void ShowPage()
    {
        Rows.Clear();
        foreach (var customer in _matches.Skip((Page - 1) * PageSize).Take(PageSize))
        {
            Rows.Add(customer);
        }
    }
}
