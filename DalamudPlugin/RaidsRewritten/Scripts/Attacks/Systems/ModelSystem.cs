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
                BattleChara* obj = null;

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

                    obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)idx);

                    obj->ObjectKind = ObjectKind.BattleNpc;
                    obj->TargetableStatus = 0;
                    var modelData = &obj->ModelContainer;

                    modelData->ModelCharaId = model.ModelCharaId;
                }
                else
                {
                    obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex((ushort)model.GameObjectIndex);
                }

                obj->SetPosition(position.Value.X, position.Value.Y, position.Value.Z);
                obj->SetRotation(rotation.Value);
                //obj->Position = position.Value;
                //obj->Rotation = rotation.Value;
                obj->Scale = scale.Value;

                this.logger.Info(position.Value.ToString());

                if (!model.DrawEnabled)
                {
                    if (obj->IsReadyToDraw())
                    {
                        obj->EnableDraw();
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
