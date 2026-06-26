using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using MidgardStudio.App.Common;
using MidgardStudio.Grf;

namespace MidgardStudio.App.Services;

/// <summary>Resolves client asset images (item icon / illustration / sprite animation) from the layered GRFs.</summary>
public sealed class GrfImageService
{
    private readonly GrfService _grf;

    // Decoding a GRF image (decompress + bitmap build) is expensive and was repeated on every selection /
    // resource-name commit. Cache the frozen ImageSource keyed by GRF path, bounded so it can't grow without
    // limit, and drop it whenever the configured sources change. Frozen images are safe to share/cross-thread.
    private const int CacheCap = 2048;
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _order = new();
    private readonly object _lock = new();

    public GrfImageService(GrfService grf)
    {
        _grf = grf;
        _grf.SourcesChanged += ClearCache;
    }

    private void ClearCache()
    {
        lock (_lock) { _cache.Clear(); _order.Clear(); }
    }

    private ImageSource? Cached(string path, Func<ImageSource?> decode)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var hit)) return hit;
            var img = decode();
            _cache[path] = img;
            _order.Enqueue(path);
            while (_order.Count > CacheCap)
                _cache.Remove(_order.Dequeue());
            return img;
        }
    }

    public ImageSource? ItemIcon(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName)) return null;
        var path = GrfAssetPaths.ItemIcon(resourceName!);
        return Cached(path, () => GrfImaging.ToImageSource(_grf.GetImage(path)));
    }

    public ImageSource? ItemCollection(string? resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName)) return null;
        var path = GrfAssetPaths.ItemCollection(resourceName!);
        return Cached(path, () => GrfImaging.ToImageSource(_grf.GetImage(path)));
    }

    public ImageSource? MonsterSprite(string? spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        var path = GrfAssetPaths.MonsterSprite(spriteName!);
        return Cached(path, () => GrfImaging.ToImageSource(_grf.GetImage(path)));
    }

    /// <summary>Decodes the monster's .spr/.act from the GRF into an animatable sequence of frames.</summary>
    public SpriteAnimation? MonsterAnimation(string? spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName)) return null;
        string sprPath = GrfAssetPaths.MonsterSprite(spriteName!);
        byte[]? spr = _grf.GetData(sprPath);
        if (spr is null) return null;
        byte[]? act = _grf.GetData(Path.ChangeExtension(sprPath, ".act"));
        return SpriteRenderer.Build(spr, act);
    }
}
