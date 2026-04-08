// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text.Json;

namespace PokedexTray;

public class PokemonService : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<int, PokemonInfo> _cache = new();

    public bool WasCacheHit { get; private set; }

    public PokemonService()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://pokeapi.co/api/v2/")
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PokedexTray/1.0 (Hermes Sample)");
    }

    public async Task<PokemonInfo?> LookupAsync(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            WasCacheHit = true;
            return cached;
        }

        WasCacheHit = false;

        try
        {
            using var response = await _http.GetAsync($"pokemon/{id}");
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString() ?? "unknown";

            var spriteUrl = "";
            if (root.TryGetProperty("sprites", out var sprites) &&
                sprites.TryGetProperty("front_default", out var frontDefault) &&
                frontDefault.ValueKind == JsonValueKind.String)
            {
                spriteUrl = frontDefault.GetString() ?? "";
            }

            var types = root.GetProperty("types")
                .EnumerateArray()
                .Select(t => t.GetProperty("type").GetProperty("name").GetString() ?? "")
                .Where(t => t.Length > 0)
                .ToArray();

            var info = new PokemonInfo(name, spriteUrl, types);
            _cache[id] = info;
            return info;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public void ClearCache() => _cache.Clear();

    public void Dispose() => _http.Dispose();
}
