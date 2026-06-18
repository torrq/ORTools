using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ORTools.UI.ViewModels;

public partial class AutobuffSearchViewModel : ObservableObject
{
    private readonly AutobuffSkillViewModel _skillVm;
    private readonly AutobuffItemViewModel _itemVm;

    [ObservableProperty]
    private string _searchText = "";

    public ObservableCollection<object> ActiveSlots { get; } = new();
    public ObservableCollection<object> SearchResults { get; } = new();

    public bool HasActiveSlots => ActiveSlots.Any();
    public bool HasSearchResults => SearchResults.Any();

    public AutobuffSearchViewModel(AutobuffSkillViewModel skillVm, AutobuffItemViewModel itemVm)
    {
        _skillVm = skillVm;
        _itemVm = itemVm;

        // Listen for group changes from the worker parsing new states
        _skillVm.ConfigUpdated += RefreshSearch;
        _itemVm.ConfigUpdated += RefreshSearch;
        
        RefreshSearch();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshSearch();
    }

    private void RefreshSearch()
    {
        ActiveSlots.Clear();
        SearchResults.Clear();
        var val = SearchText;
        bool isSearching = !string.IsNullOrWhiteSpace(val);
        var lowerSearch = isSearching ? val.ToLowerInvariant() : "";

        var allSkills = _skillVm.SkillGroups.SelectMany(g => g.Items).ToList();
        var allItems = _itemVm.ItemGroups.SelectMany(g => g.Items).ToList();

        var activeSkills = allSkills.Where(i => i.Key != "None").OrderBy(i => i.DisplayName).ToList();
        var activeItems = allItems.Where(i => i.Key != "None").OrderBy(i => i.DisplayName).ToList();

        foreach (var skill in activeSkills) ActiveSlots.Add(skill);
        foreach (var item in activeItems) ActiveSlots.Add(item);

        if (isSearching)
        {
            var matchingSkills = allSkills.Where(i => i.Key == "None" && (i.DisplayName.ToLowerInvariant().Contains(lowerSearch) || i.Name.ToLowerInvariant().Contains(lowerSearch))).OrderBy(i => i.DisplayName).ToList();
            var matchingItems = allItems.Where(i => i.Key == "None" && (i.DisplayName.ToLowerInvariant().Contains(lowerSearch) || i.Name.ToLowerInvariant().Contains(lowerSearch))).OrderBy(i => i.DisplayName).ToList();
            
            foreach (var skill in matchingSkills) SearchResults.Add(skill);
            foreach (var item in matchingItems) SearchResults.Add(item);
        }

        OnPropertyChanged(nameof(HasActiveSlots));
        OnPropertyChanged(nameof(HasSearchResults));
    }
}
