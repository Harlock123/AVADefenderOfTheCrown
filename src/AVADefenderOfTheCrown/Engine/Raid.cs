namespace AVADefenderOfTheCrown.Engine;

/// <summary>The height of a sword stroke or a guard.</summary>
public enum Line { High, Middle, Low }

public enum Act { Guard, Cut, Parry, Wind, Recover, Stagger, Hurt, Down, Walk }

/// <summary>One of the two swordsmen: you, or the guard in front of you.</summary>
public sealed class Swordsman
{
    public double X;
    public Act Act = Act.Guard;
    public double T;
    /// <summary>The line of the stroke under way.</summary>
    public Line Line = Line.Middle;
    /// <summary>The line the blade is held across (a guard's), which a stroke there won't get past.</summary>
    public Line Guard = Line.Middle;
    public int Hp, MaxHp;
    public bool Struck;
    public double NextAttack, NextShift;
    public Swordsman Copy() => (Swordsman)MemberwiseClone();
    public bool Standing => Act != Act.Down;
}

public enum RaidPhase { Enter, Fight, Won, Lost, Done }

/// <summary>
/// A raid on a castle by night: a swordfight down its passages, side-on, against one guard after another. Cut high,
/// thrust or cut low where the guard isn't covering; a cut into his guard is turned aside. When he winds up a blow,
/// parry it as it falls (the better the swordsman, the more time there is): it leaves him off balance, open to
/// anything. Each blow you take costs strength; beat every guard and the castle's treasure (or the lady it holds) is
/// yours; fall and you are taken, and must be ransomed.
/// </summary>
public sealed class Raid
{
    public const double EnterSeconds = 1.6, EndSeconds = 2.4;
    public const double CutSeconds = 0.42, CutLands = 0.2, ParrySeconds = 0.5, WindSeconds = 0.62, RecoverSeconds = 0.35;
    public const double StaggerSeconds = 0.85, HurtSeconds = 0.4, DownSeconds = 1.3;
    public const double Reach = 150, HeroStart = 330, GuardDoor = 1060;

    public Swordsman Hero = new();
    public List<Swordsman> Guards = [];
    public int Current;
    public int Lord;
    public bool ForLady;
    public Rating Skill;
    public RaidPhase Phase = RaidPhase.Enter;
    public double T;
    public Dice Dice;
    public List<Sfx> Heard = [];

    public Raid(Rating swordplay, int lord, bool forLady, int seed)
    {
        Skill = swordplay;
        Lord = lord;
        ForLady = forLady;
        Dice = new Dice(seed);
        Hero = new Swordsman { X = HeroStart, Hp = 3 + (int)swordplay, MaxHp = 3 + (int)swordplay };
        int n = forLady ? 4 : 3;
        for (int i = 0; i < n; i++)
        {
            int hp = i == n - 1 ? 3 : 2;
            Guards.Add(new Swordsman { X = GuardDoor + 40 * i, Hp = hp, MaxHp = hp, Act = Act.Walk, NextAttack = 1.2, NextShift = 1, Guard = (Line)Dice.Next(3) });
        }
    }

    public Raid Copy()
    {
        var r = (Raid)MemberwiseClone();
        r.Hero = Hero.Copy();
        r.Guards = Guards.Select(g => g.Copy()).ToList();
        r.Heard = [];
        return r;
    }

    public Swordsman? Foe => Current < Guards.Count ? Guards[Current] : null;
    public bool Over => Phase == RaidPhase.Done;
    public bool Success => Phase is RaidPhase.Won or RaidPhase.Done && Hero.Hp > 0 && Current >= Guards.Count;

    /// <summary>How long after he starts to parry a falling blow is still turned: longer for a better swordsman.</summary>
    public double ParryWindow => 0.2 + 0.07 * (int)Skill;

    /// <summary>How quickly a guard strikes again: the later ones (and the captain) are quicker.</summary>
    private double Pace => 1 - 0.1 * Current;

    /// <summary>A stroke on a line, if the hero is ready.</summary>
    public bool Attack(Line line)
    {
        if (Phase != RaidPhase.Fight || Hero.Act != Act.Guard) return false;
        Hero.Act = Act.Cut;
        Hero.Line = line;
        Hero.T = 0;
        Hero.Struck = false;
        Heard.Add(Sfx.Swish);
        return true;
    }

    public bool Parry()
    {
        if (Phase != RaidPhase.Fight || Hero.Act != Act.Guard) return false;
        Hero.Act = Act.Parry;
        Hero.T = 0;
        return true;
    }

