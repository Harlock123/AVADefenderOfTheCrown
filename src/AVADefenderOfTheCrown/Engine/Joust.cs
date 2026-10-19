namespace AVADefenderOfTheCrown.Engine;

public enum JoustPhase { Ready, Charge, Strike, Done }

/// <summary>Where a lance meets the other knight: nowhere, on the shield (the lance shatters), or square on the boss (he's unhorsed).</summary>
public enum Strike { Miss, Shatter, Unhorse }

/// <summary>
/// A joust, seen down the lists from the saddle. The other knight gallops in from the far end with his shield swaying
/// as he rides; you steer your lance point (which bobs with your horse's stride, less for a better jouster) so it's on
/// his shield when the horses meet. A lance square on the boss unhorses him; on the shield it shatters, a point; off
/// it, nothing. He aims at you too. Three passes at most: the first knight unhorsed loses; after three, the points.
/// <para>Positions are in screen pixels at the moment the horses meet, where the shield's middle sways round
/// (<see cref="CentreX"/>, <see cref="CentreY"/>). While the knight is far off, both his shield and your lance point are
/// drawn shrunk towards the far end of the lists.</para>
/// </summary>
public sealed class Joust
{
    public const double ReadySeconds = 1.4, ChargeSeconds = 3.6, StrikeSeconds = 2.4;
    public const double CentreX = 512, CentreY = 400;
    public const double ShieldRadius = 56, BossRadius = 19;
    public const double KeySpeed = 320, MouseSpeed = 950;
    public const int Passes = 3;
    /// <summary>The far end of the lists, where the knight starts as a speck.</summary>
    public const double VanishX = 512, VanishY = 268;

    public int Foe;
    public Rating Skill, FoeSkill;
    public int Pass = 1, Points, FoePoints;
    public JoustPhase Phase = JoustPhase.Ready;
    public double T;
    public double AimX = CentreX, AimY = CentreY + 140;
    public Strike Mine, His;
    /// <summary>+1 won, -1 lost, 0 a draw (once done).</summary>
    public int Result;
    public bool BothDown;
    public double[] Sway = new double[4];
    public Dice Dice;
    public List<Sfx> Heard = [];

    public Joust(Rating skill, int foe, int seed)
    {
        Skill = skill;
        Foe = foe;
        FoeSkill = Realm.Lords[foe].Jousting;
        Dice = new Dice(seed);
        NewSway();
    }

    public Joust Copy()
    {
        var j = (Joust)MemberwiseClone();
        j.Sway = (double[])Sway.Clone();
        j.Heard = [];
        return j;
    }

    private void NewSway()
    {
        for (int i = 0; i < Sway.Length; i++) Sway[i] = Dice.Range(0, Math.Tau);
    }

    /// <summary>How far through the charge (0 at the far end, 1 when the horses meet).</summary>
    public double Progress => Phase switch { JoustPhase.Ready => 0, JoustPhase.Charge => Math.Clamp(T / ChargeSeconds, 0, 1), _ => 1 };

    /// <summary>The other knight's shield's middle at time <paramref name="t"/> into the charge.</summary>
    public (double X, double Y) ShieldAt(double t)
    {
        double sx = 26 + 9 * (int)FoeSkill;
        return (CentreX + sx * Math.Sin(1.15 * t + Sway[0]) + 0.35 * sx * Math.Sin(2.9 * t + Sway[1]),
                CentreY + 20 * Math.Sin(1.7 * t + Sway[2]));
    }

    /// <summary>How far the lance point bobs with the horse's stride: less for a better jouster.</summary>
    public double BobSize => 10 + 10 * (3 - (int)Skill);

    /// <summary>The lance point at time <paramref name="t"/>: where it's aimed, bobbing with the stride.</summary>
    public (double X, double Y) TipAt(double t) =>
        (AimX + BobSize * 0.35 * Math.Sin(2 * Math.PI * 1.2 * t + Sway[3]), AimY + BobSize * Math.Sin(2 * Math.PI * 2.4 * t));

    public (double X, double Y) Shield => ShieldAt(Phase == JoustPhase.Charge ? T : ChargeSeconds);
    public (double X, double Y) Tip => TipAt(Phase == JoustPhase.Charge ? T : ChargeSeconds);

