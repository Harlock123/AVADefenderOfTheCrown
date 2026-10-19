namespace AVADefenderOfTheCrown.Engine;

public enum GameState
{
    /// <summary>The title, with its tune.</summary>
    Title,
    /// <summary>The demonstration: the computer plays a game of its own.</summary>
    Attract,
    /// <summary>Choosing which Saxon knight to be.</summary>
    Choose,
    Playing,
    Paused,
    /// <summary>The story's end has been told: a moment, then the initials or the table.</summary>
    GameOver,
    EnterInitials,
    HighScores,
}

/// <summary>What fills the screen while a game is played.</summary>
public enum Scene { Map, Card, Joust, Siege, Raid, Battle }

/// <summary>What the map is waiting for: an order, a shire or castle to be picked, or one of the order panels.</summary>
public enum MapMode { Orders, Conquer, Raid, Tournament, Buy, Garrison, Gil, GilRaid }

/// <summary>The orders on the map's menu.</summary>
public enum Order { Tournament, Conquer, Raid, Buy, Garrison, Gil, Pass }

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Click, Refused, Coins, Drum, Fanfare, Wedding,
    Hooves, LanceHit, Shatter, Whoosh, Fall, Cheer,
    Swish, Clang, Slash, Hurt,
    Launch, Crash, Thud, Fire,
    Din, Horn,
    TitleTune, Victory, Defeat, MusicStop,
}

/// <summary>
/// The controls in a step: a direction (up is -1, as on the screen), the fire and second buttons, back, a number key,
/// where the mouse points (in screen pixels) and the storm key for a siege.
/// </summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Fire = false, bool Alt = false, bool Back = false, int Pick = 0,
    (double X, double Y)? Point = null, bool Storm = false);

