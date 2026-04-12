// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace PokedexTray;

public record PokemonInfo(string Name, string SpriteUrl, string[] Types);

/// <summary>
/// A Pokémon the user has marked as a favorite. Persisted to the Hermes KV store
/// so the favorites bar survives across launches even though the tray window is
/// torn down between sessions.
/// </summary>
public record Favorite(int Id, string Name);
