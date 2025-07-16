using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks.Systems;

public unsafe sealed class ModelSystem : ISystem, IDisposable
{
    private readonly DalamudServices dalamud;
    private readonly Lazy<EcsContainer> ecsContainer;
    private readonly ILogger logger;

    private const string CalculateAndApplyOverallSpeedSig = "E8 ?? ?? ?? ?? 48 8D 8B ?? ?? ?? ?? 48 8B 01 FF 50 ?? 48 8D 8B ?? ?? ?? ?? 48 8B 01 FF 50 ?? F6 83";
    private delegate bool CalculateAndApplyOverallSpeedDelegate(TimelineContainer* a1);
    private readonly Hook<CalculateAndApplyOverallSpeedDelegate> calculateAndApplyOverallSpeedHook = null!;

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

        using var q1 = this.ecsContainer.Value.World.Query<Model>();
        q1.Each((Iter it, int i, ref Model model) =>
        {
            if (model.Spawned)
            {
                DeleteModel(model.GameObjectIndex);
            }
        });

        using var q2 = this.ecsContainer.Value.World.Query<ModelFadeOut>();
        q2.Each((Iter it, int i, ref ModelFadeOut model) =>
        {
            DeleteModel(model.GameObjectIndex);
        });
    }

    public void Register(World world)
    {
        world.System<Model, Position, Rotation, UniformScale, Alpha>()
            .Each((ref Model model, ref Position position, ref Rotation rotation, ref UniformScale scale, ref Alpha alpha) =>
            {
                BattleChara* chara = null;

                if (!model.Spawned)
                {
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
                    chara->Alpha = alpha.Value;
                    var modelData = &chara->ModelContainer;

                    modelData->ModelCharaId = model.ModelCharaId;

                    var name = $"FakeBattleNpc{model.ModelCharaId}";
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
                        // This is needed to get idle/movement sounds working
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
                        chara->EnableDraw();
                        model.DrawEnabled = true;
                    }
                }
            });

        world.Observer<Model, Alpha>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Model _, ref Alpha _) =>
            {
                var model = e.Get<Model>();
                var alpha = e.Get<Alpha>();
                if (model.Spawned)
                {
                    e.CsWorld().Entity()
                        .Set(new ModelFadeOut(model.GameObjectIndex, 1.0f, 1.0f, alpha.Value));
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
                    obj->Alpha = modelFade.Alpha * modelFade.TimeRemaining / modelFade.Duration;
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
        bool result = calculateAndApplyOverallSpeedHook.Original(a1);
        // Convert this to a dictionary lookup if needed
        using var q = this.ecsContainer.Value.World.Query<Model, ModelTimelineSpeed>();
        q.Each((ref Model model, ref ModelTimelineSpeed speed) =>
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
}
