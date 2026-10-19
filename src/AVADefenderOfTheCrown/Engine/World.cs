namespace AVADefenderOfTheCrown.Engine;

/// <summary>The fixed screen everything is laid out on, and the length of one step of the game.</summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
}

/// <summary>A small random number generator whose state can be copied (and saved).</summary>
public struct Dice
{
    public ulong State;
    public Dice(int seed) => State = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        State ^= State << 13;
        State ^= State >> 7;
        State ^= State << 17;
        return (State >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);

    public double Range(double lo, double hi) => lo + Next() * (hi - lo);

    public bool Chance(double p) => Next() < p;
}

/// <summary>An army: foot soldiers, mounted knights (each worth three soldiers in a fight) and catapults for sieges.</summary>
public readonly record struct Troops(int Soldiers = 0, int Knights = 0, int Catapults = 0)
{
    public int Strength => Soldiers + 3 * Knights;
    public bool Empty => Soldiers <= 0 && Knights <= 0;
    public static Troops operator +(Troops a, Troops b) => new(a.Soldiers + b.Soldiers, a.Knights + b.Knights, a.Catapults + b.Catapults);
    public static Troops operator -(Troops a, Troops b) => new(Math.Max(0, a.Soldiers - b.Soldiers), Math.Max(0, a.Knights - b.Knights), Math.Max(0, a.Catapults - b.Catapults));
    public override string ToString() => $"{Soldiers} SOLDIERS, {Knights} KNIGHTS" + (Catapults > 0 ? $", {Catapults} CATAPULT{(Catapults > 1 ? "S" : "")}" : "");
}

/// <summary>A lord's fortunes: his treasury, his campaign army and where it stands, and his castle's garrison.</summary>
public sealed class Lord
{
    public int Id;
    public int Gold;
    public Troops Army;
    /// <summary>The shire the campaign army stands in.</summary>
    public int At;
    public Troops Garrison;
    /// <summary>The shire his castle stands in, or -1 once it has fallen.</summary>
    public int Castle;
    public bool Alive => Castle >= 0;
    public LordInfo Info => Realm.Lords[Id];
    public Lord Copy() => (Lord)MemberwiseClone();
}

public enum CardKind { News, War, Loss, Story, Victory, Defeat }

/// <summary>An event card: a turn of the story, shown over the map with the arms of the lord it's about.</summary>
public sealed record Card(string Title, string Text, int Lord = -1, CardKind Kind = CardKind.News, int Shire = -1);

/// <summary>A Norman (or Saxon) army marching on the player: to be fought out on the field when the cards have been read.</summary>
public sealed record Assault(int Lord, Troops Army, int Shire, double Breach);

/// <summary>What became of the Saxon lady: not yet taken, held in a Norman castle, wed to the player, or wed to another.</summary>
public enum LadyState { Free, Held, Married }

/// <summary>
/// Everything about a game that changes, kept together so it can be copied, saved and loaded: the shires and their
/// owners, the lords and their armies, the season, the outlaw's help, the lady, and the cards waiting to be read.
/// </summary>
public sealed class World
{
    public int Player;
    public Lord[] Lords = [];
    /// <summary>Each shire's owner: a lord's number, <see cref="Realm.Nobody"/> or <see cref="Realm.Outlaws"/>.</summary>
    public int[] Owner = new int[Realm.Count];
    public int Turn = 1;
    public int GilCalls = 3;
    /// <summary>Gil's archers ride with the army in its next battle.</summary>
    public bool GilAid;
    public LadyState Lady;
    public int LadyHeldBy = -1;
    public int LadyTurn = 4;
    public int Renown;
    public int NormansBeaten;
    public int Tournaments, Raids;
    public bool Won, Lost;
    public Dice Dice = new(1);
    public List<Card> Cards = [];
    public Assault? Assault;
    public List<Sfx> Heard = [];

    public Lord Me => Lords[Player];
    public int Year => Realm.FirstYear + (Turn - 1) / 4;
    public string Season => Realm.Seasons[(Turn - 1) % 4];
    public string Date => $"{Season} {Year}";

    public IEnumerable<int> Lands(int lord)
    {
        for (int i = 0; i < Owner.Length; i++)
            if (Owner[i] == lord) yield return i;
    }

    public int LandCount(int lord) => Owner.Count(o => o == lord);

    public bool IsCastle(int shire) => Lords.Any(l => l.Castle == shire);

    public Lord? CastleOf(int shire) => Lords.FirstOrDefault(l => l.Castle == shire);

    public Lord? ArmyIn(int shire, int except = -9) => Lords.FirstOrDefault(l => l.Alive && l.Id != except && l.At == shire && !l.Army.Empty);

    public World Copy() => new()
    {
        Player = Player, Lords = Lords.Select(l => l.Copy()).ToArray(), Owner = (int[])Owner.Clone(), Turn = Turn, GilCalls = GilCalls,
        GilAid = GilAid, Lady = Lady, LadyHeldBy = LadyHeldBy, LadyTurn = LadyTurn, Renown = Renown, NormansBeaten = NormansBeaten,
        Tournaments = Tournaments, Raids = Raids, Won = Won, Lost = Lost, Dice = Dice, Cards = [.. Cards], Assault = Assault,
    };
}
