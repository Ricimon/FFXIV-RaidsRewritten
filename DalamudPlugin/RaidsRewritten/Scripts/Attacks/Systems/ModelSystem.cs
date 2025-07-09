using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks.Systems;

public unsafe sealed class ModelSystem(DalamudServices dalamud, Lazy<EcsContainer> ecsContainer, ILogger logger) : ISystem, IDisposable
{
    private readonly DalamudServices dalamud = dalamud;
    private readonly Lazy<EcsContainer> ecsContainer = ecsContainer;
    private readonly ILogger logger = logger;

    public void Dispose()
    {
        this.ecsContainer.Value.World.Query<Model>()
            .Each((Iter it, int i, ref Model model) =>
            {
                if (model.Spawned)
                {
                    DeleteModel(model.GameObjectIndex);
                }
            });

        this.ecsContainer.Value.World.Query<ModelFadeOut>()
            .Each((Iter it, int i, ref ModelFadeOut model) =>
            {
                DeleteModel(model.GameObjectIndex);
            });
    }

    public void Register(World world)
    {
        world.System<Model, Position, Rotation, UniformScale>()
            .Each((ref Model model, ref Position position, ref Rotation rotation, ref UniformScale scale) =>
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

    private void DeleteModel(uint gameObjectId)
    {
        var obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)gameObjectId);
        obj->DisableDraw();
        ClientObjectManager.Instance()->DeleteObjectByIndex((ushort)gameObjectId, 0);
    }
}
