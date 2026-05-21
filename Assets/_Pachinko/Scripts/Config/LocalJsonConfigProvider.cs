using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

public class LocalJsonConfigProvider : IConfigProvider
{
    private static readonly Regex SafeId =
        new Regex(@"^[a-zA-Z0-9_\-]{1,64}$", RegexOptions.Compiled);

    private readonly Dictionary<string, BoardConfig> _cache = new();
    private readonly string _boardsRoot;

    public LocalJsonConfigProvider()
    {
        _boardsRoot = Path.GetFullPath(
            Path.Combine(Application.streamingAssetsPath, "boards"));
    }

    public BoardConfig GetBoard(string boardId)
    {
        if (_cache.TryGetValue(boardId, out var cached)) return cached;

        if (!SafeId.IsMatch(boardId))
            throw new ArgumentException($"boardId '{boardId}' contains illegal characters.");

        string path     = Path.Combine(_boardsRoot, boardId + ".json");
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(_boardsRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected for boardId '{boardId}'.");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Board config not found: {fullPath}");

        string raw = File.ReadAllText(fullPath);
        var cfg = new BoardConfig();
        JsonConvert.PopulateObject(raw, cfg,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore });

        string error = cfg.Validate();
        if (error != null)
            throw new InvalidOperationException($"Invalid board config '{boardId}': {error}");

        _cache[boardId] = cfg;
        return cfg;
    }
}
