﻿using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Surgery.Tools;
using Content.Shared.Targeting;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodySystem))]
public sealed partial class BodyPartComponent : Component, ISurgeryToolComponent
{
    // Need to set this on container changes as it may be several transform parents up the hierarchy.
    /// <summary>
    /// Parent body for this part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    [DataField, AutoNetworkedField]
    public EntityUid? OriginalBody;

    [DataField, AutoNetworkedField]
    public BodyPartSlot? ParentSlot;

    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public BodyPartType PartType = BodyPartType.Other;

    // TODO BODY Replace with a simulation of organs
    /// <summary>
    ///     Whether or not the owning <see cref="Body"/> will die if all
    ///     <see cref="BodyComponent"/>s of this type are removed from it.
    /// </summary>
    [DataField("vital"), AutoNetworkedField]
    public bool IsVital;

    /// <summary>
    /// Amount of damage to deal when the part gets removed.
    /// Only works if IsVital is true.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 VitalDamage = 100;


    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    [DataField, AlwaysPushInheritance]
    public string ToolName { get; set; } = "A body part";

    [DataField, AlwaysPushInheritance]
    public float Speed { get; set; } = 1f;

    [DataField, AutoNetworkedField]
    public bool? Used { get; set; } = null;

    /// <summary>
    /// Child body parts attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, BodyPartSlot> Children = new();

    /// <summary>
    /// Organs attached to this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, OrganSlot> Organs = new();

    /// <summary>
    /// What's the max health this body part can have?
    /// </summary>
    [DataField]
    public float MinIntegrity;

    /// <summary>
    /// Whether this body part can be severed or not
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanSever = true;

    /// <summary>
    /// Whether this body part is enabled or not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Whether this body part can be enabled or not. Used for non-functional prosthetics.
    /// </summary>
    [DataField]
    public bool CanEnable = true;

    /// <summary>
    /// Whether this body part can attach children or not.
    /// </summary>
    [DataField]
    public bool CanAttachChildren = true;

    /// <summary>
    /// How long it takes to run another self heal tick on the body part.
    /// </summary>
    [DataField]
    public float HealingTime = 30;

    /// <summary>
    /// How long it has been since the last self heal tick on the body part.
    /// </summary>
    public float HealingTimer;

    /// <summary>
    /// How much health to heal on the body part per tick.
    /// </summary>
    [DataField]
    public float SelfHealingAmount = 0.5f; // Floof - reduced from 5 due to body part damage nerf. Was 10/min, now 1/min.

    /// <summary>
    /// The name of the container for this body part. Used in insertion surgeries.
    /// </summary>
    [DataField]
    public string ContainerName { get; set; } = "part_slot";

    /// <summary>
    /// The slot for item insertion.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ItemSlot ItemInsertionSlot = new();

    /// <summary>
    ///     Current species. Dictates things like body part sprites.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Species { get; set; } = "";

    /// <summary>
    /// The total damage that has to be dealt to a body part
    /// to make possible severing it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SeverIntegrity = 300; // Floof - increased to 300 due to frequent RR

    /// <summary>
    /// The ID of the base layer for this body part.
    /// </summary>
    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public string? BaseLayerId;

    /// <summary>
    /// On what TargetIntegrity we should re-enable the part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TargetIntegrity EnableIntegrity = TargetIntegrity.ModeratelyWounded;

    [DataField, AutoNetworkedField]
    public Dictionary<TargetIntegrity, float> IntegrityThresholds = new()
    {
        { TargetIntegrity.CriticallyWounded, 100 }, // Floof - Increased from 90
        { TargetIntegrity.HeavilyWounded, 70 }, // Floof note: this is where "passive healing" stops
        { TargetIntegrity.ModeratelyWounded, 60 },
        { TargetIntegrity.SomewhatWounded, 40},
        { TargetIntegrity.LightlyWounded, 20 },
        { TargetIntegrity.Healthy, 10 },
    };

    /// <summary>
    ///     When attached, the part will ensure these components on the entity, and delete them on removal.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnAdd;

    /// <summary>
    ///     When removed, the part will ensure these components on the entity, and add them on removal.
    /// </summary>
    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? OnRemove;

    /// <summary>
    /// These are only for VV/Debug do not use these for gameplay/systems
    /// </summary>
    [ViewVariables]
    private List<ContainerSlot> BodyPartSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Children.Keys)
            {
                temp.Add((ContainerSlot) containerSystem.GetContainer(Owner, SharedBodySystem.PartSlotContainerIdPrefix+slotId));
            }

            return temp;
        }
    }

    [ViewVariables]
    private List<ContainerSlot> OrganSlotsVV
    {
        get
        {
            List<ContainerSlot> temp = new();
            var containerSystem = IoCManager.Resolve<IEntityManager>().System<SharedContainerSystem>();

            foreach (var slotId in Organs.Keys)
            {
                temp.Add((ContainerSlot) containerSystem.GetContainer(Owner, SharedBodySystem.OrganSlotContainerIdPrefix+slotId));
            }

            return temp;
        }
    }
}

/// <summary>
/// Contains metadata about a body part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct BodyPartSlot
{
    public string Id;
    public BodyPartType Type;

    public BodyPartSlot(string id, BodyPartType type)
    {
        Id = id;
        Type = type;
    }
};

/// <summary>
/// Contains metadata about an organ part in relation to its slot.
/// </summary>
[NetSerializable, Serializable]
[DataRecord]
public partial struct OrganSlot
{
    public string Id;

    public OrganSlot(string id)
    {
        Id = id;
    }
};
