namespace AVADefenderOfTheCrown.Engine;

/// <summary>
/// The four orders an army can be given each round. Each beats one other: a fierce attack overruns catapults at their
/// work, a bombardment wears down an army standing fast, and an army standing fast breaks a fierce attack on its
/// line. Retreat leaves the field, taking a parting blow.
/// </summary>
public enum Tactic { StandFast, Bombard, FierceAttack, Retreat }

public enum BattlePhase { Choose, Clash, Done }

/// <summary>
/// A battle between two armies, fought in rounds. Each round both sides choose a tactic; the clash is shown for a
/// moment, and each side loses men by the other's strength, its leader, the castle walls (for a defender behind
/// them) and how the two tactics meet. It ends when a side is gone or leaves the field, or after
/// <see cref="MaxRounds"/> rounds, when the attackers give up.
/// </summary>
public sealed class Battle
{
    public const int MaxRounds = 8;
    public const double ClashSeconds = 2.4, DoneSeconds = 2.2;
    /// <summary>The share of an army's strength it strikes the other with in an ordinary round.</summary>
    public const double Bite = 0.2;

    /// <summary>Soldiers and knights as fractions, so small losses add up; catapults don't fall.</summary>
    public double[] Soldiers = new double[2], Knights = new double[2];
    public int[] Catapults = new int[2];
    public int[] Lord = [-1, -1];
    public double[] Lead = [1, 1];
    /// <summary>The defender's walls (side 1): 1 in the open field, up to <see cref="Sim.CastleWalls"/> behind an unbroken wall.</summary>
    public double Walls = 1;
    /// <summary>Which side the player commands (0 attacking, 1 defending), or -1 for a battle the computer fights out.</summary>
    public int Mine = 0;
    public bool Archers;
    public int Shire = -1;
    public int Round;
    public BattlePhase Phase = BattlePhase.Choose;
    public double T;
    public Tactic[] Last = [Tactic.StandFast, Tactic.StandFast];
    public double[] Fell = new double[2];
    /// <summary>The winning side (0 or 1) once it's done.</summary>
    public int Winner = -1;
    public string Report = "";
    public Dice Dice;
    public List<Sfx> Heard = [];

    public Battle(Troops attacker, int attackerLord, Troops defender, int defenderLord, double walls, int mine, int seed, int shire = -1)
    {
        Soldiers = [attacker.Soldiers, defender.Soldiers];
        Knights = [attacker.Knights, defender.Knights];
        Catapults = [attacker.Catapults, defender.Catapults];
        Lord = [attackerLord, defenderLord];
        Lead = [LeadOf(attackerLord), LeadOf(defenderLord)];
        Walls = walls;
        Mine = mine;
        Shire = shire;
        Dice = new Dice(seed);
    }

    private static double LeadOf(int lord) => lord >= 0 ? Realm.Lead(Realm.Lords[lord].Leadership) : 0.9;

    public Battle Copy()
    {
        var b = (Battle)MemberwiseClone();
        b.Soldiers = (double[])Soldiers.Clone();
        b.Knights = (double[])Knights.Clone();
        b.Catapults = (int[])Catapults.Clone();
        b.Lord = (int[])Lord.Clone();
        b.Lead = (double[])Lead.Clone();
        b.Last = (Tactic[])Last.Clone();
        b.Fell = (double[])Fell.Clone();
        b.Heard = [];
        return b;
    }

    public double Strength(int side) => Soldiers[side] + 3 * Knights[side];

    /// <summary>What's left of a side, rounding the last wounded man up.</summary>
    public Troops Left(int side) => new((int)Math.Ceiling(Soldiers[side] - 0.25), (int)Math.Ceiling(Knights[side] - 0.25), Catapults[side]);

    public bool CanBombard(int side) => Catapults[side] > 0;

    public bool Over => Phase == BattlePhase.Done;

    /// <summary>How hard an army fighting with tactic <paramref name="a"/> strikes one fighting with <paramref name="b"/>.</summary>
    public static double Meet(Tactic a, Tactic b) => (a, b) switch
    {
        (Tactic.Retreat, _) => 0,
        (_, Tactic.Retreat) => 0.5,
        (Tactic.FierceAttack, Tactic.FierceAttack) => 1.3,
        (Tactic.FierceAttack, Tactic.StandFast) => 0.6,
        (Tactic.FierceAttack, Tactic.Bombard) => 1.7,
        (Tactic.StandFast, Tactic.FierceAttack) => 1.3,
        (Tactic.StandFast, Tactic.StandFast) => 0.5,
        (Tactic.StandFast, Tactic.Bombard) => 0.4,
        (Tactic.Bombard, Tactic.FierceAttack) => 0.35,
        (Tactic.Bombard, Tactic.StandFast) => 1.4,
        (Tactic.Bombard, Tactic.Bombard) => 0.8,
        _ => 1,
    };

