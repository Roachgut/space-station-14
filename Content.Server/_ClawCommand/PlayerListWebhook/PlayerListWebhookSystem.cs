using System.Text.Json.Nodes;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.PlayerListWebhook;

/// <summary>
///     Maintains a live-updating Discord message showing current server status and player count,
///     replacing the external wizard-cogs GameServerStatus bot with an internal implementation.
/// </summary>
public sealed class PlayerListWebhookSystem : EntitySystem
{
    [Dependency] private readonly IBaseServer _baseServer = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IGameMapManager _gameMapManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ISawmill _sawmill = default!;

    private string _webhookUrl = string.Empty;
    private bool _enabled;
    private float _updateInterval = 60f;

    private WebhookIdentifier _webhookIdentifier;
    private ulong _messageId;
    private TimeSpan _lastUpdateTime;
    private bool _initialized;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("discord.playerlist");

        _cfg.OnValueChanged(CCVars.DiscordPlayerlistStatus, OnWebhookUrlChanged, true);
        _cfg.OnValueChanged(CCVars.DiscordPlayerlistStatusEnabled, OnEnabledChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _cfg.UnsubValueChanged(CCVars.DiscordPlayerlistStatus, OnWebhookUrlChanged);
        _cfg.UnsubValueChanged(CCVars.DiscordPlayerlistStatusEnabled, OnEnabledChanged);
    }

    private void OnWebhookUrlChanged(string url)
    {
        _webhookUrl = url;
        // Reset state so we re-initialize with the new webhook
        _initialized = false;
        _messageId = 0;
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || string.IsNullOrWhiteSpace(_webhookUrl))
            return;

        var now = _gameTiming.RealTime;
        if (now - _lastUpdateTime < TimeSpan.FromSeconds(_updateInterval))
            return;

        _lastUpdateTime = now;
        SendUpdate();
    }

    private async void SendUpdate()
    {
        try
        {
            // Initialize webhook identifier if needed
            if (!_initialized)
            {
                var webhookData = await _discord.GetWebhook(_webhookUrl);
                if (webhookData == null)
                {
                    _sawmill.Warning("Failed to get webhook data for player list. Is the URL correct?");
                    return;
                }

                _webhookIdentifier = webhookData.Value.ToIdentifier();
                _initialized = true;
            }

            var payload = BuildPayload();

            if (_messageId == 0)
            {
                // Create initial message
                var response = await _discord.CreateMessage(_webhookIdentifier, payload);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var id = JsonNode.Parse(content)?["id"]?.GetValue<string>();
                    if (id != null)
                        _messageId = ulong.Parse(id);

                    _sawmill.Debug("Created player list message with ID {0}", _messageId);
                }
                else
                {
                    _sawmill.Error("Failed to create player list message: {0}", response.StatusCode);
                }
            }
            else
            {
                // Edit existing message
                var response = await _discord.EditMessage(_webhookIdentifier, _messageId, payload);
                if (!response.IsSuccessStatusCode)
                {
                    // Message might have been deleted, try creating a new one next tick
                    _sawmill.Warning("Failed to edit player list message (ID {0}), will recreate.", _messageId);
                    _messageId = 0;
                }
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error updating player list webhook:\n{e}");
            // If something went wrong, reset message ID so we try to create a new one
            _messageId = 0;
        }
    }

    private WebhookPayload BuildPayload()
    {
        var gameTicker = EntityManager.System<GameTicker>();
        var sharedTicker = EntityManager.System<SharedGameTicker>();

        var serverName = _baseServer.ServerName;
        var playerCount = _playerManager.PlayerCount;
        var maxPlayers = _cfg.GetCVar(CCVars.SoftMaxPlayers);
        var runLevel = gameTicker.RunLevel;
        var roundId = sharedTicker.RoundId;
        var mapName = _gameMapManager.GetSelectedMap()?.MapName ?? "Unknown";
        // Use Decoy preset if set (matches what the /status endpoint shows publicly)
        var presetProto = gameTicker.Decoy ?? gameTicker.CurrentPreset ?? gameTicker.Preset;
        var preset = presetProto != null ? Loc.GetString(presetProto.ModeTitle) : "Unknown";

        // Build status string with elapsed time if in-round
        var status = runLevel switch
        {
            GameRunLevel.PreRoundLobby => "In Lobby",
            GameRunLevel.InRound => FormatInRoundStatus(sharedTicker),
            GameRunLevel.PostRound => "Ending",
            _ => "Unknown"
        };

        // Color: green for in-round, blue for lobby, yellow for ending
        var color = runLevel switch
        {
            GameRunLevel.PreRoundLobby => 0x3498DB, // Blue
            GameRunLevel.InRound => 0x2ECC71,       // Green
            GameRunLevel.PostRound => 0xF1C40F,     // Yellow
            _ => 0x95A5A6                            // Gray
        };

        return new WebhookPayload
        {
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = serverName,
                    Description =
                        $"**Players:** {playerCount}/{maxPlayers}\n" +
                        $"**Status:** {status}\n" +
                        $"**Map:** {mapName}\n" +
                        $"**Preset:** {preset}",
                    Color = color,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = $"Round ID: {roundId}"
                    }
                }
            }
        };
    }

    private string FormatInRoundStatus(SharedGameTicker sharedTicker)
    {
        var elapsed = _gameTiming.RealTime - sharedTicker.RoundStartTimeSpan;

        if (elapsed.TotalSeconds < 0)
            return "In game";

        // Format elapsed time similar to the Python bot's humanize_timedelta
        var parts = new List<string>();

        if (elapsed.Days > 0)
            parts.Add($"{elapsed.Days} day{(elapsed.Days != 1 ? "s" : "")}");
        if (elapsed.Hours > 0)
            parts.Add($"{elapsed.Hours} hour{(elapsed.Hours != 1 ? "s" : "")}");
        if (elapsed.Minutes > 0)
            parts.Add($"{elapsed.Minutes} minute{(elapsed.Minutes != 1 ? "s" : "")}");

        if (parts.Count == 0)
            return "In game";

        return $"In game ({string.Join(", ", parts)})";
    }
}
