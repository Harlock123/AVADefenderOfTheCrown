namespace AVADefenderOfTheCrown.Engine;

public enum SiegePhase { Aim, Flight, Result, Done }

/// <summary>What the catapult throws: boulders at the wall, Greek fire over it, or a carcass to bring sickness inside.</summary>
public enum Ammo { Boulder, GreekFire, Disease }

/// <summary>Where a shot came down.</summary>
public enum Landing { None, Short, Wall, Battlements, Inside, Over }

/// <summary>
/// The siege of a castle, seen side-on: your catapult on the left, the castle's curtain wall on the right. Aim (the
/// angle) and power (the throw's speed), and loose: a boulder flies in an arc under gravity. On the wall's face it
/// breaks the stones, best low and hard; on the battlements it sweeps defenders off; over the wall it lands among
/// the garrison. Greek fire and disease do little to the stones but much to the men inside. When the wall is
/// breached, or the shots are spent, the army storms the castle; the less wall left, the less it shelters them.
/// </summary>
public sealed class Siege
{
    public const double Gravity = 520;
    public const double LaunchX = 214, LaunchY = 512;
    public const double GroundY = 612, WallX = 742, WallThick = 48, WallTop = 360, Merlon = 24, KeepX = 1000;
    /// <summary>Above this a shot strikes the battlements (and the men on them) rather than the wall's face.</summary>
    public const double BattlementY = WallTop + 10;
    public const double MinAngle = 15, MaxAngle = 75, MinPower = 260, MaxPower = 920;
    public const double AngleSpeed = 30, PowerSpeed = 260;
    public const double ResultSeconds = 1.3, DoneSeconds = 1.6;

    public double Angle = 40, Power = 520;
    public Ammo Ammo;
    public int Shots, GreekFire = 2, Disease = 1;
    /// <summary>How broken the wall is, 0 to 100; where on its face the stones came down (0 its foot, 1 its top).</summary>
    public double Breach, BreachHeight = 0.3;
    public int Hits;
    public SiegePhase Phase = SiegePhase.Aim;
    public double T;
    /// <summary>The shot in the air (or the last one).</summary>
    public double X, Y, Vx, Vy, FlightAngle, FlightPower;
    public Ammo Flying;
    public Landing Last;
    public string Report = "";
    /// <summary>The castle's garrison, and the men the siege has killed.</summary>
    public Troops Garrison;
    public int KilledSoldiers, KilledKnights;
    public int Fires;
    public int Lord;
    public Dice Dice;
    public List<Sfx> Heard = [];

    public Siege(int catapults, Troops garrison, int lord, int seed)
    {
        Shots = Math.Clamp(6 + 2 * (catapults - 1), 6, 12);
        Garrison = garrison;
        Lord = lord;
        Dice = new Dice(seed);
    }

    public Siege Copy()
    {
        var s = (Siege)MemberwiseClone();
        s.Heard = [];
        return s;
    }

    public bool Over => Phase == SiegePhase.Done;
    public bool Breached => Breach >= 100;
    public Troops Left => new(Math.Max(0, Garrison.Soldiers - KilledSoldiers), Math.Max(0, Garrison.Knights - KilledKnights));

    public int AmmoLeft(Ammo a) => a switch { Ammo.GreekFire => Math.Min(GreekFire, Shots), Ammo.Disease => Math.Min(Disease, Shots), _ => Shots };

    public void Select(Ammo a)
    {
        if (Phase == SiegePhase.Aim && AmmoLeft(a) > 0) Ammo = a;
    }

    public void NextAmmo()
    {
        for (int i = 1; i <= 3; i++)
        {
            var a = (Ammo)(((int)Ammo + i) % 3);
            if (AmmoLeft(a) > 0) { Ammo = a; return; }
        }
    }

    /// <summary>Where a shot loosed at <paramref name="angle"/> degrees and <paramref name="power"/> is, <paramref name="t"/> seconds out.</summary>
    public static (double X, double Y) Arc(double angle, double power, double t)
    {
        double a = angle * Math.PI / 180;
        return (LaunchX + power * Math.Cos(a) * t, LaunchY - power * Math.Sin(a) * t + Gravity * t * t / 2);
    }