    /// <summary>Where a lance point lands, by how far it is from the shield's middle.</summary>
    public static Strike Classify(double dx, double dy)
    {
        double d = Math.Sqrt(dx * dx + dy * dy);
        return d <= BossRadius ? Strike.Unhorse : d <= ShieldRadius ? Strike.Shatter : Strike.Miss;
    }

    /// <summary>The other knight's lance: better jousters strike true more often, and a better rider keeps his seat.</summary>
    public Strike HisStrike(double roll)
    {
        double unhorse = (0.05 + 0.05 * (int)FoeSkill) * (1.3 - 0.15 * (int)Skill);
        double shatter = 0.38 + 0.06 * (int)FoeSkill;
        return roll < unhorse ? Strike.Unhorse : roll < unhorse + shatter ? Strike.Shatter : Strike.Miss;
    }

    public bool Over => Phase == JoustPhase.Done;

    /// <summary>One step: the lance steered by the keys (<paramref name="dx"/>, <paramref name="dy"/>) or towards the mouse.</summary>
    public void Step(double dt, int dx, int dy, (double X, double Y)? point = null)
    {
        T += dt;
        switch (Phase)
        {
            case JoustPhase.Ready:
                Steer(dt, dx, dy, point);
                if (T >= ReadySeconds) { Phase = JoustPhase.Charge; T = 0; Heard.Add(Sfx.Hooves); }
                break;
            case JoustPhase.Charge:
                Steer(dt, dx, dy, point);
                if (T >= ChargeSeconds)
                {
                    T = ChargeSeconds;
                    Meet();
                }
                break;
            case JoustPhase.Strike:
                if (T >= StrikeSeconds) NextPass();
                break;
        }
    }

    private void Steer(double dt, int dx, int dy, (double X, double Y)? point)
    {
        if (dx != 0 || dy != 0)
        {
            AimX += dx * KeySpeed * dt;
            AimY += dy * KeySpeed * dt;
        }
        else if (point is { } p)
        {
            double ex = p.X - AimX, ey = p.Y - AimY, d = Math.Sqrt(ex * ex + ey * ey), max = MouseSpeed * dt;
            if (d > max) { ex *= max / d; ey *= max / d; }
            AimX += ex;
            AimY += ey;
        }
        AimX = Math.Clamp(AimX, CentreX - 280, CentreX + 280);
        AimY = Math.Clamp(AimY, CentreY - 220, CentreY + 220);
    }

    /// <summary>The horses meet: where both lances land.</summary>
    private void Meet()
    {
        var (sx, sy) = ShieldAt(ChargeSeconds);
        var (tx, ty) = TipAt(ChargeSeconds);
        Settle(Classify(tx - sx, ty - sy), HisStrike(Dice.Next()));
    }

    /// <summary>The pass's outcome, once both lances have landed (for tests too).</summary>
    public void Settle(Strike mine, Strike his)
    {
        Mine = mine;
        His = his;
        Phase = JoustPhase.Strike;
        T = 0;
        BothDown = mine == Strike.Unhorse && his == Strike.Unhorse;
        if (!BothDown)
        {
            if (mine == Strike.Shatter) Points++;
            if (his == Strike.Shatter) FoePoints++;
        }
        Heard.Add(mine switch { Strike.Unhorse => Sfx.LanceHit, Strike.Shatter => Sfx.Shatter, _ => Sfx.Whoosh });
        if (his == Strike.Unhorse || mine == Strike.Unhorse) Heard.Add(Sfx.Fall);
        if (mine == Strike.Unhorse && !BothDown) Heard.Add(Sfx.Cheer);
    }

    private void NextPass()
    {
        if (!BothDown && Mine == Strike.Unhorse) { Finish(1); return; }
        if (!BothDown && His == Strike.Unhorse) { Finish(-1); return; }
        if (Pass >= Passes && !BothDown) { Finish(Math.Sign(Points - FoePoints)); return; }
        if (!BothDown) Pass++;
        Phase = JoustPhase.Ready;
        T = 0;
        AimX = CentreX;
        AimY = CentreY + 140;
        NewSway();
    }

    private void Finish(int result)
    {
        Result = result;
        Phase = JoustPhase.Done;
        T = 0;
    }
}
