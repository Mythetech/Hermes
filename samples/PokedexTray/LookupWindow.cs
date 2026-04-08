// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text.Json;
using Hermes;

namespace PokedexTray;

public static class LookupWindow
{
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

                case "lookup":
                    var id = root.GetProperty("id").GetInt32();
                    var info = await service.LookupAsync(id);

                    string response;
                    if (info is not null)
                    {
                        response = JsonSerializer.Serialize(new
                        {
                            name = info.Name,
                            sprite = info.SpriteUrl,
                            types = info.Types,
                            cached = service.WasCacheHit
                        });
                    }
                    else
                    {
                        response = JsonSerializer.Serialize(new { error = $"Pokemon #{id} not found" });
                    }

                    window.Invoke(() => window.SendMessage(response));
                    break;
            }
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { error = ex.Message });
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
        .status {
            font-size: 12px;
            color: #888;
            margin-top: 4px;
        }
        .status.cached { color: #2ecc71; }
        .status.fetched { color: #3498db; }
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
            <div class="name" id="pokeName"></div>
            <div class="types" id="types"></div>
            <div class="status" id="status"></div>
        </div>
        <script>
            const idInput = document.getElementById('pokemonId');
            const searchBtn = document.getElementById('searchBtn');
            const loading = document.getElementById('loading');
            const errorEl = document.getElementById('error');
            const result = document.getElementById('result');

            idInput.addEventListener('keydown', e => {
                if (e.key === 'Enter') doSearch();
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

            window.external.receiveMessage(function(msg) {
                searchBtn.disabled = false;
                loading.classList.remove('visible');
                const data = JSON.parse(msg);
                if (data.error) {
                    showError(data.error);
                    return;
                }
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
                result.classList.add('visible');
            });

            function showError(msg) {
                errorEl.textContent = msg;
                result.classList.remove('visible');
            }
        </script>
    </body>
    </html>
    """;
}
