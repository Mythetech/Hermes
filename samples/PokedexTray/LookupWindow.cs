// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text.Json;
using Hermes;
using Hermes.Storage;

namespace PokedexTray;

public static class LookupWindow
{
    // Keys in the default Hermes KV store. Persistence survives the tray
    // window being torn down between sessions, which is the localStorage
    // gap this sample is meant to demonstrate.
    private const string LastViewedKey = "lastViewedId";
    private const string FavoritesKey = "favorites";

    public static HermesWindow Create(PokemonService service, Action? onHidden = null)
    {
        HermesWindow? window = null;
        window = new HermesWindow()
            .SetTitle("PokeDex Lookup")
            .SetSize(360, 500)
            .Center()
            .SetChromeless(true)
            .SetTopMost(true)
            .SetResizable(false)
            .SetDevToolsEnabled(true)
            .LoadHtml(Html)
            .OnWebMessage(msg =>
            {
                _ = HandleMessageAsync(window!, service, msg, onHidden);
            });

        return window;
    }

    private static async Task HandleMessageAsync(HermesWindow window, PokemonService service, string msg, Action? onHidden)
    {
        try
        {
            using var doc = JsonDocument.Parse(msg);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var action))
                return;

            switch (action.GetString())
            {
                case "hide":
                    window.Invoke(() =>
                    {
                        window.Hide();
                        onHidden?.Invoke();
                    });
                    break;

                case "init":
                    {
                        // Page just loaded; replay persisted state from the KV store.
                        var lastViewed = HermesStore.Default.Get<int?>(LastViewedKey);
                        var favorites = HermesStore.Default.Get<List<Favorite>>(FavoritesKey)
                                        ?? new List<Favorite>();

                        var initResponse = JsonSerializer.Serialize(new
                        {
                            type = "init",
                            lastViewedId = lastViewed,
                            favorites = favorites.Select(f => new { id = f.Id, name = f.Name }).ToArray()
                        });
                        window.Invoke(() => window.SendMessage(initResponse));
                        break;
                    }

                case "lookup":
                    {
                        var id = root.GetProperty("id").GetInt32();
                        var info = await service.LookupAsync(id);

                        string response;
                        if (info is not null)
                        {
                            // Persist last-viewed so the next launch reopens to the same Pokémon.
                            HermesStore.Default.Set(LastViewedKey, id);

                            response = JsonSerializer.Serialize(new
                            {
                                type = "lookup",
                                id,
                                name = info.Name,
                                sprite = info.SpriteUrl,
                                types = info.Types,
                                cached = service.WasCacheHit
                            });
                        }
                        else
                        {
                            response = JsonSerializer.Serialize(new { type = "lookup", error = $"Pokemon #{id} not found" });
                        }

                        window.Invoke(() => window.SendMessage(response));
                        break;
                    }

                case "toggleFavorite":
                    {
                        var id = root.GetProperty("id").GetInt32();
                        var name = root.GetProperty("name").GetString() ?? "";

                        var favorites = HermesStore.Default.Get<List<Favorite>>(FavoritesKey)
                                        ?? new List<Favorite>();

                        var existingIndex = favorites.FindIndex(f => f.Id == id);
                        if (existingIndex >= 0)
                        {
                            favorites.RemoveAt(existingIndex);
                        }
                        else
                        {
                            favorites.Add(new Favorite(id, name));
                        }

                        HermesStore.Default.Set(FavoritesKey, favorites);

                        var favResponse = JsonSerializer.Serialize(new
                        {
                            type = "favorites",
                            favorites = favorites.Select(f => new { id = f.Id, name = f.Name }).ToArray()
                        });
                        window.Invoke(() => window.SendMessage(favResponse));
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { type = "lookup", error = ex.Message });
            window.Invoke(() => window.SendMessage(error));
        }
    }

    private const string Html = """
    <!DOCTYPE html>
    <html lang="en">
    <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>PokeDex Lookup</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #1a1a2e;
            color: #e0e0e0;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 0 20px 24px 20px;
            min-height: 100vh;
            user-select: none;
            border-radius: 10px;
            overflow: hidden;
        }
        .titlebar {
            width: 100%;
            display: flex;
            justify-content: flex-end;
            padding: 8px 4px 4px 4px;
            margin-bottom: 8px;
        }
        .close-btn {
            width: 24px;
            height: 24px;
            border: none;
            border-radius: 50%;
            background: rgba(255,255,255,0.1);
            color: #888;
            font-size: 14px;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: background 0.2s, color 0.2s;
            padding: 0;
        }
        .close-btn:hover { background: rgba(255,255,255,0.2); color: #e0e0e0; }
        h1 {
            font-size: 28px;
            font-weight: 700;
            margin-bottom: 20px;
            color: #ff6b6b;
            letter-spacing: 1px;
        }
        .search-row {
            display: flex;
            gap: 8px;
            width: 100%;
            max-width: 300px;
            margin-bottom: 24px;
        }
        input {
            flex: 1;
            padding: 10px 14px;
            border: 2px solid #333;
            border-radius: 8px;
            background: #16213e;
            color: #e0e0e0;
            font-size: 16px;
            outline: none;
            transition: border-color 0.2s;
        }
        input:focus { border-color: #ff6b6b; }
        input::placeholder { color: #666; }
        button {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            background: #e74c3c;
            color: white;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: background 0.2s;
        }
        button:hover { background: #c0392b; }
        button:active { background: #a93226; }
        button:disabled { background: #555; cursor: not-allowed; }
        .result {
            display: none;
            flex-direction: column;
            align-items: center;
            background: #16213e;
            border-radius: 12px;
            padding: 24px;
            width: 100%;
            max-width: 300px;
        }
        .result.visible { display: flex; }
        .sprite {
            width: 120px;
            height: 120px;
            image-rendering: pixelated;
            margin-bottom: 12px;
        }
        .name {
            font-size: 24px;
            font-weight: 700;
            text-transform: capitalize;
            margin-bottom: 12px;
        }
        .types {
            display: flex;
            gap: 8px;
            margin-bottom: 16px;
        }
        .type-badge {
            padding: 4px 14px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: 600;
            text-transform: capitalize;
            color: white;
        }
        .name-row {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-bottom: 12px;
        }
        .star-btn {
            background: none;
            border: none;
            color: #555;
            font-size: 22px;
            cursor: pointer;
            padding: 0 4px;
            line-height: 1;
            transition: color 0.2s, transform 0.1s;
        }
        .star-btn:hover { color: #f1c40f; transform: scale(1.15); }
        .star-btn.active { color: #f1c40f; }
        .status {
            font-size: 12px;
            color: #888;
            margin-top: 4px;
        }
        .status.cached { color: #2ecc71; }
        .status.fetched { color: #3498db; }
        .favorites-bar {
            width: 100%;
            max-width: 300px;
            margin-top: 16px;
            display: none;
            flex-direction: column;
            gap: 6px;
        }
        .favorites-bar.visible { display: flex; }
        .favorites-label {
            font-size: 11px;
            color: #666;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }
        .favorites-list {
            display: flex;
            flex-wrap: wrap;
            gap: 6px;
        }
        .fav-chip {
            padding: 4px 10px;
            border-radius: 12px;
            background: rgba(241, 196, 15, 0.15);
            border: 1px solid rgba(241, 196, 15, 0.4);
            color: #f1c40f;
            font-size: 12px;
            font-weight: 600;
            text-transform: capitalize;
            cursor: pointer;
            transition: background 0.2s;
        }
        .fav-chip:hover { background: rgba(241, 196, 15, 0.3); }
        .error {
            color: #e74c3c;
            font-size: 14px;
            margin-top: 12px;
            text-align: center;
        }
        .loading {
            display: none;
            color: #888;
            font-size: 14px;
            margin-top: 12px;
        }
        .loading.visible { display: block; }

        /* Pokemon type colors */
        .type-normal { background: #a8a878; }
        .type-fire { background: #f08030; }
        .type-water { background: #6890f0; }
        .type-electric { background: #f8d030; color: #333; }
        .type-grass { background: #78c850; }
        .type-ice { background: #98d8d8; color: #333; }
        .type-fighting { background: #c03028; }
        .type-poison { background: #a040a0; }
        .type-ground { background: #e0c068; color: #333; }
        .type-flying { background: #a890f0; }
        .type-psychic { background: #f85888; }
        .type-bug { background: #a8b820; }
        .type-rock { background: #b8a038; }
        .type-ghost { background: #705898; }
        .type-dragon { background: #7038f8; }
        .type-dark { background: #705848; }
        .type-steel { background: #b8b8d0; color: #333; }
        .type-fairy { background: #ee99ac; color: #333; }
    </style>
    </head>
    <body>
        <div class="titlebar">
            <button class="close-btn" onclick="window.external.sendMessage(JSON.stringify({action:'hide'}))" title="Close">&#x2715;</button>
        </div>
        <h1>PokeDex</h1>
        <div class="search-row">
            <input type="number" id="pokemonId" placeholder="Enter ID (1-1025)" min="1" max="1025" />
            <button id="searchBtn" onclick="doSearch()">Search</button>
        </div>
        <div class="loading" id="loading">Searching...</div>
        <div class="error" id="error"></div>
        <div class="result" id="result">
            <img class="sprite" id="sprite" alt="Pokemon sprite" />
            <div class="name-row">
                <div class="name" id="pokeName"></div>
                <button class="star-btn" id="starBtn" title="Toggle favorite">&#x2606;</button>
            </div>
            <div class="types" id="types"></div>
            <div class="status" id="status"></div>
        </div>
        <div class="favorites-bar" id="favoritesBar">
            <div class="favorites-label">Favorites (persisted)</div>
            <div class="favorites-list" id="favoritesList"></div>
        </div>
        <script>
            const idInput = document.getElementById('pokemonId');
            const searchBtn = document.getElementById('searchBtn');
            const loading = document.getElementById('loading');
            const errorEl = document.getElementById('error');
            const result = document.getElementById('result');
            const starBtn = document.getElementById('starBtn');
            const favoritesBar = document.getElementById('favoritesBar');
            const favoritesList = document.getElementById('favoritesList');

            // Tracks the currently displayed Pokémon so the star button knows
            // which id to toggle.
            let currentPokemon = null;
            // Local mirror of the persisted favorites list, kept in sync with
            // every server response so the star indicator stays accurate.
            let favorites = [];

            idInput.addEventListener('keydown', e => {
                if (e.key === 'Enter') doSearch();
            });

            starBtn.addEventListener('click', () => {
                if (!currentPokemon) return;
                window.external.sendMessage(JSON.stringify({
                    action: 'toggleFavorite',
                    id: currentPokemon.id,
                    name: currentPokemon.name
                }));
            });

            function doSearch() {
                const id = parseInt(idInput.value, 10);
                if (isNaN(id) || id < 1) {
                    showError('Please enter a valid Pokemon ID');
                    return;
                }
                searchBtn.disabled = true;
                loading.classList.add('visible');
                errorEl.textContent = '';
                result.classList.remove('visible');
                window.external.sendMessage(JSON.stringify({ action: 'lookup', id: id }));
            }

            function isFavorited(id) {
                return favorites.some(f => f.id === id);
            }

            function renderStar() {
                if (currentPokemon && isFavorited(currentPokemon.id)) {
                    starBtn.classList.add('active');
                    starBtn.innerHTML = '&#x2605;'; // filled
                } else {
                    starBtn.classList.remove('active');
                    starBtn.innerHTML = '&#x2606;'; // outline
                }
            }

            function renderFavorites() {
                favoritesList.innerHTML = '';
                if (favorites.length === 0) {
                    favoritesBar.classList.remove('visible');
                    return;
                }
                favoritesBar.classList.add('visible');
                favorites.forEach(f => {
                    const chip = document.createElement('span');
                    chip.className = 'fav-chip';
                    chip.textContent = '#' + f.id + ' ' + f.name;
                    chip.addEventListener('click', () => {
                        idInput.value = f.id;
                        doSearch();
                    });
                    favoritesList.appendChild(chip);
                });
            }

            function renderLookup(data) {
                document.getElementById('sprite').src = data.sprite;
                document.getElementById('pokeName').textContent = data.name;

                const typesEl = document.getElementById('types');
                typesEl.innerHTML = '';
                data.types.forEach(t => {
                    const badge = document.createElement('span');
                    badge.className = 'type-badge type-' + t;
                    badge.textContent = t;
                    typesEl.appendChild(badge);
                });

                const statusEl = document.getElementById('status');
                if (data.cached) {
                    statusEl.textContent = 'Cached';
                    statusEl.className = 'status cached';
                } else {
                    statusEl.textContent = 'Fetched from API';
                    statusEl.className = 'status fetched';
                }
                currentPokemon = { id: data.id, name: data.name };
                renderStar();
                result.classList.add('visible');
            }

            window.external.receiveMessage(function(msg) {
                const data = JSON.parse(msg);

                if (data.type === 'init') {
                    favorites = data.favorites || [];
                    renderFavorites();
                    if (data.lastViewedId) {
                        idInput.value = data.lastViewedId;
                        doSearch();
                    }
                    return;
                }

                if (data.type === 'favorites') {
                    favorites = data.favorites || [];
                    renderFavorites();
                    renderStar();
                    return;
                }

                // Default: lookup response
                searchBtn.disabled = false;
                loading.classList.remove('visible');
                if (data.error) {
                    showError(data.error);
                    return;
                }
                renderLookup(data);
            });

            function showError(msg) {
                errorEl.textContent = msg;
                result.classList.remove('visible');
            }

            // Ask the host for persisted state as soon as the page is ready.
            window.external.sendMessage(JSON.stringify({ action: 'init' }));
        </script>
    </body>
    </html>
    """;
}
