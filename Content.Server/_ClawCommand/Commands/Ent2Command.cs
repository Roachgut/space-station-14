using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;

namespace Content.Server._ClawCommand.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Moderator)]
internal sealed class Ent2Command : ToolshedCommand
{
    [CommandImplementation]
    public EntityUid Ent2(EntityUid uid) => uid;
}