    /// <summary>The height up the wall's face (0 its foot, 1 the top of the merlons) where a shot reaches it, or null if it falls short.</summary>
    public static double? HeightAtWall(double angle, double power)
    {
        double a = angle * Math.PI / 180, vx = power * Math.Cos(a);
        double t = (WallX - LaunchX) / vx;
        var (_, y) = Arc(angle, power, t);
        // Fell to the ground before the wall?
        double vy = power * Math.Sin(a);
        double tGround = (vy + Math.Sqrt(vy * vy + 2 * Gravity * (GroundY - LaunchY))) / Gravity;
        if (tGround < t) return null;
        return (GroundY - y) / (GroundY - (WallTop - Merlon));
    }

    /// <summary>Where a shot comes down: short, on the wall's face, on the battlements, inside, or over the castle altogether.</summary>
    public static Landing Predict(double angle, double power)
    {
        var h = HeightAtWall(angle, power);
        if (h == null) return Landing.Short;
        double y = GroundY - h.Value * (GroundY - (WallTop - Merlon));
        if (y >= BattlementY) return Landing.Wall;
        if (y >= WallTop - Merlon) return Landing.Battlements;
        // Over the wall: where does it land?
        double a = angle * Math.PI / 180, vy = power * Math.Sin(a);
        double t = (vy + Math.Sqrt(vy * vy + 2 * Gravity * (GroundY - LaunchY))) / Gravity;
        var (x, _) = Arc(angle, power, t);
        return x <= KeepX ? Landing.Inside : Landing.Over;
    }

    /// <summary>How much a boulder at height <paramref name="h"/> up the face and this power breaks the wall: most low and hard.</summary>
    public static double WallDamage(double h, double power) =>
        Math.Max(4, 14 + 22 * (1 - 2 * Math.Abs(h - 0.3))) * Math.Clamp(power / 640, 0.6, 1.3);

    /// <summary>One step: aiming with the keys or the mouse, loosing, the shot in the air, and what it hit.</summary>
    public void Step(double dt, int dx, int dy, bool fire, (double X, double Y)? point = null)
    {
        T += dt;
        switch (Phase)
        {
            case SiegePhase.Aim:
                if (dx != 0 || dy != 0)
                {
                    Angle = Math.Clamp(Angle - dy * AngleSpeed * dt, MinAngle, MaxAngle);
                    Power = Math.Clamp(Power + dx * PowerSpeed * dt, MinPower, MaxPower);
                }
                else if (point is { } p) AimAt(p.X, p.Y);
                if (fire) Loose();
                break;
            case SiegePhase.Flight:
                Fly(dt);
                break;
            case SiegePhase.Result:
                if (T >= ResultSeconds)
                {
                    Phase = Shots <= 0 || Breached ? SiegePhase.Done : SiegePhase.Aim;
                    T = 0;
                    if (AmmoLeft(Ammo) <= 0) Ammo = Ammo.Boulder;
                }
                break;
        }
    }

    /// <summary>The catapult aimed at a point on the screen: the angle along the line to it, and the power by how far it is.</summary>
    public void AimAt(double x, double y)
    {
        double ex = x - LaunchX, ey = LaunchY - y;
        if (ex < 10) ex = 10;
        Angle = Math.Clamp(Math.Atan2(ey, ex) * 180 / Math.PI, MinAngle, MaxAngle);
        Power = Math.Clamp(Math.Sqrt(ex * ex + ey * ey) * 1.25, MinPower, MaxPower);
    }

    public bool Loose()
    {
        if (Phase != SiegePhase.Aim || AmmoLeft(Ammo) <= 0) return false;
        Shots--;
        if (Ammo == Ammo.GreekFire) GreekFire--;
        if (Ammo == Ammo.Disease) Disease--;
        Flying = Ammo;
        FlightAngle = Angle;
        FlightPower = Power;
        double a = Angle * Math.PI / 180;
        (X, Y, Vx, Vy) = (LaunchX, LaunchY, Power * Math.Cos(a), -Power * Math.Sin(a));
        Phase = SiegePhase.Flight;
        T = 0;
        Heard.Add(Sfx.Launch);
        return true;
    }

