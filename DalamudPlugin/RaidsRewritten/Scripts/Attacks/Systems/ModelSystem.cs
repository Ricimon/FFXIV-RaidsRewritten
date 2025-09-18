using System;
using Dalamud.Hooking;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Systems;

public unsafe sealed class ModelSystem : ISystem, IDisposable
{
    private readonly DalamudServices dalamud;
    private readonly Lazy<EcsContainer> ecsContainer;
    private readonly ILogger logger;

    private const string CalculateAndApplyOverallSpeedSig = "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 48 8B 01 FF 50 ?? 48 8D 8B ?? ?? ?? ?? 48 8B 01 FF 50 ?? F6 83";
    private delegate bool CalculateAndApplyOverallSpeedDelegate(TimelineContainer* a1);
    private readonly Hook<CalculateAndApplyOverallSpeedDelegate> calculateAndApplyOverallSpeedHook = null!;

    private Query<Model, ModelTimelineSpeed> modelTimelineSpeedQuery;

    public ModelSystem(DalamudServices dalamud, Lazy<EcsContainer> ecsContainer, ILogger logger)
    {
        this.dalamud = dalamud;
        this.ecsContainer = ecsContainer;
        this.logger = logger;

        var calculateAndApplyAddress = dalamud.SigScanner.ScanText(CalculateAndApplyOverallSpeedSig);
        calculateAndApplyOverallSpeedHook = dalamud.GameInteropProvider.HookFromAddress<CalculateAndApplyOverallSpeedDelegate>(calculateAndApplyAddress, CalculateAndApplyOverallSpeedDetour);
        calculateAndApplyOverallSpeedHook.Enable();
    }

    public void Dispose()
    {
        calculateAndApplyOverallSpeedHook.Dispose();

        this.modelTimelineSpeedQuery.Dispose();

        using (var q = this.ecsContainer.Value.World.Query<Model>())
        {
            q.Each((Iter it, int i, ref Model model) =>
            {
                if (model.Spawned)
                {
                    DeleteModel(model.GameObjectIndex);
                }
            });
        }

        using (var q = this.ecsContainer.Value.World.Query<ModelFadeOut>())
        {
            q.Each((Iter it, int i, ref ModelFadeOut model) =>
            {
                DeleteModel(model.GameObjectIndex);
            });
        }
    }

    public void Register(Flecs.NET.Core.World world)
    {
        world.System<Model, Position, Rotation, UniformScale>()
            .Each((Iter it, int i, ref Model model, ref Position position, ref Rotation rotation, ref UniformScale scale) =>
            {
                BattleChara* chara = null;

                if (!model.Spawned)
                {
                    var entity = it.Entity(i);
                    var idx = ClientObjectManager.Instance()->CreateBattleCharacter();
                    if (idx == 0xFFFFFFFF)
                    {
                        this.logger.Warn("Model could not be spawned");
                        return;
                    }

                    model.GameObjectIndex = idx;
                    model.Spawned = true;

                    var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)idx);
                    chara = (BattleChara*)obj;

                    chara->ObjectKind = ObjectKind.BattleNpc;
                    chara->TargetableStatus = 0;
                    chara->Position = position.Value;
                    chara->Rotation = rotation.Value;
                    chara->Scale = scale.Value;

                    if (entity.TryGet<AnimationState>(out var animationState))
                    {
                        chara->Timeline.AnimationState[0] = animationState.Value1;
                        chara->Timeline.AnimationState[1] = animationState.Value2;
                    }

                    if (entity.TryGet<NpcEquipRow>(out var npcEquip) && npcEquip.Value != 0)
                    {
                        var row = dalamud.DataManager.GetExcelSheet<NpcEquip>().GetRow(npcEquip.Value);
                        // from Brio https://github.com/Etheirys/Brio/blob/main/Brio/Game/Actor/ActorAppearanceService.cs
                        var (mainHand, offHand, equipment) = FromNpcEquip(row);
                        fixed (EquipmentModelId* ptr = chara->DrawData.EquipmentModelIds)
                        {
                            *(Interop.Structs.ActorEquipment*)ptr = equipment;
                        }
                        chara->DrawData.LoadWeapon(DrawDataContainer.WeaponSlot.MainHand, mainHand, 0, 0, 0, 0);
                        chara->DrawData.LoadWeapon(DrawDataContainer.WeaponSlot.OffHand, offHand, 0, 0, 0, 0);
                    }

                    var modelData = &chara->ModelContainer;
                    modelData->ModelCharaId = model.ModelCharaId;

                    var name = $"FakeBattleNpc[{model.ModelCharaId}]";
                    for (int x = 0; x < name.Length; x++)
                    {
                        obj->Name[x] = (byte)name[x];
                    }
                    obj->Name[name.Length] = 0;
                    // Needed to get Actor VFX to play on GameObject
                    obj->RenderFlags = 0;

                    model.GameObject = this.dalamud.ObjectTable.CreateObjectReference((nint)obj);

                    if (model.GameObject != null)
                    {
                        // This line is in Brio, and is needed to get animations working on a human model,
                        // but this does not play action VFX outside of gpose.
                        //var localPlayer = dalamud.ClientState.LocalPlayer;
                        //if (localPlayer != null)
                        //{
                        //    chara->CharacterSetup.CopyFromCharacter(localPlayer.Character(), CharacterSetupContainer.CopyFlags.WeaponHiding);
                        //}
                        // This is needed to get idle/movement sounds working (must be called after model id is assigned)
                        chara->CharacterSetup.CopyFromCharacter((Character*)model.GameObject.Address, CharacterSetupContainer.CopyFlags.None);
                    }
                }
                else
                {
                    chara = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);

                    chara->SetPosition(position.Value.X, position.Value.Y, position.Value.Z);
                    chara->SetRotation(rotation.Value);
                    chara->Scale = scale.Value;
                }