    /// <summary>The computer's choice for a side: bold when it's stronger, steady when it isn't, and off the field when it's beaten.</summary>
    public Tactic Choose(int side)
    {
        double mine = Strength(side) * Lead[side] * (side == 1 ? Walls : 1), theirs = Strength(1 - side) * Lead[1 - side] * (side == 0 ? Walls : 1);
        if (side == 0 && mine < theirs * 0.45 && Round >= 2) return Tactic.Retreat;
        double r = Dice.Next();
        if (CanBombard(side) && r < 0.3) return Tactic.Bombard;
        if (mine > theirs * 1.2) return r < 0.75 ? Tactic.FierceAttack : Tactic.StandFast;
        return r < 0.4 ? Tactic.FierceAttack : Tactic.StandFast;
    }

    /// <summary>One round with both sides' tactics: the losses on each side and whether the battle is over.</summary>
    public void Fight(Tactic a, Tactic d)
    {
        if (Over) return;
        if (a == Tactic.Bombard && !CanBombard(0)) a = Tactic.StandFast;
        if (d == Tactic.Bombard && !CanBombard(1)) d = Tactic.StandFast;
        Round++;
        Last = [a, d];
        double sa = Strength(0), sd = Strength(1);
        double toD = sa * Bite * Lead[0] * Meet(a, d) / (a == Tactic.Retreat ? 1 : Walls);
        double toA = sd * Bite * Lead[1] * Meet(d, a) * (d == Tactic.Retreat ? 1 : Math.Sqrt(Walls));
        if (a == Tactic.Bombard) toD += Catapults[0] * 2.5 * Math.Sqrt(Walls);
        if (d == Tactic.Bombard) toA += Catapults[1] * 2.5;
        if (Archers && Mine >= 0 && a != Tactic.Retreat && d != Tactic.Retreat)
        {
            if (Mine == 0) toD += 4 + sa * 0.05; else toA += 4 + sd * 0.05;
        }
        toD *= Dice.Range(0.85, 1.15);
        toA *= Dice.Range(0.85, 1.15);
        Fell = [Lose(0, toA), Lose(1, toD)];
        Heard.Add(Sfx.Din);
        if (a == Tactic.Retreat || d == Tactic.Retreat)
        {
            Winner = a == Tactic.Retreat ? 1 : 0;
            Report = a == Tactic.Retreat ? "THE ATTACKERS FALL BACK" : "THE DEFENDERS FLEE THE FIELD";
        }
        else if (Strength(1) < 0.5) { Winner = 0; Report = "THE DEFENDERS ARE ROUTED"; }
        else if (Strength(0) < 0.5) { Winner = 1; Report = "THE ATTACKERS ARE CUT DOWN"; }
        else if (Round >= MaxRounds) { Winner = 1; Report = "THE ATTACKERS GIVE UP THE FIELD"; }
    }

    /// <summary>Takes a blow of <paramref name="damage"/> strength off a side: seven parts in ten from the soldiers, the rest from the knights.</summary>
    private double Lose(int side, double damage)
    {
        double before = Strength(side);
        double fromSoldiers = Knights[side] > 0 ? damage * 0.7 : damage;
        double s = Math.Min(Soldiers[side], fromSoldiers);
        Soldiers[side] -= s;
        double rest = damage - s;
        Knights[side] = Math.Max(0, Knights[side] - rest / 3);
        if (Strength(side) < 0.5) Soldiers[side] = Knights[side] = 0;
        return before - Strength(side);
    }

    /// <summary>The player's order for the round: the other side answers, and the clash is shown.</summary>
    public bool Order(Tactic t)
    {
        if (Phase != BattlePhase.Choose || Mine < 0) return false;
        if (t == Tactic.Bombard && !CanBombard(Mine)) return false;
        var foe = Choose(1 - Mine);
        if (Mine == 0) Fight(t, foe); else Fight(foe, t);
        Phase = BattlePhase.Clash;
        T = 0;
        return true;
    }

    /// <summary>The clash plays out, then the next round's orders are asked for, or the battle ends.</summary>
    public void Step(double dt)
    {
        T += dt;
        switch (Phase)
        {
            case BattlePhase.Clash when T >= ClashSeconds:
                Phase = Winner >= 0 ? BattlePhase.Done : BattlePhase.Choose;
                T = 0;
                if (Winner >= 0) Heard.Add(Winner == Mine ? Sfx.Fanfare : Sfx.Drum);
                break;
        }
    }

    public bool Finished => Phase == BattlePhase.Done && T >= DoneSeconds;

    /// <summary>Fights the whole battle out with the computer choosing for both sides.</summary>
    public int Resolve()
    {
        Mine = -1;
        while (Winner < 0) Fight(Choose(0), Choose(1));
        Phase = BattlePhase.Done;
        return Winner;
    }
}
