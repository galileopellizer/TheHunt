/// <summary>
/// Holds host-selected settings that persist from main menu into the game scene.
/// Set by the host before session creation; read by LobbyManager when loading the map.
/// </summary>
public static class MapSettings
{
    public static string SelectedScene { get; set; } = "GameScene";
    public static int EffigyCount      { get; set; } = 3;
}
