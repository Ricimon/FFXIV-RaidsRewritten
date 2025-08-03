using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECommons;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class LastWish : Mechanic
{
    /*
    private float OMEN_VISIBLE_SECONDS = 0.0f;
    private float STATUS_DELAY_SECONDS = 0.0f;
    private float DELAY_SECONDS = 0.0f;

    private Query<Player.Component> playerQuery;

    private List<(HitZone, HitZone)>? SolutionTable;
    private List<(int, int)>? Pairs;
    private List<(HitZone, HitZone)>? TrueSolutionZones;

    public enum HitZone
    {
        In,
        Out,
        North,
        East,
        South,
        West
    }
    public enum ZoneType
    {
        Rect,
        Donut,
        Circle
    }
    public enum Phase
    {
        Start,
        Idle,
        Telegraph,
        Resolution
    }

    public readonly DalamudServices dalamud = dalamud;
    public readonly ILogger logger = logger;

    private static readonly Dictionary<HitZone, HitZone> Opposites = new()
    {
        { HitZone.In, HitZone.Out },
        { HitZone.Out, HitZone.In },
        { HitZone.North, HitZone.South },
        { HitZone.South, HitZone.North },
        { HitZone.East, HitZone.West },
        { HitZone.West, HitZone.East },
    };
    private struct ZoneData
    {
        public Vector3 TelegraphScale;
        public string OmenPath;
        public string PredictOmenPath;
        public string VfxPath;
        public Vector3 Position;
        public float VfxDelaySeconds;
        public ZoneType ZoneType;
        public float Rotation;
    }
    private static readonly Dictionary<HitZone, ZoneData> ZoneDict = new Dictionary<HitZone, ZoneData>
    {
        {
            HitZone.In, new ZoneData
            {
                TelegraphScale = new Vector3(0, 0, 0),
                Position = new Vector3(0, 0, 0),
                OmenPath = "",
                PredictOmenPath = "",
                VfxPath = "",
                VfxDelaySeconds = 0.0f,
                ZoneType = ZoneType.Circle,
            }
        },
        {
            HitZone.Out, new ZoneData
            {
                ZoneType = ZoneType.Donut,
            }
        },
        {
            HitZone.North, new ZoneData
            {
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.East, new ZoneData
            {
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.South, new ZoneData
            {
                ZoneType = ZoneType.Rect,
            }
        },
        {
            HitZone.West, new ZoneData
            {
                ZoneType = ZoneType.Rect,
            }
        },
    };
    private static List<string> SymbolPaths = new List<string> {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        ""
    };
    public record struct ShowOmen(Entity Omen);




    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }
    public void Dispose()
    {
        this.playerQuery.Dispose();
    }
    #region Algorithm
    private void GenerateTable(int Players, int Sequences)
    {
        if (Players < Enum.GetValues<HitZone>().Length) { return; } //unsolveable random distribution
        var zones = Enum.GetValues<HitZone>();
        SolutionTable = new HitZone[Players, Sequences];

        for (int col = 0; col < Sequences; col++)
        {
            var availableRows = new List<int>();
            for (int r = 0; r < Players; r++)
            {
                availableRows.Add(r);
            }
            var shuffledZones = new List<HitZone>(zones);
            Shuffle(random, shuffledZones);

            for (int i = 0; i < zones.Length; i++)
            {
                int rowIndex = availableRows[random.Next(availableRows.Count)];
                availableRows.Remove(rowIndex);
                SolutionTable[rowIndex, col] = shuffledZones[i];
            }

            foreach (var row in availableRows)
            {
                var randomZone = zones[random.Next(zones.Length)];
                SolutionTable[row, col] = randomZone;
            }
        }
    }

    private void GenerateSolution()
    {
        if (SolutionTable == null) { return; }
        if (SolutionTable.GetLength(0) == 0) { return; }
        int cols = SolutionTable.GetLength(1);
        int rows = SolutionTable.GetLength(0);

        TrueSolutionZones = new List<(HitZone, HitZone)>();
        Pairs = new List<(int, int)>();

        HitZone? lastInDirection = null;

        for (int col = 0; col < cols; col++)
        {
            var validPairs = new List<((HitZone, HitZone) pair, (int, int) indices)>();

            var allRowPairs = new List<(int, int)>();
            for (int i = 0; i < rows; i++)
                for (int j = i + 1; j < rows; j++)
                    allRowPairs.Add((i, j));

            Shuffle(random, allRowPairs);

            foreach (var (i, j) in allRowPairs)
            {
                var a = SolutionTable[i, col];
                var b = SolutionTable[j, col];

                if (a == b || IsOpposite(a, b)) continue;

                if (ContainsInWithDirection(a, b, out var currentInDirection))
                {
                    if (lastInDirection.HasValue && IsOpposite(currentInDirection, lastInDirection.Value))
                        continue;
                }

                validPairs.Add(((a, b), (i, j)));
            }



            var (selectedPair, selectedIndices) = validPairs[random.Next(validPairs.Count)];
            TrueSolutionZones.Add(selectedPair);
            Pairs.Add(selectedIndices);

            if (ContainsInWithDirection(selectedPair.Item1, selectedPair.Item2, out var newInDir))
                lastInDirection = newInDir;
        }
    }

    private static bool IsOpposite(HitZone a, HitZone b)
    {
        return Opposites.TryGetValue(a, out var oppA) && oppA == b;
    }

    private static bool ContainsInWithDirection(HitZone a, HitZone b, out HitZone direction)
    {
        direction = default;

        if (a == HitZone.In && IsCardinal(b))
        {
            direction = b;
            return true;
        }
        if (b == HitZone.In && IsCardinal(a))
        {
            direction = a;
            return true;
        }

        return false;
    }

    private static bool IsCardinal(HitZone a)
    {
        return a == HitZone.North || a == HitZone.East || a == HitZone.South || a == HitZone.West;
    }

    private static void Shuffle<T>(Random rand, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    #endregion

    private void DebugOutput()
    {
        var str = "";
        int rows = SolutionTable.GetLength(0);
        int cols = SolutionTable.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                str += $"{SolutionTable[r, c],6} ";
            }
            logger.Info(str);
            str = "";
        }


        logger.Info("Player Solution");
        Pairs.Each(a =>
        {
            logger.Info($"{a.ToString()}");
        });

        logger.Info("Zone Solution");
        TrueSolutionZones.Each(a =>
        {
            logger.Info($"{a.ToString()}");
        });
    }

    public void TriggerPhase(Entity playerEntity, Phase phase)
    {
        var world = playerEntity.CsWorld();
        using Query<LastWish.Component> q = playerEntity.CsWorld().QueryBuilder<LastWish.Component>().With(Ecs.ChildOf, playerEntity).Build();
        if (!q.IsTrue()) { return; }
        Entity le = default;
        q.Each((Entity e, ref LastWish.Component lw) => {
            lw.Phase = phase;
        });
    }
    //*/
    public int RngSeed { get; set; }
    private readonly List<Entity> attacks = [];

    private const uint PHEONIX_HOOK = 9970;
    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
    }
    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Target == null) { return; }
        if (set.Action.Value.RowId != PHEONIX_HOOK) { return; }

        var localPlayer = Dalamud.ClientState.LocalPlayer;
        if (localPlayer == null) { return; }
    }
}
