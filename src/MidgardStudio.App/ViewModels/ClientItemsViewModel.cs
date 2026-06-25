using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MidgardStudio.App.Services;
using MidgardStudio.Core.Commands;
using MidgardStudio.Core.Lua;
using MidgardStudio.Core.Model;
using MidgardStudio.Core.Overlay;
using MidgardStudio.Core.Schema;

namespace MidgardStudio.App.ViewModels;

/// <summary>
/// The client-side item editor (separate from the Server Items section): the same item list, but the
/// detail panel edits the client itemInfo (display/resource/description names, slots, ClassNum, costume,
/// headgear sprite + icon) read from itemInfo.lua / itemInfo_C.lua.
/// </summary>
public sealed partial class ClientItemsViewModel : ObservableObject
{
    private readonly WorkspaceSession _session;
    private readonly ClientItemService _clientItems;
    private readonly GrfImageService _images;
    private readonly SpriteLinkService _sprite;
    private readonly AppSettingsService _settings;
    private readonly DbSchema _itemSchema;
    private OverlayTable? _overlay;

    public ClientItemsViewModel(WorkspaceSession session, ClientItemService clientItems, GrfImageService images,
        SpriteLinkService sprite, AppSettingsService settings, DbSchema itemSchema)
    {
        _session = session;
        _clientItems = clientItems;
        _images = images;
        _sprite = sprite;
        _settings = settings;
        _itemSchema = itemSchema;
    }

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private DbListViewModel? _list;

    [ObservableProperty]
    private ClientTextViewModel? _editor;

    public bool HasEditor => Editor is not null;

    partial void OnEditorChanged(ClientTextViewModel? value) => OnPropertyChanged(nameof(HasEditor));

    /// <summary>Autocompletes the selected item(s): bulk (with confirm + summary) when several rows are
    /// selected, otherwise the single open editor.</summary>
    [RelayCommand]
    private void AutocompleteSelected()
    {
        if (List?.SelectedRows is { Count: > 1 } rows) BulkAutocomplete(rows.ToList());
        else Editor?.AutocompleteCommand.Execute(null);
    }

    /// <summary>Copies the selected item's client itemInfo Lua block to the clipboard.</summary>
    [RelayCommand]
    private void CopyClientLua() => Editor?.CopyLuaCommand.Execute(null);

    /// <summary>Regenerates client text for every selected row in one undo step.</summary>
    private void BulkAutocomplete(IReadOnlyList<RecordRowViewModel> rows)
    {
        if (rows.Count == 0) return;
        var cfg = _settings.Settings.Autocomplete;
        if (!Views.ConfirmDialog.Show("Autocomplete selected",
                $"Autocomplete {rows.Count} selected item(s)? The property lines are regenerated from server data; any lore text at the top is kept.",
                yes: "Autocomplete")) return;

        var gen = new ItemAutocomplete(cfg, Resolver());
        var ops = new List<(ItemInfoEntry Old, ItemInfoEntry New)>();
        foreach (var row in rows)
        {
            int id = (int)row.Key.AsInt;
            var oldClone = _clientItems.GetOrCreate(id).Clone();
            var updated = oldClone.Clone();

            if (cfg.OverwriteExisting || string.IsNullOrWhiteSpace(updated.IdentifiedDisplayName))
                updated.IdentifiedDisplayName = gen.DisplayName(row.Record);
            if (cfg.OverwriteExisting || updated.IdentifiedDescription.Count == 0)
                updated.IdentifiedDescription = gen.BuildDescription(row.Record, oldClone.IdentifiedDescription);
            updated.SlotCount = row.Record.GetInt("Slots");
            updated.ClassNum = row.Record.GetInt("View");
            if (cfg.DefaultUnidentifiedDescription.Length > 0 && (cfg.OverwriteExisting || updated.UnidentifiedDescription.Count == 0))
                updated.UnidentifiedDescription = cfg.DefaultUnidentifiedDescription.Replace("\r\n", "\n").Split('\n').ToList();

            ops.Add((oldClone, updated));
        }

        _session.Commands.Execute(new ListMutateCommand($"Autocomplete {rows.Count} items",
            () => { foreach (var op in ops) _clientItems.Upsert(op.New.Clone()); RefreshEditor(); },
            () => { foreach (var op in ops) _clientItems.Upsert(op.Old.Clone()); RefreshEditor(); }));

        Views.ConfirmDialog.Show("Autocomplete complete", $"Autocompleted {rows.Count} item(s).", yes: "OK");
    }

    /// <summary>Rebuilds the open editor for the current row (keeps it in sync after bulk / undo).</summary>
    private void RefreshEditor()
    {
        if (List?.SelectedRow is { } row)
            Editor = new ClientTextViewModel(row.Record, _clientItems, _images, _session.Commands, _settings, Resolver(), _sprite);
    }

    private bool _skillResolved;
    private Func<string, string?>? _skill;

    /// <summary>The skill AegisName → display-name resolver (from <see cref="SkillLookupService"/>), or null.</summary>
    private Func<string, string?>? Resolver()
    {
        if (_skillResolved) return _skill;
        _skillResolved = true;
        try { if (App.Services.GetService<SkillLookupService>() is { } s) _skill = s.Display; } catch { /* host not ready */ }
        return _skill;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_overlay is not null) return;

        IsLoading = true;
        var modeSet = await Task.Run(() => _session.GetModeSet(_itemSchema));
        _overlay = modeSet.For(_session.Mode);

        var list = new DbListViewModel(_overlay,
            key => _images.ItemIcon(_clientItems.GetOrCreate((int)key.AsInt).IdentifiedResourceName));
        list.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DbListViewModel.SelectedRow))
                Editor = list.SelectedRow is { } row
                    ? new ClientTextViewModel(row.Record, _clientItems, _images, _session.Commands, _settings, Resolver(), _sprite)
                    : null;
        };

        List = list;
        IsLoading = false;
        list.SelectedRow = list.Rows.FirstOrDefault();
        if (_pendingSelect is { } sel) { list.SelectByKey(sel); _pendingSelect = null; }
    }

    private RecordKey? _pendingSelect;

    public void SelectRow(RecordKey key)
    {
        if (List is not null) List.SelectByKey(key);
        else _pendingSelect = key; // applied once loaded (navigation from Items)
    }
}
