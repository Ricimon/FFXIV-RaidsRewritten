using Flecs.NET.Core;

namespace RaidsRewritten.Tests;

[TestFixture]
public class FlecsTests
{
    private record struct Comp1(int Value);
    private record struct Comp2(int Value);
    private struct Tag1;
    private struct Tag2;

    private World World { get; set; }
    private World Stage => World.GetStage(0);

    [SetUp]
    public void Setup()
    {
        World = World.Create();
    }

    [TearDown]
    public void Teardown()
    {
        World.Dispose();
    }

    [Test]
    public void ObserverQuirk()
    {
        // A system needs to exist for World.Progress to do anything
        World.System().Each(() => { });

        var fireCount = 0;
        // This observer specifically needs Observer<Comp2>().With<Tag1>()
        World.Observer<Comp2>()
            .With<Tag1>()
            .Event(Ecs.OnSet)
            .Each((ref Comp2 _) => { fireCount++; });

        Stage.Entity().Set(new Comp2()).Add<Tag1>();

        // This is necessary for the stage to execute
        World.Progress();

        // BUG/QUIRK, this really should be 1
        Assert.That(fireCount, Is.EqualTo(2));
    }

    [Test]
    public void ObserverQuirkComplex()
    {
        // A system needs to exist for World.Progress to do anything
        World.System().Each(() => { });

        var fireCount = 0;
        // This observer specifically needs Observer<Comp2>().With<Tag1>()
        World.Observer<Comp2>()
            .With<Tag1>()
            .Event(Ecs.OnSet)
            .Each((ref Comp2 _) => { fireCount++; });

        var e1 = World.Entity().Set(new Comp1());

        // e1 here is a staged entity
        e1 = World.GetStage(0).Query<Comp1>().First();
        Assert.That(e1.IsValid());

        // This operation is staged as e1's CsWorld() is a stage
        // Same thing will happen if the call is Stage.Entity()
        var e2 = e1.CsWorld().Entity().Set(new Comp2());
        e2.Add<Tag1>();

        // This is necessary for the stage to execute
        World.Progress();

        // BUG/QUIRK, this really should be 1
        Assert.That(fireCount, Is.EqualTo(2));
    }

    [Test]
    public void ObserverQuirkFollowup()
    {
        // A system needs to exist for World.Progress to do anything
        World.System().Each(() => { });

        var fireCount = 0;
        // This observer specifically needs the With<Tag1>() property
        World.Observer<Comp2>()
            .With<Tag1>()
            .Event(Ecs.OnSet)
            .Each((ref Comp2 _) => { fireCount++; });

        var e1 = World.Entity().Set(new Comp1());

        // e1 here is a staged entity
        e1 = World.GetStage(0).Query<Comp1>().First();
        Assert.That(e1.IsValid());

        // This is to showcase the pitfall of a potential workaround
        World.System().Each(() =>
        {
            // Calling it like this will CRASH (immediate operations cannot be done in an non-immediate system)
            //var e3 = e1.CsWorld().GetWorld().Entity().Set(new Comp2());
            var e3 = e1.CsWorld().Entity().Set(new Comp2());
            e3.Add<Tag1>();
        });
        World.Progress();

        // BUG/QUIRK, this really should be 1
        Assert.That(fireCount, Is.EqualTo(2));
    }

}
