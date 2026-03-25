using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Robust.Shared.Map;
using Robust.Shared.Placement;
using Robust.Shared.Player;

namespace Content.Server.Placement;

public sealed class PlacementLoggerSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlacementEntityEvent>(OnEntityPlacement);
        SubscribeLocalEvent<PlacementTileEvent>(OnTilePlacement);
    }

    private void OnEntityPlacement(PlacementEntityEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;
        var action = ev.PlacementEventAction.ToString().ToLower();

        var logType = ev.PlacementEventAction switch
        {
            PlacementEventAction.Create => LogType.EntitySpawn,
            PlacementEventAction.Erase => LogType.EntityDelete,
            _ => LogType.Action
        };

        if (actorEntity != null)
            _adminLogger.Add(logType, LogImpact.Medium,
                $"{ToPrettyString(actorEntity.Value):actor} used placement system to {action} {ToPrettyString(ev.EditedEntity):subject} at {ev.Coordinates}");
        else if (actor != null)
            _adminLogger.Add(logType, LogImpact.Medium,
                $"{actor:actor} used placement system to {action} {ToPrettyString(ev.EditedEntity):subject} at {ev.Coordinates}");
        else
            _adminLogger.Add(logType, LogImpact.Medium,
                $"Placement system {action}ed {ToPrettyString(ev.EditedEntity):subject} at {ev.Coordinates}");

        // Send admin announcement so all admins can see entity spawns/deletions in chat
        // if (actor != null)
        //     _chatManager.SendAdminAnnouncement($"{actor.Name} used placement system to {action} {ToPrettyString(ev.EditedEntity)}");
    }

    private void OnTilePlacement(PlacementTileEvent ev)
    {
        _player.TryGetSessionById(ev.PlacerNetUserId, out var actor);
        var actorEntity = actor?.AttachedEntity;

        if (actorEntity != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{ToPrettyString(actorEntity.Value):actor} used placement system to set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates}");
        else if (actor != null)
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"{actor} used placement system to set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates}");
        else
            _adminLogger.Add(LogType.Tile, LogImpact.Medium,
                $"Placement system set tile {_tileDefinitionManager[ev.TileType].Name} at {ev.Coordinates}");
    }
}