                if (!model.DrawEnabled)
                {
                    if (chara->IsReadyToDraw())
                    {
                        // This is needed to play action sounds, so this is called even for invalid models
                        chara->EnableDraw();
                        model.DrawEnabled = true;
                    }
                }
            });

        world.System<Model, OneTimeModelTimeline>()
            .Each((Iter it, int i, ref Model model, ref OneTimeModelTimeline timeline) =>
            {
                if (model.GameObject != null && model.DrawEnabled)
                {
                    var obj = ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
                    var chara = (Character*)obj;
                    if (chara != null)
                    {
                        if (!timeline.Played)
                        {
                            //chara->SetMode(CharacterModes.AnimLock, 0);
                            chara->Timeline.BaseOverride = timeline.Id;
                            timeline.Played = true;
                        }
                        else
                        {
                            chara->Timeline.BaseOverride = 0;
                            it.Entity(i).Remove<OneTimeModelTimeline>();
                        }
                    }
                }
            });

        world.System<Model, TimelineBase>()
            .Each((Iter it, int i, ref Model model, ref TimelineBase animationState) =>
            {
                // set animation
                var clientObjectManager = ClientObjectManager.Instance();
                if (clientObjectManager == null) { return; }

                var obj = clientObjectManager->GetObjectByIndex((ushort)model.GameObjectIndex);
                var chara = (Character*)obj;
                if (chara == null) { return; }

                chara->Timeline.BaseOverride = animationState.Value;

                if (animationState.Interrupt) {
                    chara->Timeline.TimelineSequencer.PlayTimeline(animationState.Value);
                    animationState.Interrupt = false;
                }
            });

        world.Observer<Model>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Model _) =>
            {
                var model = e.Get<Model>();
                if (model.Spawned)
                {
                    e.CsWorld().Entity()
                        .Set(new ModelFadeOut(model.GameObjectIndex, 1.0f, 1.0f));
                }
            });

        world.System<ModelFadeOut>()
            .Each((Iter it, int i, ref ModelFadeOut modelFade) =>
            {
                modelFade.TimeRemaining -= it.DeltaTime();
                if (modelFade.TimeRemaining > 0)
                {
                    var obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)modelFade.GameObjectIndex);
                    if (obj == null)
                    {
                        it.Entity(i).Destruct();
                        return;
                    }
                    obj->Alpha = modelFade.TimeRemaining / modelFade.Duration;
                }
                else
                {
                    DeleteModel(modelFade.GameObjectIndex);
                    it.Entity(i).Destruct();
                }
            });
    }

    private bool CalculateAndApplyOverallSpeedDetour(TimelineContainer* a1)
    {
        if (!this.modelTimelineSpeedQuery.IsValid())
        {
            // This can't be constructed in the constructor because it would cause a circular dependency reference
            this.modelTimelineSpeedQuery = this.ecsContainer.Value.World.Query<Model, ModelTimelineSpeed>();
        }

        bool result = calculateAndApplyOverallSpeedHook.Original(a1);
        // Convert this to a dictionary lookup if needed
        this.modelTimelineSpeedQuery.Each((ref Model model, ref ModelTimelineSpeed speed) =>
        {
            if (model.GameObject != null &&
                model.GameObject.Address == (nint)a1->OwnerObject)
            {
                a1->OverallSpeed = speed.Value;
                result |= true;
            }
        });
        return result;
    }

    private void DeleteModel(uint gameObjectId)
    {
        var obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)gameObjectId);
        obj->DisableDraw();
        ClientObjectManager.Instance()->DeleteObjectByIndex((ushort)gameObjectId, 0);
    }

    // from Brio https://github.com/Etheirys/Brio/blob/main/Brio/Game/Actor/Appearance/ActorAppearance.cs
    private static (WeaponModelId, WeaponModelId, Interop.Structs.ActorEquipment) FromNpcEquip(NpcEquip npcEquip)
    {
        var equipment = new Interop.Structs.ActorEquipment();
        var mainHand = new WeaponModelId();
        var offHand = new WeaponModelId();

        equipment.Head.Value = npcEquip.ModelHead;
        equipment.Head.Stain0 = (byte)npcEquip.DyeHead.RowId;
        equipment.Head.Stain1 = (byte)npcEquip.Dye2Head.RowId;
        equipment.Top.Value = npcEquip.ModelBody;
        equipment.Top.Stain0 = (byte)npcEquip.DyeBody.RowId;
        equipment.Top.Stain1 = (byte)npcEquip.Dye2Body.RowId;
        equipment.Arms.Value = npcEquip.ModelHands;
        equipment.Arms.Stain0 = (byte)npcEquip.DyeHands.RowId;
        equipment.Arms.Stain1 = (byte)npcEquip.Dye2Hands.RowId;
        equipment.Legs.Value = npcEquip.ModelLegs;
        equipment.Legs.Stain0 = (byte)npcEquip.DyeLegs.RowId;
        equipment.Legs.Stain1 = (byte)npcEquip.Dye2Legs.RowId;
        equipment.Feet.Value = npcEquip.ModelFeet;
        equipment.Feet.Stain0 = (byte)npcEquip.DyeFeet.RowId;
        equipment.Feet.Stain1 = (byte)npcEquip.Dye2Feet.RowId;
        equipment.Ear.Value = npcEquip.ModelEars;
        equipment.Ear.Stain0 = (byte)npcEquip.DyeEars.RowId;
        equipment.Ear.Stain1 = (byte)npcEquip.Dye2Ears.RowId;
        equipment.Neck.Value = npcEquip.ModelNeck;
        equipment.Neck.Stain0 = (byte)npcEquip.DyeNeck.RowId;
        equipment.Neck.Stain1 = (byte)npcEquip.Dye2Neck.RowId;
        equipment.Wrist.Value = npcEquip.ModelWrists;
        equipment.Wrist.Stain0 = (byte)npcEquip.DyeWrists.RowId;
        equipment.Wrist.Stain1 = (byte)npcEquip.Dye2Wrists.RowId;
        equipment.RFinger.Value = npcEquip.ModelRightRing;
        equipment.RFinger.Stain0 = (byte)npcEquip.DyeRightRing.RowId;
        equipment.RFinger.Stain1 = (byte)npcEquip.Dye2RightRing.RowId;
        equipment.LFinger.Value = npcEquip.ModelLeftRing;
        equipment.LFinger.Stain0 = (byte)npcEquip.DyeLeftRing.RowId;
        equipment.LFinger.Stain1 = (byte)npcEquip.Dye2LeftRing.RowId;

        mainHand.Value = npcEquip.ModelMainHand;
        mainHand.Stain0 = (byte)npcEquip.DyeMainHand.RowId;
        mainHand.Stain1 = (byte)npcEquip.Dye2MainHand.RowId;
        offHand.Value = npcEquip.ModelOffHand;
        offHand.Stain0 = (byte)npcEquip.DyeOffHand.RowId;
        offHand.Stain1 = (byte)npcEquip.Dye2OffHand.RowId;

        return (mainHand, offHand, equipment);
    }
}
