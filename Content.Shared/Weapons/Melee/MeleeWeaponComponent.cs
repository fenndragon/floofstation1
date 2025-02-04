using Content.Shared.Contests;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Melee;

/// <summary>
/// When given to a mob lets them do unarmed attacks, or when given to an item lets someone wield it to do attacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class MeleeWeaponComponent : Component
{
    // TODO: This is becoming bloated as shit.
    // This should just be its own component for alt attacks.
    /// <summary>
    /// Does this entity do a disarm on alt attack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AltDisarm = true;

    /// <summary>
    /// Should the melee weapon's damage stats be examinable.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    /// Next time this component is allowed to light attack. Heavy attacks are wound up and never have a cooldown.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextAttack;

    /// <summary>
    /// Starts attack cooldown when equipped if true.
    /// </summary>
    [DataField]
    public bool ResetOnHandSelected = true;

    /*
     * Melee combat works based around 2 types of attacks:
     * 1. Click attacks with left-click. This attacks whatever is under your mnouse
     * 2. Wide attacks with right-click + left-click. This attacks whatever is in the direction of your mouse.
     */

    /// <summary>
    /// How many times we can attack per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackRate = 1f;

    /// <summary>
    ///     When power attacking, the swing speed (in attacks per second) is multiplied by this amount
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyRateModifier = 0.8f;
    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [AutoNetworkedField]
    public bool Attacking = false;

    /// <summary>
    /// When did we start a heavy attack.
    /// </summary>
    /// <returns></returns>
    [ViewVariables(VVAccess.ReadWrite), DataField("windUpStart")]
    public TimeSpan? WindUpStart;

    /// <summary>
    /// Heavy attack windup time gets multiplied by this value and the light attack cooldown.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("heavyWindupModifier")]
    public float HeavyWindupModifier = 1.5f;

    /// <summary>
    /// Light attacks get multiplied by this over the base <see cref="Damage"/> value.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("heavyDamageModifier")]
    public FixedPoint2 HeavyDamageModifier = FixedPoint2.New(2);

    /// <summary>
    /// Base damage for this weapon. Can be modified via heavy damage or other means.
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public DamageSpecifier Damage = default!;

    [DataField("bluntStaminaDamageFactor")]
    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 BluntStaminaDamageFactor { get; set; } = 0.5f;

    // TODO: Temporarily 1.5 until interactionoutline is adjusted to use melee, then probably drop to 1.2
    /// <summary>
    /// Nearest edge range to hit an entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    /// <summary>
    ///     Attack range for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyRangeModifier = 1f;

    /// <summary>
    ///     Weapon damage is multiplied by this amount for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyDamageBaseModifier = 1.2f;

    /// <summary>
    ///     Part damage is multiplied by this amount for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyPartDamageMultiplier = 0.5f;

    /// <summary>
    /// Total width of the angle for wide attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle Angle = Angle.FromDegrees(60);

    [DataField, AutoNetworkedField]
    public EntProtoId Animation = "WeaponArcPunch";

    [DataField, AutoNetworkedField]
    public EntProtoId WideAnimation = "WeaponArcSlash";

    /// <summary>
    /// Rotation of the animation.
    /// 0 degrees means the top faces the attacker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle WideAnimationRotation = Angle.Zero;

    [DataField]
    public bool SwingLeft;

    [DataField, AutoNetworkedField]
    public float HeavyStaminaCost = 2.5f;

    [DataField, AutoNetworkedField]
    public int MaxTargets = 3;

    // Sounds

    /// <summary>
    /// This gets played whenever a melee attack is done. This is predicted by the client.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier SoundSwing { get; set; } = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg")
    {
        Params = AudioParams.Default.WithVolume(-3f).WithVariation(0.025f),
    };

    // We do not predict the below sounds in case the client thinks but the server disagrees. If this were the case
    // then a player may doubt if the target actually took damage or not.
    // If overwatch and apex do this then we probably should too.

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundHit;

    /// <summary>
    /// Plays if no damage is done to the target entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier SoundNoDamage { get; set; } = new SoundCollectionSpecifier("WeakHit");

    /// <summary>
    ///     Arguments for the MeleeContestInteractions constructor
    /// </summary>
    [DataField]
    public ContestArgs ContestArgs = new ContestArgs
    {
        DoStaminaInteraction = true,
        StaminaDisadvantage = true,
        DoHealthInteraction = true,
    };

    /// <summary>
    ///     If true, the weapon must be equipped for it to be used.
    ///     E.g boxing gloves must be equipped to your gloves,
    ///     not just held in your hand to be used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MustBeEquippedToUse = false;
}

/// <summary>
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class GetMeleeWeaponEvent : HandledEntityEventArgs
{
    public EntityUid? Weapon;
}

[Serializable, NetSerializable]
public sealed class MeleeWeaponComponentState : ComponentState
{
    // None of the other data matters for client as they're not predicted.

    public float AttackRate;
    public bool Attacking;
    public TimeSpan NextAttack;
    public TimeSpan? WindUpStart;

    public string ClickAnimation;
    public string WideAnimation;
    public float Range;

    public MeleeWeaponComponentState(float attackRate, bool attacking, TimeSpan nextAttack, TimeSpan? windupStart, string clickAnimation, string wideAnimation, float range)
    {
        AttackRate = attackRate;
        Attacking = attacking;
        NextAttack = nextAttack;
        WindUpStart = windupStart;
        ClickAnimation = clickAnimation;
        WideAnimation = wideAnimation;
        Range = range;
    }
}
