using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using AsyncAwaitBestPractices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class LightDarkTowers : Mechanic
{
    private const uint FinalWordActionId = 18557;
    // Contact Regulation and Escape Detection, the orange and purple baits
    private const uint LightBeaconStatusId = 2153;
    private const uint DarkBeaconStatusId = 2155;
    // Lands between the Ordained Motion/Stillness checks at 7s and 17s after Final Word
    private const float TowerSpawnDelay = 10.0f;

    private static readonly Vector3 ArenaCenter = new(100, 0, 100);
    // Same as the Nisi towers
    private const float TowerDistance = 15.0f;

    // m0196, the folder the pop effects come from
    private const byte MonsterModelType = 3;
    private const ushort AthenaModel = 196;

    private readonly List<Entity> attacks = [];

    private int? athenaModelCharaId;

    public int RngSeed { get; set; }

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != FinalWordActionId) { return; }

        var delayedSend = DelayedAction.Create(Stage, () =>
        {
            ulong lightBeacon = 0;
            ulong darkBeacon = 0;
            CommonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
            {
                if (player.PlayerCharacter == null) { return; }
                if (!e.TryGet(out Player.ContentId contentId)) { return; }

                foreach (var status in player.PlayerCharacter.StatusList)
                {
                    if (status.StatusId == LightBeaconStatusId)
                    {
                        lightBeacon = contentId.Value;
                    }
                    else if (status.StatusId == DarkBeaconStatusId)
                    {
                        darkBeacon = contentId.Value;
                    }
                }
            });

            var (lightTowerDirection, darkTowerDirection) = GetTowerDirections(RngSeed);
            SendStartMechanic("LightDarkTowers", lightBeacon, darkBeacon,
                GetTowerPosition(lightTowerDirection), GetTowerPosition(darkTowerDirection));
        }, TowerSpawnDelay);
        attacks.Add(delayedSend);
    }

    // Any two different cardinals or intercardinals, 0 is north going clockwise.
    // Shanoa's mechanic can call this with the same seed to know where the towers go
    public static (int Light, int Dark) GetTowerDirections(int rngSeed)
    {
        var random = new Random(rngSeed + 0x70E5);
        var light = random.Next(8);
        var dark = (light + random.Next(1, 8)) % 8;
        return (light, dark);
    }

    public static Vector3 GetTowerPosition(int direction)
    {
        var angle = direction * 0.25f * MathF.PI;
        return ArenaCenter + TowerDistance * new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle));
    }

    // Happy path: you're the light beacon, the first other party member found is the dark
    // beacon, and each tower drops on its beacon holder
    public override void DebugSimulate()
    {
        var localContentId = Dalamud.PlayerState.ContentId;
        var (lightTowerDirection, darkTowerDirection) = GetTowerDirections(RngSeed);
        var lightTowerPosition = GetTowerPosition(lightTowerDirection);
        var darkTowerPosition = GetTowerPosition(darkTowerDirection);
        ulong darkBeacon = 0;
        CommonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
        {
            if (player.PlayerCharacter == null) { return; }
            if (!e.TryGet(out Player.ContentId contentId)) { return; }
            if (contentId.Value == localContentId)
            {
                darkTowerPosition = player.PlayerCharacter.Position;
            }
            else if (darkBeacon == 0)
            {
                darkBeacon = contentId.Value;
                lightTowerPosition = player.PlayerCharacter.Position;
            }
        });

        SendStartMechanic(Guid.NewGuid().ToString(), localContentId, darkBeacon, lightTowerPosition, darkTowerPosition);
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        if (string.IsNullOrEmpty(payload.extraData)) { return; }

        var position = new Vector3(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default);
        var rotation = new Rotation(payload.rotation ?? default);

        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaSpawnLightDarkTower:
                {
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 2) { return; }
                    if (!BigInteger.TryParse(arguments[0], out var vfxId)) { return; }
                    var towerVfx = arguments[1];
                    if (!GameUtilities.IsValidVfxPath(towerVfx)) { return; }

                    // The server's stop_vfx for this id removes the tower
                    var tower = FakeActor.CreateInvalidModel(Stage)
                        .Set(new Position(position))
                        .Set(rotation)
                        .Set(new ActorVfx(towerVfx))
                        .Set(new VfxId(vfxId));
                    attacks.Add(tower);
                }
                break;

            case (int)NetworkMechanicCommand.TeaPopLightDarkTower:
                {
                    var popVfx = payload.extraData;
                    if (!GameUtilities.IsValidVfxPath(popVfx)) { return; }

                    // The pop is one of Athena's effects and only shows up on her skeleton,
                    // so it plays on a hidden copy of her model
                    var athenaModelId = GetAthenaModelCharaId();
                    if (athenaModelId == null) { return; }

                    var pop = Stage.Entity()
                        .Set(new Model(athenaModelId.Value))
                        .Set(new Position(position))
                        .Set(rotation)
                        .Set(new Scale(Vector3.One))
                        .Set(new UniformScale(1f))
                        .Set(new Alpha(0))
                        .Set(new FakeActor.Component())
                        .Add<EnsureModelIsDrawn>()
                        .Add<Attack>()
                        .Set(new ActorVfx(popVfx));
                    attacks.Add(pop);
                }
                break;
        }
    }

    private int? GetAthenaModelCharaId()
    {
        if (athenaModelCharaId == null)
        {
            foreach (var row in Dalamud.DataManager.GetExcelSheet<ModelChara>())
            {
                if (row.Type == MonsterModelType && row.Model == AthenaModel)
                {
                    athenaModelCharaId = (int)row.RowId;
                    break;
                }
            }
        }
        return athenaModelCharaId;
    }

    private void SendStartMechanic(string requestId, ulong lightBeacon, ulong darkBeacon, Vector3 lightTower, Vector3 darkTower)
    {
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = requestId,
                mechanicId = (uint)NetworkMechanic.TeaLightDarkTowers,
                extraData = string.Join(',',
                    lightBeacon,
                    darkBeacon,
                    lightTower.X.ToString(CultureInfo.InvariantCulture),
                    lightTower.Z.ToString(CultureInfo.InvariantCulture),
                    darkTower.X.ToString(CultureInfo.InvariantCulture),
                    darkTower.Z.ToString(CultureInfo.InvariantCulture)),
            }
        }).SafeFireAndForget();
    }
}
