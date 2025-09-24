using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using RaidsRewritten.Services.Posing;
using RaidsRewritten.Structures.Files;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Structures;

public sealed class CharacterSkeletonContainer : IDisposable
{
    public Skeleton? CharacterSkeleton { get; private set; }
    public Skeleton? MainHandSkeleton { get; private set; }
    public Skeleton? OffHandSkeleton { get; private set; }

    public PoseInfo PoseInfo { get; set; } = new PoseInfo();

    private readonly ICharacter character;
    private readonly SkeletonService skeletonService;

    private readonly List<Action<Bone, BonePoseInfo>> _transitiveActions = [];

    public unsafe CharacterSkeletonContainer(ICharacter character, SkeletonService skeletonService)
    {
        this.character = character;
        this.skeletonService = skeletonService;

        this.skeletonService.SkeletonUpdateStart += OnSkeletonUpdateStart;
        this.skeletonService.SkeletonUpdateEnd += OnSkeletonUpdateEnd;
    }

    public void Dispose()
    {
        this.skeletonService.SkeletonUpdateStart -= OnSkeletonUpdateStart;
        this.skeletonService.SkeletonUpdateEnd -= OnSkeletonUpdateEnd;
    }

    public void RegisterTransitiveAction(Action<Bone, BonePoseInfo> action)
    {
        _transitiveActions.Add(action);
    }

    public void ExecuteTransitiveActions(Bone bone, BonePoseInfo poseInfo)
    {
        _transitiveActions.ForEach(a => a(bone, poseInfo));
    }

    public void ImportSkeletonPose(PoseFile poseFile, PoseImporterOptions options, bool expressionPhase = false)
    {
        var importer = new PoseImporter(poseFile, options, expressionPhase);
        RegisterTransitiveAction(importer.ApplyBone);
    }

    public unsafe BonePoseInfo GetBonePose(Bone bone)
    {
        if (CharacterSkeleton != null && CharacterSkeleton == bone.Skeleton)
        {
            return PoseInfo.GetPoseInfo(bone, PoseInfoSlot.Character);
        }

        if (MainHandSkeleton != null && MainHandSkeleton == bone.Skeleton)
        {
            return PoseInfo.GetPoseInfo(bone, PoseInfoSlot.MainHand);
        }

        if (OffHandSkeleton != null && OffHandSkeleton == bone.Skeleton)
        {
            return PoseInfo.GetPoseInfo(bone, PoseInfoSlot.OffHand);
        }

        return PoseInfo.GetPoseInfo(bone, PoseInfoSlot.Unknown);
    }

    private unsafe void UpdateCache()
    {
        CharacterSkeleton = skeletonService.GetSkeleton(character.GetCharacterBase());
        MainHandSkeleton = skeletonService.GetSkeleton(character.GetWeaponCharacterBase(ActorEquipSlot.MainHand));
        OffHandSkeleton = skeletonService.GetSkeleton(character.GetWeaponCharacterBase(ActorEquipSlot.OffHand));

        this.skeletonService.RegisterForFrameUpdate(CharacterSkeleton, this);
        this.skeletonService.RegisterForFrameUpdate(MainHandSkeleton, this);
        this.skeletonService.RegisterForFrameUpdate(OffHandSkeleton, this);
    }

    private void OnSkeletonUpdateStart()
    {
        UpdateCache();
    }

    private void OnSkeletonUpdateEnd()
    {
        _transitiveActions.Clear();
    }
}
