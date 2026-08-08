namespace RaidsRewritten.Network;

public enum NetworkMechanic : uint
{
    None = 0,
    Spread = 1,
    Enumeration = 10,
    ExplosiveTrap = 20,
    // TEA - to server
    TeaFireTornado1 = 1000,
    TeaHawkBlasterTower = 1010,
    TeaBlasstyChargeHit = 1011,
    TeaLimitCutEnd = 1012,
    TeaSpawnShanoa = 1020,
    // TEA - to client
}
