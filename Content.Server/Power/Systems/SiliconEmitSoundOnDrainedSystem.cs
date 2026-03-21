// claw command - IPC
using Content.Server.Silicon.Death;
using Content.Shared.Sound.Components;
using Content.Server.Sound;
using Content.Shared.Mobs;

namespace Content.Server.Silicon;

public sealed class EmitSoundOnCritSystem : EntitySystem
{
    [Dependency] private readonly EmitSoundSystem _emitSound = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SiliconEmitSoundOnDrainedComponent, SiliconChargeDeathEvent>(OnDeath);
        SubscribeLocalEvent<SiliconEmitSoundOnDrainedComponent, SiliconChargeAliveEvent>(OnAlive);
        SubscribeLocalEvent<SiliconEmitSoundOnDrainedComponent, MobStateChangedEvent>(OnStateChange);
    }

    private void OnDeath(EntityUid uid, SiliconEmitSoundOnDrainedComponent component, SiliconChargeDeathEvent args)
    {
        var spamComp = EnsureComp<SpamEmitSoundComponent>(uid);

        spamComp.MinInterval = component.MinInterval;
        spamComp.MaxInterval = component.MaxInterval;
        spamComp.PopUp = component.PopUp;
        spamComp.Sound = component.Sound;
        _emitSound.SetEnabled((uid, spamComp), true);
    }

    private void OnAlive(EntityUid uid, SiliconEmitSoundOnDrainedComponent component, SiliconChargeAliveEvent args)
    {
        RemComp<SpamEmitSoundComponent>(uid);
    }

    public void OnStateChange(EntityUid uid, SiliconEmitSoundOnDrainedComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RemComp<SpamEmitSoundComponent>(uid);
    }
}