/// <summary>
/// The whole game with no UI: the title, the choice of knight, the map and its orders, the scenes (the joust, the
/// siege, the raid, the battle), the cards that tell the story, saving, the end and the high scores. Call
/// <see cref="Update"/> with the elapsed time and the controls; it runs in fixed 60 Hz steps, so it plays the same at
/// any frame rate. The mouse comes in through <see cref="ClickShire"/>, <see cref="DoOrder"/> and the rest.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 18;
    public const double AttractSeconds = 110;
    public const double HighScoreSeconds = 9, OverSeconds = 3;
    /// <summary>In the demonstration, a card stays up this long.</summary>
    public const double AttractCardSeconds = 2.2;
    public const double MenuRepeat = 0.22;

    private double _accumulator, _idle, _repeat;
    private bool _startPrev, _tunePending = true;
    private Controls _prev;
    private int _seed;
    private Defence _defence;
    private int _target = -1;
    private bool _assault, _forLand;

    public GameEngine(HighScoreTable? table = null, string? saveFolder = null, int seed = 0)
    {
        HighScores = table ?? HighScoreTable.Load();
        SaveFolder = saveFolder ?? HighScores.Folder;
        _seed = seed != 0 ? seed : (int)(DateTime.Now.Ticks % 1_000_000) + 1;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Scene Scene { get; private set; } = Scene.Map;
    public MapMode Mode { get; private set; } = MapMode.Orders;
    public double StateTime { get; private set; }
    public double SceneTime { get; private set; }
    public bool Silent { get; set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public string SaveFolder { get; }
    public World World { get; private set; } = new();
    public Pilot Pilot { get; } = new();
    public Joust? Joust { get; private set; }
    public Siege? Siege { get; private set; }
    public Raid? Raid { get; private set; }
    public Battle? Battle { get; private set; }
    public int Score => Sim.Score(World);

    /// <summary>The knight highlighted on the choice screen.</summary>
    public int ChooseIndex { get; set; }
    /// <summary>The order highlighted on the map's menu.</summary>
    public int MenuIndex { get; set; }
    /// <summary>The shire or castle picked out with the keys while choosing where to march or raid.</summary>
    public int PickIndex { get; private set; }
    /// <summary>The shire the mouse is over (or last clicked), for its details.</summary>
    public int Selected { get; set; } = -1;
    /// <summary>The row highlighted in a panel, and the tactic highlighted in a battle.</summary>
    public int Row { get; set; }
    public int TacticIndex { get; set; } = 2;

    // The buy and garrison panels' numbers.
    public int BuySoldiers { get; set; }
    public int BuyKnights { get; set; }
    public int BuyCatapults { get; set; }
    public bool BuyToGarrison { get; set; }
    public int MoveSoldiers { get; set; }
    public int MoveKnights { get; set; }
    public bool MoveToGarrison { get; set; }
    public bool TournamentForLand { get; set; } = true;
    public bool GilForRaid { get; set; } = true;

    /// <summary>The line along the bottom of the map: a word about the last thing tried.</summary>
    public string? Message { get; private set; }
    public double MessageTime { get; private set; }

    /// <summary>The card on show (the first one waiting).</summary>
    public Card? Card => Scene == Scene.Card && World.Cards.Count > 0 ? World.Cards[0] : null;

    public bool HasSave => SaveFile.Exists(SaveFolder);

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    private void Say(string text, double seconds = 3.5)
    {
        Message = text;
        MessageTime = seconds;
    }

    private bool Demo => State == GameState.Attract;

    // ───────────────────────────── time ─────────────────────────────

    public void Update(double dt, Controls c, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(c, start);
        }
    }

    public void Tick(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        if (MessageTime > 0) { MessageTime -= dt; if (MessageTime <= 0) Message = null; }
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                _idle += dt;
                if (startPressed || Pressed(c.Fire, _prev.Fire)) NewGame();
                else if (_idle > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Choose:
                ChooseKeys(c);
                if (startPressed) Begin(ChooseIndex);
                break;
            case GameState.Attract:
                if (startPressed) { ReturnToTitle(); break; }
                if (StateTime > AttractSeconds) { ShowHighScores(-1); break; }
                Run(dt, Pilot.Drive(this, dt), demo: true);
                break;
            case GameState.Playing:
                Run(dt, c, demo: false);
                break;
            case GameState.GameOver:
                if (StateTime > OverSeconds)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, World.Turn);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) NewGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
        _prev = c;
    }

    private static bool Pressed(bool now, bool before) => now && !before;

    /// <summary>A direction pressed (or held long enough to repeat), for moving through menus.</summary>
    private (int Dx, int Dy) Nudge(Controls c)
    {
        if (c.Dx == 0 && c.Dy == 0) { _repeat = 0; return (0, 0); }
        bool fresh = c.Dx != _prev.Dx || c.Dy != _prev.Dy;
        _repeat -= Field.Step;
        if (!fresh && _repeat > 0) return (0, 0);
        _repeat = fresh ? MenuRepeat * 1.8 : MenuRepeat;
        return (c.Dx, c.Dy);
    }

    // ───────────────────────────── starting and ending ─────────────────────────────

    /// <summary>On to the choice of knight.</summary>
    public void NewGame()
    {
        State = GameState.Choose;
        StateTime = 0;
        NewEntryIndex = -1;
        Message = null;
        Play(Sfx.Click);
    }

    /// <summary>A new game as one of the four Saxon knights.</summary>
    public void Begin(int knight)
    {
        if (knight is < 0 or >= Realm.SaxonCount) return;
        ChooseIndex = knight;
        var w = new World();
        _seed = (int)((uint)(_seed * 1103515245 + 12345) % 999983) + 1;
        Sim.Setup(w, knight, _seed);
        Start(w);
        State = GameState.Playing;
        Play(Sfx.MusicStop);
        Play(Sfx.Fanfare);
    }

    private void Start(World w)
    {
        World = w;
        StateTime = 0;
        Joust = null; Siege = null; Raid = null; Battle = null;
        Mode = MapMode.Orders;
        MenuIndex = 0;
        Selected = -1;
        Message = null;
        ShowCards();
    }

    /// <summary>The game saved at the start of the last turn played, if there is one.</summary>
    public bool Continue()
    {
        var w = SaveFile.Load(SaveFolder);
        if (w == null) { Play(Sfx.Refused); return false; }
        ChooseIndex = w.Player;
        Start(w);
        State = GameState.Playing;
        Play(Sfx.MusicStop);
        Say($"{w.Date}: THE CAMPAIGN GOES ON");
        return true;
    }

    private void StartAttract()
    {
        State = GameState.Attract;
        StateTime = 0;
        Play(Sfx.MusicStop);
        Pilot.Reset();
        var w = new World();
        _seed = (int)((uint)(_seed * 1103515245 + 12345) % 999983) + 1;
        Sim.Setup(w, _seed % Realm.SaxonCount, _seed);
        Start(w);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _idle = 0;
        _tunePending = true;
        Message = null;
        Scene = Scene.Map;
        Joust = null; Siege = null; Raid = null; Battle = null;
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Joust = null; Siege = null; Raid = null; Battle = null;
    }

    public void Pause()
    {
        if (State == GameState.Playing) State = GameState.Paused;
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    /// <summary>The story told: the crown won or the castle lost.</summary>
    private void End()
    {
        Play(World.Won ? Sfx.Victory : Sfx.Defeat);
        if (Demo) { ShowHighScores(-1); return; }
        SaveFile.Delete(SaveFolder);
        State = GameState.GameOver;
        StateTime = 0;
    }

    // ───────────────────────────── the knight ─────────────────────────────

    private void ChooseKeys(Controls c)
    {
        var (dx, dy) = Nudge(c);
        if (dx != 0 || dy != 0)
        {
            ChooseIndex = (ChooseIndex + (dx != 0 ? dx : dy * 2) + Realm.SaxonCount * 4) % Realm.SaxonCount;
            Play(Sfx.Click);
        }
        if (c.Pick is >= 1 and <= Realm.SaxonCount) { Begin(c.Pick - 1); return; }
        if (Pressed(c.Fire, _prev.Fire)) { Begin(ChooseIndex); return; }
        if (Pressed(c.Back, _prev.Back)) ReturnToTitle();
    }

    // ───────────────────────────── a game ─────────────────────────────

    private void Run(double dt, Controls c, bool demo)
    {
        SceneTime += dt;
        switch (Scene)
        {
            case Scene.Card:
                if (Pressed(c.Fire, _prev.Fire) || Pressed(c.Back, _prev.Back) || demo && SceneTime > AttractCardSeconds) DismissCard();
                break;
            case Scene.Map:
                MapKeys(c);
                break;
            case Scene.Joust:
                RunJoust(dt, c);
                break;
            case Scene.Siege:
                RunSiege(dt, c);
                break;
            case Scene.Raid:
                RunRaid(dt, c);
                break;
            case Scene.Battle:
                RunBattle(dt, c);
                break;
        }
        Hear(World.Heard);
    }

    private void Hear(List<Sfx> heard)
    {
        foreach (var s in heard) Play(s);
        heard.Clear();
    }

    private void SetScene(Scene s)
    {
        Scene = s;
        SceneTime = 0;
    }

    /// <summary>The cards waiting are shown one by one; then the game goes on.</summary>
    private void ShowCards()
    {
        if (World.Cards.Count > 0)
        {
            SetScene(Scene.Card);
            Play(World.Cards[0].Kind switch { CardKind.War or CardKind.Loss => Sfx.Drum, CardKind.Victory => Sfx.Fanfare, _ => Sfx.Horn });
            return;
        }
        if (World.Won || World.Lost) { End(); return; }
        if (World.Assault != null) { StartDefence(); return; }
        SetScene(Scene.Map);
        Mode = MapMode.Orders;
        if (State == GameState.Playing) SaveFile.Save(World, SaveFolder);
    }

    public void DismissCard()
    {
        if (Scene != Scene.Card || World.Cards.Count == 0) return;
        World.Cards.RemoveAt(0);
        Play(Sfx.Click);
        ShowCards();
    }

    /// <summary>The player's turn is over: the other lords take theirs, and the season turns.</summary>
    private void EndTurn()
    {
        Joust = null; Siege = null; Raid = null; Battle = null;
        Sim.EndTurn(World);
        Selected = -1;
        Message = null;
        ShowCards();
    }

    // ───────────────────────────── the map and its orders ─────────────────────────────

    public static string OrderName(Order o) => o switch
    {
        Order.Tournament => "HOLD A TOURNAMENT", Order.Conquer => "GO CONQUERING", Order.Raid => "GO RAIDING", Order.Buy => "BUY ARMY",
        Order.Garrison => "MUSTER THE GARRISON", Order.Gil => "SEND TO SHERWOOD", _ => "PASS",
    };

    /// <summary>The shires the player can pick in the current mode: where the army can march, or the castles to raid.</summary>
    public IReadOnlyList<int> Candidates => Mode switch
    {
        MapMode.Conquer => Sim.Marches(World, World.Player).ToList(),
        MapMode.Raid or MapMode.GilRaid => Sim.RaidTargets(World).Select(l => World.Lords[l].Castle).ToList(),
        _ => [],
    };

    public bool CanOrder(Order o) => o switch
    {
        Order.Conquer => !World.Me.Army.Empty,
        Order.Gil => World.GilCalls > 0,
        Order.Garrison => World.Me.At == World.Me.Castle || World.Me.Army.Empty,
        _ => true,
    };

    public void DoOrder(Order o)
    {
        if (State is not (GameState.Playing or GameState.Attract) || Scene != Scene.Map) return;
        MenuIndex = (int)o;
        if (!CanOrder(o))
        {
            Say(o switch
            {
                Order.Conquer => "YOU HAVE NO ARMY IN THE FIELD: BUY ONE FIRST",
                Order.Gil => $"{Realm.OutlawName.ToUpperInvariant()} HAS DONE ALL HE WILL FOR YOU",
                _ => "YOUR ARMY MUST BE AT YOUR CASTLE",
            });
            Play(Sfx.Refused);
            return;
        }
        Play(Sfx.Click);
        Row = 0;
        PickIndex = 0;
        switch (o)
        {
            case Order.Tournament: Mode = MapMode.Tournament; break;
            case Order.Conquer: Mode = MapMode.Conquer; Say("CHOOSE A SHIRE NEXT TO YOUR ARMY TO MARCH INTO", 6); break;
            case Order.Raid: Mode = MapMode.Raid; Say("CHOOSE A CASTLE TO RAID", 6); break;
            case Order.Buy:
                Mode = MapMode.Buy;
                BuySoldiers = BuyKnights = BuyCatapults = 0;
                break;
            case Order.Garrison:
                Mode = MapMode.Garrison;
                MoveSoldiers = MoveKnights = 0;
                MoveToGarrison = !World.Me.Army.Empty;
                break;
            case Order.Gil: Mode = MapMode.Gil; break;
            case Order.Pass:
                Say("YOU WAIT OUT THE SEASON");
                EndTurn();
                break;
        }
    }

    /// <summary>Back from a panel or a choice to the orders.</summary>
    public void Cancel()
    {
        if (Scene != Scene.Map || Mode == MapMode.Orders) return;
        Mode = MapMode.Orders;
        Message = null;
        Play(Sfx.Click);
    }

    /// <summary>A shire clicked on the map: picked as the march's or raid's target, or just looked at.</summary>
    public void ClickShire(int s)
    {
        if (Scene != Scene.Map || s < 0 || s >= Realm.Count) return;
        Selected = s;
        switch (Mode)
        {
            case MapMode.Conquer: Conquer(s); break;
            case MapMode.Raid:
                if (World.CastleOf(s) is { } l && l.Id != World.Player) StartRaid(l.Id);
                else { Say("CHOOSE ANOTHER LORD'S CASTLE"); Play(Sfx.Refused); }
                break;
            case MapMode.GilRaid:
                if (World.CastleOf(s) is { } g && g.Id != World.Player)
                {
                    Sim.GilRaid(World, g.Id);
                    EndTurn();
                }
                else { Say("CHOOSE ANOTHER LORD'S CASTLE"); Play(Sfx.Refused); }
                break;
        }
    }

    private void MapKeys(Controls c)
    {
        var (dx, dy) = Nudge(c);
        bool fire = Pressed(c.Fire, _prev.Fire), back = Pressed(c.Back, _prev.Back);
        int pick = c.Pick != _prev.Pick ? c.Pick : 0;
        switch (Mode)
        {
            case MapMode.Orders:
                if (dy != 0) { MenuIndex = (MenuIndex + dy + 7) % 7; Play(Sfx.Click); }
                if (pick is >= 1 and <= 7) DoOrder((Order)(pick - 1));
                else if (fire) DoOrder((Order)MenuIndex);
                break;
            case MapMode.Conquer or MapMode.Raid or MapMode.GilRaid:
            {
                var list = Candidates;
                if (list.Count == 0) { if (back || fire) Cancel(); break; }
                if (dx != 0 || dy != 0) { PickIndex = (PickIndex + (dx != 0 ? dx : dy) + list.Count * 4) % list.Count; Play(Sfx.Click); }
                PickIndex = Math.Clamp(PickIndex, 0, list.Count - 1);
                Selected = list[PickIndex];
                if (back) Cancel();
                else if (fire) ClickShire(list[PickIndex]);
                break;
            }
            case MapMode.Tournament or MapMode.Gil:
                if (dx != 0 || dy != 0) { Row = 1 - Row; Play(Sfx.Click); }
                if (back) Cancel();
                else if (fire || pick is 1 or 2)
                {
                    bool first = pick == 1 || pick == 0 && Row == 0;
                    if (Mode == MapMode.Tournament) HoldTournament(first);
                    else CallGil(first);
                }
                break;
            case MapMode.Buy:
                if (dy != 0) { Row = (Row + dy + 4) % 4; Play(Sfx.Click); }
                if (dx != 0) AdjustBuy(Row, dx);
                if (back) Cancel();
                else if (fire) ConfirmBuy();
                break;
            case MapMode.Garrison:
                if (dy != 0) { Row = (Row + dy + 3) % 3; Play(Sfx.Click); }
                if (dx != 0) AdjustMove(Row, dx);
                if (back) Cancel();
                else if (fire) ConfirmMove();
                break;
        }
    }

    // ───────────────────────────── buying and mustering ─────────────────────────────

    public int BuyCost => Sim.Price(BuySoldiers, BuyKnights, BuyCatapults);

    /// <summary>One more (or fewer) of a row in the buy panel: soldiers, knights, catapults, or where they go.</summary>
    public void AdjustBuy(int row, int by)
    {
        int gold = World.Me.Gold;
        switch (row)
        {
            case 0: BuySoldiers = Math.Max(0, BuySoldiers + by * (by > 0 && gold - BuyCost >= 5 * Realm.SoldierCost && BuySoldiers >= 10 ? 5 : 1)); break;
            case 1: BuyKnights = Math.Max(0, BuyKnights + by); break;
            case 2: BuyCatapults = Math.Max(0, BuyCatapults + by); break;
            default: BuyToGarrison = !BuyToGarrison; break;
        }
        while (BuyCost > gold && row < 3)
        {
            if (row == 0 && BuySoldiers > 0) BuySoldiers--;
            else if (row == 1 && BuyKnights > 0) BuyKnights--;
            else if (row == 2 && BuyCatapults > 0) BuyCatapults--;
            else break;
        }
        Row = row;
        Play(Sfx.Click);
    }

    public void ConfirmBuy()
    {
        if (BuyCost == 0) { Cancel(); return; }
        if (!Sim.Buy(World, World.Player, BuySoldiers, BuyKnights, BuyCatapults, BuyToGarrison)) { Play(Sfx.Refused); return; }
        Say($"BOUGHT {BuySoldiers} SOLDIERS, {BuyKnights} KNIGHTS" + (BuyCatapults > 0 ? $" AND {BuyCatapults} CATAPULT{(BuyCatapults > 1 ? "S" : "")}" : "") +
            (BuyToGarrison ? " FOR THE GARRISON" : " FOR THE ARMY"));
        Hear(World.Heard);
        BuySoldiers = BuyKnights = BuyCatapults = 0;
        Mode = MapMode.Orders;
    }

    public void AdjustMove(int row, int by)
    {
        var from = MoveToGarrison ? World.Me.Army : World.Me.Garrison;
        switch (row)
        {
            case 0: MoveSoldiers = Math.Clamp(MoveSoldiers + by * (MoveSoldiers >= 10 && by > 0 ? 5 : 1), 0, from.Soldiers); break;
            case 1: MoveKnights = Math.Clamp(MoveKnights + by, 0, from.Knights); break;
            default:
                MoveToGarrison = !MoveToGarrison;
                MoveSoldiers = MoveKnights = 0;
                break;
        }
        Row = row;
        Play(Sfx.Click);
    }

    public void ConfirmMove()
    {
        if (MoveSoldiers + MoveKnights == 0) { Cancel(); return; }
        if (!Sim.Transfer(World, MoveSoldiers, MoveKnights, MoveToGarrison)) { Play(Sfx.Refused); return; }
        Say(MoveToGarrison ? $"{MoveSoldiers} SOLDIERS AND {MoveKnights} KNIGHTS MAN THE WALLS" : $"{MoveSoldiers} SOLDIERS AND {MoveKnights} KNIGHTS JOIN THE ARMY");
        Play(Sfx.Horn);
        MoveSoldiers = MoveKnights = 0;
        Mode = MapMode.Orders;
    }

    // ───────────────────────────── conquering ─────────────────────────────

    /// <summary>Marches the army into a shire: walking in, a battle, or a siege.</summary>
    public void Conquer(int shire)
    {
        var w = World;
        switch (Sim.Plan(w, w.Player, shire))
        {
            case Venture.None:
                Say("YOUR ARMY CAN ONLY MARCH INTO A SHIRE NEXT TO IT");
                Play(Sfx.Refused);
                return;
            case Venture.NeedCatapult:
                Say("YOU NEED A CATAPULT TO BESIEGE A CASTLE");
                Play(Sfx.Refused);
                return;
            case Venture.March:
            {
                bool mine = w.Owner[shire] == w.Player;
                Sim.Conquer(w, w.Player, shire, w.Me.Army);
                Say(mine ? $"YOUR ARMY MARCHES INTO {Realm.Shires[shire].Name.ToUpperInvariant()}" : $"{Realm.Shires[shire].Name.ToUpperInvariant()} IS YOURS WITHOUT A FIGHT");
                Play(Sfx.Horn);
                EndTurn();
                return;
            }
            case Venture.Battle:
                _target = shire;
                _defence = Sim.Defenders(w, shire, w.Player);
                _assault = false;
                StartBattle(w.Me.Army, w.Player, _defence.Troops, _defence.Lord, 1, 0);
                return;
            case Venture.Siege:
                _target = shire;
                _defence = Sim.Defenders(w, shire, w.Player);
                _assault = false;
                Siege = new Siege(w.Me.Army.Catapults, _defence.Troops, _defence.Lord, w.Dice.Next(1 << 30));
                SetScene(Scene.Siege);
                Play(Sfx.Horn);
                return;
        }
    }

    private void StartBattle(Troops attacker, int attackerLord, Troops defender, int defenderLord, double walls, int mine)
    {
        Battle = new Battle(attacker, attackerLord, defender, defenderLord, walls, mine, World.Dice.Next(1 << 30), _target);
        if (World.GilAid) { Battle.Archers = true; World.GilAid = false; }
        TacticIndex = 2;
        SetScene(Scene.Battle);
        Play(Sfx.Horn);
    }

    private void RunSiege(double dt, Controls c)
    {
        var s = Siege!;
        if (c.Pick is >= 1 and <= 3 && c.Pick != _prev.Pick) s.Select((Ammo)(c.Pick - 1));
        if (Pressed(c.Alt, _prev.Alt)) s.NextAmmo();
        if (Pressed(c.Storm, _prev.Storm) || Pressed(c.Back, _prev.Back)) s.Storm();
        s.Step(dt, c.Dx, c.Dy, Pressed(c.Fire, _prev.Fire), c.Point);
        Hear(s.Heard);
        if (s.Over && s.T >= Siege.DoneSeconds) StormCastle();
    }

    /// <summary>The siege over, the army storms the castle against what's left of its garrison and its wall.</summary>
    public void StormCastle()
    {
        var s = Siege;
        if (s == null) return;
        if (World.CastleOf(_target) is { } c)
        {
            // The siege's dead come off the garrison first.
            int ks = s.KilledSoldiers, kk = s.KilledKnights;
            int gs = Math.Min(ks, c.Garrison.Soldiers), gk = Math.Min(kk, c.Garrison.Knights);
            c.Garrison = new Troops(c.Garrison.Soldiers - gs, c.Garrison.Knights - gk);
            if (c.At == _target) c.Army = new Troops(Math.Max(0, c.Army.Soldiers - (ks - gs)), Math.Max(0, c.Army.Knights - (kk - gk)), c.Army.Catapults);
        }
        _defence = _defence with { Troops = s.Left, Walls = s.Walls };
        Siege = null;
        if (_defence.Troops.Empty)
        {
            Sim.Conquer(World, World.Player, _target, World.Me.Army);
            EndTurn();
            return;
        }
        StartBattle(World.Me.Army, World.Player, _defence.Troops, _defence.Lord, _defence.Walls, 0);
    }

    /// <summary>A Norman army at the gate: the player defends with the garrison (and the army, if it's there).</summary>
    private void StartDefence()
    {
        var a = World.Assault!;
        var me = World.Me;
        _target = a.Shire;
        _defence = Sim.Defenders(World, a.Shire, a.Lord);
        _assault = true;
        double walls = _defence.Castle ? 1 + (Sim.CastleWalls - 1) * (1 - a.Breach) : 1;
        StartBattle(a.Army, a.Lord, _defence.Troops, me.Id, walls, 1);
    }

    private void RunBattle(double dt, Controls c)
    {
        var b = Battle!;
        if (b.Phase == BattlePhase.Choose)
        {
            var (dx, dy) = Nudge(c);
            if (dx != 0 || dy != 0)
            {
                TacticIndex = (TacticIndex + (dx != 0 ? dx : dy) + 4) % 4;
                if (TacticIndex == (int)Tactic.Bombard && !b.CanBombard(b.Mine)) TacticIndex = (TacticIndex + (dx != 0 ? dx : dy) + 4) % 4;
                Play(Sfx.Click);
            }
            if (c.Pick is >= 1 and <= 4 && c.Pick != _prev.Pick) Command((Tactic)(c.Pick - 1));
            else if (Pressed(c.Fire, _prev.Fire)) Command((Tactic)TacticIndex);
        }
        b.Step(dt);
        Hear(b.Heard);
        if (b.Finished) EndBattle();
    }

    /// <summary>The player's order to the army for this round.</summary>
    public bool Command(Tactic t)
    {
        if (Battle is not { Phase: BattlePhase.Choose } b) return false;
        TacticIndex = (int)t;
        if (!b.Order(t)) { Play(Sfx.Refused); return false; }
        Hear(b.Heard);
        return true;
    }

    private void EndBattle()
    {
        var b = Battle!;
        var w = World;
        Battle = null;
        if (_assault)
        {
            var a = w.Assault!;
            w.Assault = null;
            Sim.Settle(w, a.Lord, a.Shire, _defence, b);
            if (b.Winner == 1)
                w.Cards.Add(new Card("THE ATTACK IS BEATEN OFF", $"{Realm.Lords[a.Lord].Name} falls back from {Realm.Shires[a.Shire].Name}, leaving his dead on the field.", w.Player, CardKind.Victory, a.Shire));
            else if (!w.Lost)
                w.Cards.Add(new Card("DEFEAT", $"Your army is broken in {Realm.Shires[a.Shire].Name}, and {Realm.Lords[a.Lord].Name} holds the field.", a.Lord, CardKind.Loss, a.Shire));
            Sim.CheckEnd(w);
            ShowCards();
            return;
        }
        bool castle = _defence.Castle;
        Sim.Settle(w, w.Player, _target, _defence, b);
        if (b.Winner == 0 && !castle)
            w.Cards.Add(new Card($"{Realm.Shires[_target].Name.ToUpperInvariant()} IS YOURS", $"The field is yours, and {Realm.Shires[_target].Name} with it. Its lords swear fealty to you.", w.Player, CardKind.Victory, _target));
        else if (b.Winner != 0)
            w.Cards.Add(new Card("THE ATTACK FAILS", $"Your army is thrown back from {Realm.Shires[_target].Name}.", _defence.Lord >= 0 ? _defence.Lord : -1, CardKind.Loss, _target));
        EndTurn();
    }

    // ───────────────────────────── tournaments ─────────────────────────────

    /// <summary>A tournament for land (a shire is the prize) or for fame (a purse and knights to your banner).</summary>
    public void HoldTournament(bool forLand)
    {
        var foes = Sim.TournamentFoes(World, forLand).ToList();
        if (foes.Count == 0) { Say("NO LORD WILL RIDE AGAINST YOU FOR LAND"); Play(Sfx.Refused); return; }
        int foe = foes[World.Dice.Next(foes.Count)];
        _forLand = forLand;
        Joust = new Joust(Realm.Lords[World.Player].Jousting, foe, World.Dice.Next(1 << 30));
        SetScene(Scene.Joust);
        Play(Sfx.Fanfare);
        Mode = MapMode.Orders;
    }

    public bool ForLand => _forLand;

    private void RunJoust(double dt, Controls c)
    {
        var j = Joust!;
        j.Step(dt, c.Dx, c.Dy, c.Point);
        Hear(j.Heard);
        if (j.Over && j.T >= 1.2)
        {
            Sim.Tournament(World, j.Foe, _forLand, j.Result);
            EndTurn();
        }
    }

    // ───────────────────────────── raids ─────────────────────────────

    public void StartRaid(int lord)
    {
        var l = World.Lords[lord];
        if (lord == World.Player || !l.Alive) return;
        Raid = new Raid(Realm.Lords[World.Player].Swordplay, lord, Sim.HoldsLady(World, lord), World.Dice.Next(1 << 30));
        SetScene(Scene.Raid);
        Mode = MapMode.Orders;
        Play(Sfx.Click);
    }

    private void RunRaid(double dt, Controls c)
    {
        var r = Raid!;
        Line? stroke = null;
        bool parry = false;
        if (c.Dy < 0 && _prev.Dy >= 0) stroke = Line.High;
        else if (c.Dy > 0 && _prev.Dy <= 0) stroke = Line.Low;
        else if (c.Dx > 0 && _prev.Dx <= 0 || Pressed(c.Fire, _prev.Fire)) stroke = Line.Middle;
        else if (c.Dx < 0 && _prev.Dx >= 0 || Pressed(c.Alt, _prev.Alt)) parry = true;
        r.Step(dt, stroke, parry);
        Hear(r.Heard);
        if (r.Over)
        {
            Sim.Raided(World, r.Lord, r.Success, r.ForLady);
            EndTurn();
        }
    }

    /// <summary>A stroke or a parry from the buttons under the swordfight.</summary>
    public void Sword(Line? stroke, bool parry = false)
    {
        if (Raid is not { } r) return;
        if (parry) r.Parry();
        else if (stroke is { } s) r.Attack(s);
    }

    // ───────────────────────────── the outlaw ─────────────────────────────

    public void CallGil(bool raid)
    {
        if (World.GilCalls <= 0) { Play(Sfx.Refused); Mode = MapMode.Orders; return; }
        if (raid)
        {
            Mode = MapMode.GilRaid;
            PickIndex = 0;
            Say("CHOOSE THE CASTLE WHOSE TAXES GIL SHOULD TAKE", 6);
            Play(Sfx.Click);
            return;
        }
        if (!Sim.GilAid(World)) { Say("GIL'S ARCHERS ALREADY MARCH WITH YOU"); Play(Sfx.Refused); Mode = MapMode.Orders; return; }
        Mode = MapMode.Orders;
        ShowCards();
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Tick(c);
    }

    public void Idle(int steps) => Run(steps);

    /// <summary>Straight into a game as a knight, past the opening card.</summary>
    public void Go(int knight = 0, int seed = 7)
    {
        _seed = seed;
        Begin(knight);
        World.Cards.Clear();
        ShowCards();
        Message = null;
    }

    /// <summary>Puts a card in front of the player (the story's own come from the rules).</summary>
    public void Tell(Card c)
    {
        World.Cards.Add(c);
        ShowCards();
    }

    /// <summary>Presses a control for a step and lets it go for a step.</summary>
    public void Press(Controls c)
    {
        Tick(c);
        Tick(default);
    }
}