    /// <summary>Follows the shot (in small steps, so it can't pass through the wall) until it hits something.</summary>
    private void Fly(double dt)
    {
        const int sub = 4;
        for (int i = 0; i < sub && Phase == SiegePhase.Flight; i++)
        {
            double h = dt / sub, px = X;
            X += Vx * h;
            Vy += Gravity * h;
            Y += Vy * h;
            if (px < WallX && X >= WallX && Y >= WallTop - Merlon && Y <= GroundY)
            {
                X = WallX;
                Land(Y < BattlementY ? Landing.Battlements : Landing.Wall, (GroundY - Y) / (GroundY - (WallTop - Merlon)));
            }
            else if (Y >= GroundY)
            {
                Y = GroundY;
                Land(X < WallX ? Landing.Short : X <= KeepX ? Landing.Inside : Landing.Over, 0);
            }
            else if (X > Field.Width + 60) Land(Landing.Over, 0);
        }
    }

    private void Land(Landing where, double height)
    {
        Last = where;
        Phase = SiegePhase.Result;
        T = 0;
        var g = Left;
        switch (where)
        {
            case Landing.Short:
                Report = "SHORT OF THE WALL";
                Heard.Add(Sfx.Thud);
                break;
            case Landing.Over:
                Report = "CLEAN OVER THE CASTLE";
                Heard.Add(Sfx.Thud);
                break;
            case Landing.Wall:
                if (Flying == Ammo.Boulder)
                {
                    double d = WallDamage(height, FlightPower);
                    BreachHeight = Hits == 0 ? height : (BreachHeight * Hits + height) / (Hits + 1);
                    Hits++;
                    Breach = Math.Min(100, Breach + d);
                    Report = Breached ? "THE WALL IS BREACHED!" : d >= 26 ? "A MIGHTY BLOW TO THE WALL" : "THE WALL SHAKES";
                    Heard.Add(Sfx.Crash);
                }
                else
                {
                    Breach = Math.Min(100, Breach + (Flying == Ammo.GreekFire ? 4 : 2));
                    Report = Flying == Ammo.GreekFire ? "THE FIRE BURNS OUT ON THE STONES" : "IT BREAKS ON THE WALL";
                    Heard.Add(Flying == Ammo.GreekFire ? Sfx.Fire : Sfx.Thud);
                }
                break;
            case Landing.Battlements:
            {
                int k = Math.Min(g.Soldiers, 1 + Dice.Next(3));
                KilledSoldiers += k;
                if (Flying == Ammo.Boulder) Breach = Math.Min(100, Breach + 6);
                Report = k > 0 ? $"{k} DEFENDER{(k > 1 ? "S" : "")} SWEPT FROM THE BATTLEMENTS" : "THE MERLONS CRUMBLE";
                Heard.Add(Sfx.Crash);
                break;
            }
            case Landing.Inside:
            {
                int ks = 0, kk = 0;
                switch (Flying)
                {
                    case Ammo.Boulder:
                        ks = Math.Min(g.Soldiers, 1 + Dice.Next(2));
                        Report = $"{ks} DEFENDER{(ks == 1 ? "" : "S")} CRUSHED IN THE BAILEY";
                        Heard.Add(Sfx.Thud);
                        break;
                    case Ammo.GreekFire:
                        ks = Math.Min(g.Soldiers, 2 + (int)Math.Round(g.Soldiers * 0.12));
                        kk = g.Knights > 0 && Dice.Chance(0.5) ? 1 : 0;
                        Fires++;
                        Report = "GREEK FIRE SWEEPS THE BAILEY!";
                        Heard.Add(Sfx.Fire);
                        break;
                    case Ammo.Disease:
                        ks = Math.Min(g.Soldiers, 1 + (int)Math.Round(g.Soldiers * 0.2));
                        kk = (int)Math.Round(g.Knights * 0.2);
                        Report = "SICKNESS SPREADS AMONG THE GARRISON";
                        Heard.Add(Sfx.Thud);
                        break;
                }
                KilledSoldiers += ks;
                KilledKnights += kk;
                break;
            }
        }
    }

    /// <summary>Gives up aiming and storms the castle as it stands.</summary>
    public void Storm()
    {
        if (Phase is SiegePhase.Aim or SiegePhase.Result) { Phase = SiegePhase.Done; T = 0; }
    }

    /// <summary>How much the wall still shelters the garrison in the battle: <see cref="Sim.CastleWalls"/> whole, and even breached its towers still count for something.</summary>
    public double Walls => 1 + (Sim.CastleWalls - 1) * (1 - 0.8 * Breach / 100);
}
