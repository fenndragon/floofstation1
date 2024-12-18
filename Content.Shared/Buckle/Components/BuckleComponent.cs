using Content.Shared.Interaction;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Buckle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedBuckleSystem))]
public sealed partial class BuckleComponent : Component
{
    /// <summary>
    /// The range from which this entity can buckle to a <see cref="StrapComponent"/>.
    /// Separated from normal interaction range to fix the "someone buckled to a strap
    /// across a table two tiles away" problem.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = SharedInteractionSystem.InteractionRange / 1.4f;

    /// <summary>
    /// True if the entity is buckled, false otherwise.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool Buckled;

    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? LastEntityBuckledTo;

    /// <summary>
    /// Whether or not collisions should be possible with the entity we are strapped to
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public bool DontCollide;

    /// <summary>
    /// Whether or not we should be allowed to pull the entity we are strapped to
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool PullStrap;

    /// <summary>
    /// The amount of time that must pass for this entity to
    /// be able to unbuckle after recently buckling.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.25f);

    /// <summary>
    /// The time that this entity buckled at.
    /// </summary>
    [ViewVariables]
    public TimeSpan BuckleTime;

    /// <summary>
    /// The strap that this component is buckled to.
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public EntityUid? BuckledTo;

    /// <summary>
    /// The amount of space that this entity occupies in a
    /// <see cref="StrapComponent"/>.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Size = 100;

    /// <summary>
    /// Used for client rendering
    /// </summary>
    [ViewVariables] public int? OriginalDrawDepth;
    [DataField, AutoNetworkedField]
    public float? BuckleDelay;

    [DataField, AutoNetworkedField]
    public bool ClickUnbuckle = true;
}

public sealed partial class UnbuckleAlertEvent : BaseAlertEvent;

/// <summary>
/// Event raised directed at a strap entity before some entity gets buckled to it.
/// </summary>
[ByRefEvent]
public record struct StrapAttemptEvent(
    Entity<StrapComponent> Strap,
    Entity<BuckleComponent> Buckle,
    EntityUid? User,
    bool Popup)
{
    public bool Cancelled;
}

/// <summary>
/// Event raised directed at a buckle entity before it gets buckled to some strap entity.
/// </summary>
[ByRefEvent]
public record struct BuckleAttemptEvent(
    Entity<StrapComponent> Strap,
    Entity<BuckleComponent> Buckle,
    EntityUid? User,
    bool Popup)
{
    public bool Cancelled;
}

/// <summary>
/// Event raised directed at a strap entity before some entity gets unbuckled from it.
/// </summary>
[ByRefEvent]
public record struct UnstrapAttemptEvent(
    Entity<StrapComponent> Strap,
    Entity<BuckleComponent> Buckle,
    EntityUid? User,
    bool Popup)
{
    public bool Cancelled;
}

/// <summary>
/// Event raised directed at a buckle entity before it gets unbuckled.
/// </summary>
[ByRefEvent]
public record struct UnbuckleAttemptEvent(
    Entity<StrapComponent> Strap,
    Entity<BuckleComponent> Buckle,
    EntityUid? User,
    bool Popup)
{
    public bool Cancelled;
}

/// <summary>
/// Event raised directed at a strap entity after something has been buckled to it.
/// </summary>
[ByRefEvent]
public readonly record struct StrappedEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

/// <summary>
/// Event raised directed at a buckle entity after it has been buckled.
/// </summary>
[ByRefEvent]
public readonly record struct BuckledEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

/// <summary>
/// Event raised directed at a strap entity after something has been unbuckled from it.
/// </summary>
[ByRefEvent]
public readonly record struct UnstrappedEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

/// <summary>
/// Event raised directed at a buckle entity after it has been unbuckled from some strap entity.
/// </summary>
[ByRefEvent]
public readonly record struct UnbuckledEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

[Serializable, NetSerializable]
public enum BuckleVisuals
{
    Buckled
}