    /// <summary>One step; <paramref name="attack"/> a stroke asked for this step (if any), <paramref name="parry"/> a parry.</summary>
    public void Step(double dt, Line? attack = null, bool parry = false)
    {
        T += dt;
        switch (Phase)
        {
            case RaidPhase.Enter:
                Walk(dt);
                if (T >= EnterSeconds) { Phase = RaidPhase.Fight; T = 0; }
                return;
            case RaidPhase.Won or RaidPhase.Lost:
                Animate(Hero, dt);
                foreach (var g in Guards) Animate(g, dt);
                if (T >= EndSeconds) { Phase = RaidPhase.Done; T = 0; }
                return;
            case RaidPhase.Done:
                return;
        }
        if (attack is { } a) Attack(a);
        else if (parry) Parry();
        Animate(Hero, dt);
        HeroStroke();
        var foe = Foe;
        if (foe == null) return;
        Walk(dt);
        Animate(foe, dt);
        if (foe.Act == Act.Down && foe.T >= DownSeconds)
        {
            Current++;
            if (Current >= Guards.Count) { Phase = RaidPhase.Won; T = 0; Heard.Add(Sfx.Fanfare); }
            return;
        }
        if (foe.Act is Act.Guard or Act.Walk or Act.Recover)
        {
            foe.NextShift -= dt;
            if (foe.NextShift <= 0)
            {
                foe.Guard = (Line)(((int)foe.Guard + 1 + Dice.Next(2)) % 3);
                foe.NextShift = Dice.Range(0.8, 1.6) * Pace;
            }
            foe.NextAttack -= dt;
        }
        if (foe.Act == Act.Guard && Math.Abs(foe.X - Hero.X) <= Reach + 4)
        {
            if (foe.NextAttack <= 0)
            {
                foe.Act = Act.Wind;
                foe.T = 0;
                foe.Line = (Line)Dice.Next(3);
                foe.Struck = false;
            }
        }
        if (foe.Act == Act.Wind && foe.T >= WindSeconds && !foe.Struck) GuardStroke(foe);
    }

    /// <summary>The hero's stroke lands: past the guard's blade (or while he's off balance or reeling), or into it.</summary>
    private void HeroStroke()
    {
        var h = Hero;
        if (h.Act != Act.Cut || h.Struck || h.T < CutLands) return;
        h.Struck = true;
        var foe = Foe;
        if (foe == null || !foe.Standing || foe.Act == Act.Walk || Math.Abs(foe.X - h.X) > Reach + 20) return;
        bool open = foe.Act is Act.Stagger or Act.Hurt || foe.Guard != h.Line;
        if (open)
        {
            foe.Hp--;
            if (foe.Hp <= 0 || foe.Act != Act.Wind)
            {
                // Struck as he winds up, he takes the wound and his blow still falls.
                foe.Act = foe.Hp <= 0 ? Act.Down : Act.Hurt;
                foe.T = 0;
                foe.X += 34;
            }
            foe.NextAttack = Math.Min(foe.NextAttack, 0.5);
            Heard.Add(foe.Hp <= 0 ? Sfx.Fall : Sfx.Slash);
        }
        else
        {
            Heard.Add(Sfx.Clang);
            foe.NextAttack = Math.Min(foe.NextAttack, 0.3);
        }
        // He learns: the next stroke on that line is likelier to find his blade there.
        if (foe.Standing && Dice.Chance(0.45)) { foe.Guard = h.Line; foe.NextShift = Dice.Range(0.6, 1.2); }
    }

    /// <summary>A guard's blow falls: turned by a parry made in time, or it strikes home.</summary>
    private void GuardStroke(Swordsman foe)
    {
        foe.Struck = true;
        var h = Hero;
        if (h.Act == Act.Parry && h.T <= ParryWindow)
        {
            foe.Act = Act.Stagger;
            foe.T = 0;
            foe.X += 14;
            Heard.Add(Sfx.Clang);
        }
        else
        {
            foe.Act = Act.Recover;
            foe.T = 0;
            h.Hp--;
            h.Act = h.Hp <= 0 ? Act.Down : Act.Hurt;
            h.T = 0;
            h.X = Math.Max(140, h.X - 26);
            Heard.Add(Sfx.Hurt);
            if (h.Hp <= 0) { Phase = RaidPhase.Lost; T = 0; Heard.Add(Sfx.Drum); }
        }
        foe.NextAttack = Dice.Range(1.0, 2.0) * Pace;
    }

    /// <summary>Strokes, parries and staggers run their course and go back to guard.</summary>
    private static void Animate(Swordsman s, double dt)
    {
        s.T += dt;
        double end = s.Act switch
        {
            Act.Cut => CutSeconds, Act.Parry => ParrySeconds, Act.Recover => RecoverSeconds, Act.Stagger => StaggerSeconds,
            Act.Hurt => HurtSeconds, _ => double.MaxValue,
        };
        if (s.T >= end) { s.Act = Act.Guard; s.T = 0; }
    }

    /// <summary>The guard in front walks up to within reach of the hero (the next one comes in from the door).</summary>
    private void Walk(double dt)
    {
        var foe = Foe;
        if (foe == null || !foe.Standing) return;
        double want = Hero.X + Reach;
        if (foe.X > want + 1)
        {
            foe.X = Math.Max(want, foe.X - 170 * dt);
            if (foe.Act == Act.Guard) foe.Act = Act.Walk;
        }
        else if (foe.Act == Act.Walk) { foe.Act = Act.Guard; foe.T = 0; }
    }
}
