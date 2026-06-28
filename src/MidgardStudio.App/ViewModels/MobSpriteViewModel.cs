using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidgardStudio.App.Common;
using MidgardStudio.App.Services;
using MidgardStudio.Core.Commands;
using MidgardStudio.Core.Model;

namespace MidgardStudio.App.ViewModels;

/// <summary>
/// Client sprite registration for the selected monster: writes the npcidentity.lub + jobname.lub
/// mapping (JT_&lt;NAME&gt; = mob id, sprite name) and previews the sprite from the GRF.
/// </summary>
public sealed partial class MobSpriteViewModel : ObservableObject
{
    private readonly DbRecord _server;
    private readonly MobSpriteService _service;
    private readonly GrfImageService _images;
    private readonly EditCommandStack _stack;

    public MobSpriteViewModel(DbRecord server, MobSpriteService service, GrfImageService images, EditCommandStack stack)
    {
        _server = server;
        _service = service;
        _images = images;
        _stack = stack;

        _spriteName = server.GetString("AegisName") ?? string.Empty; // sprite name defaults to AegisName
        RegisteredConstant = service.IsAvailable ? service.FindConstantForMob(server.GetInt("Id")) : null;
        RefreshPreview();
    }

    public bool CanRegister => _service.IsAvailable;

    public string? RegisteredConstant { get; }

    [ObservableProperty]
    private string _spriteName;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private ImageSource? _spritePreview;

    /// <summary>Decoded, animatable sprite frames for the current sprite name (null when not in the GRF).</summary>
    [ObservableProperty]
    private SpriteAnimation? _animation;

    public bool HasAnimation => Animation is not null;

    partial void OnAnimationChanged(SpriteAnimation? value) => OnPropertyChanged(nameof(HasAnimation));

    [RelayCommand]
    private void Register()
    {
        if (!_service.IsAvailable || string.IsNullOrWhiteSpace(SpriteName)) return;
        try
        {
            int mobId = _server.GetInt("Id");
            string sprite = SpriteName.Trim();
            if (_service.HasPending(mobId, sprite))
            {
                StatusMessage = $"'{sprite}' is already queued for this monster (pending save).";
                return;
            }
            string aegis = _server.GetString("AegisName") ?? ("mob" + mobId);
            var planned = _service.PlanMob(mobId, aegis, sprite);
            _stack.Execute(new ListMutateCommand("Register mob sprite: " + planned.ConstantName,
                () => _service.AddPending(planned),
                () => _service.RemovePending(planned)));
            StatusMessage = $"Queued {planned.ConstantName} = {_server.GetInt("Id")} → sprite '{planned.Sprite}'. Written to npcidentity.lub & jobname.lub on Save.";
            RefreshPreview();
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed: " + ex.Message;
        }
    }

    partial void OnSpriteNameChanged(string value) => RefreshPreview();

    private void RefreshPreview()
    {
        Animation = _images.MonsterAnimation(SpriteName);
        SpritePreview = Animation is null ? _images.MonsterSprite(SpriteName) : null;
    }
}
