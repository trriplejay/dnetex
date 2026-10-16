using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// One play-through: the ship, its lasers and the enemies, plus the player's level and upgrades.
/// It ends in defeat when the ship runs out of hit points, or in victory when it is still alive after
/// SurvivalSeconds. Each level gained pauses play until the player picks one of three offered upgrades;
/// the round for level 3 instead offers the two weapon paths, Ballista and Bola, and the pick lasts all game.
/// </summary>
public sealed class GameSession
{
    /// <summary>Points for each enemy the player kills.</summary>
    public const int PointsPerKill = 1;

    /// <summary>Surviving this long (10:00) wins the game.</summary>
    public const float SurvivalSeconds = 10 * 60;

    /// <summary>Angle between neighbouring lasers in a multi-laser volley.</summary>
    public const float VolleySpreadRadians = 10f * MathF.PI / 180f;

    /// <summary>After an enemy touches the ship, further contact is ignored for this long.</summary>
    public const float InvulnerableSeconds = 1f;

    public const int UpgradeChoices = 3;

    /// <summary>The level whose round offers the weapon paths instead of general upgrades.</summary>
    public const int WeaponPathLevel = 3;

    private readonly Random _random;
    private readonly IReadOnlyList<EnemyType> _roster;
    private readonly List<BossArrival> _bossesToCome;
    private readonly List<Enemy> _bosses = [];
    private readonly IReadOnlyList<SpawnPause> _spawnPauses;
    private readonly List<RingSpawn> _ringsToCome;
    private readonly FiringTimer _firingTimer = new();
    private readonly List<Projectile> _lasers = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Hazard> _hazards = [];
    private readonly List<Enemy> _enemySnapshot = [];
    private int _pendingLevelUps;

    /// <param name="roster">Enemy types that join the spawner at their arrival times; defaults to EnemyRoster.All.</param>
    /// <param name="bosses">Bosses and when they enter; defaults to EnemyRoster.Bosses.</param>
    /// <param name="spawnPauses">When regular spawning stops; defaults to EnemyRoster.SpawnPauses.</param>
    /// <param name="rings">Rings of enemies around the ship and when they appear; defaults to EnemyRoster.Rings.</param>
    /// <param name="start">
    /// Debug start point. The clock starts at its time, with every enemy type that has arrived by then; bosses
    /// due earlier are skipped, while one due at exactly that time enters on the first frame. Starting above
    /// level 1 offers the upgrade cards for each level skipped before anything moves. Rings follow the same rule as bosses.
    /// </param>
    public GameSession(Vector2 shipPosition, Random random, IReadOnlyList<EnemyType>? roster = null,
        IReadOnlyList<BossArrival>? bosses = null, StartOptions? start = null,
        IReadOnlyList<SpawnPause>? spawnPauses = null, IReadOnlyList<RingSpawn>? rings = null)
    {
        start ??= StartOptions.Default;
        _random = random;
        _roster = roster ?? EnemyRoster.All;
        ElapsedSeconds = start.StartSeconds;
        _bossesToCome = [.. (bosses ?? EnemyRoster.Bosses).Where(arrival => arrival.AtSeconds >= ElapsedSeconds)];
        _spawnPauses = spawnPauses ?? EnemyRoster.SpawnPauses;
        _ringsToCome = [.. (rings ?? EnemyRoster.Rings).Where(ring => ring.AtSeconds >= ElapsedSeconds)];
        Ship = new Ship(shipPosition);
        Spawner = new EnemySpawner(random);
        Leveling = new Leveling(start.StartLevel);
        _pendingLevelUps = start.StartLevel - 1;
        AddArrivedEnemyTypes();
        OfferNextUpgrade();
    }

    public Ship Ship { get; }

    /// <summary>Which enemy types spawn and how often; rates can be changed mid-game.</summary>
    public EnemySpawner Spawner { get; }

    public PlayerStats Stats { get; } = new();
    public Leveling Leveling { get; }

    /// <summary>Projectiles in flight: lasers, or the chosen weapon path's projectiles once one is picked.</summary>
    public IReadOnlyList<Projectile> Lasers => _lasers;
    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>Bosses currently alive. While there are any, the regular spawner is paused.</summary>
    public IReadOnlyList<Enemy> Bosses => _bosses;

    /// <summary>Hazards currently in play (trails, rings, bullets).</summary>
    public IReadOnlyList<Hazard> Hazards => _hazards;

    public bool IsBossActive => _bosses.Count > 0;

    /// <summary>True during a scheduled lull, when regular spawning stops.</summary>
    public bool IsSpawningPaused => _spawnPauses.Any(pause => pause.Covers(ElapsedSeconds));

    /// <summary>Seconds survived, stopping at SurvivalSeconds.</summary>
    public float ElapsedSeconds { get; private set; }

    public int Score { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsFinished => IsGameOver || IsVictory;

    /// <summary>Seconds of contact immunity left after the ship was last hit.</summary>
    public float InvulnerableSecondsLeft { get; private set; }

    /// <summary>The upgrades on offer while play is paused for a level-up; null while playing.</summary>
    public IReadOnlyList<Upgrade>? UpgradeOffer { get; private set; }

    public bool IsChoosingUpgrade => UpgradeOffer is not null;

    /// <summary>Adds an enemy (and any shield orbiting it) directly, for spawns that don't come from the edge spawner.</summary>
    public void AddEnemy(Enemy enemy) => AddEnemies([enemy]);

    /// <summary>Adds a hazard directly to the session.</summary>
    public void AddHazard(Hazard hazard) => _hazards.Add(hazard);

    /// <summary>Adds multiple hazards directly to the session.</summary>
    public void AddHazards(IEnumerable<Hazard> hazards) => _hazards.AddRange(hazards);

    /// <summary>Applies the chosen card, then offers the next level-up's cards or resumes play.</summary>
    public void ChooseUpgrade(int index)
    {
        if (UpgradeOffer is null)
            throw new InvalidOperationException("No upgrade is on offer.");
        if (index < 0 || index >= UpgradeOffer.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Upgrades.Apply(UpgradeOffer[index], Stats);
        _pendingLevelUps--;
        UpgradeOffer = null;
        OfferNextUpgrade();
    }

    /// <summary>Advances one frame. Nothing moves while an upgrade is being chosen or once the game is won or lost.</summary>
    /// <param name="moveDirection">Screen direction to fly in (see ShipInput.Direction).</param>
    /// <param name="aimTarget">Point the ship turns to face and shoots toward, e.g. the mouse; null keeps the heading.</param>
    public void Update(Vector2 moveDirection, Vector2? aimTarget, float elapsedSeconds, float width, float height)
    {
        if (IsFinished || IsChoosingUpgrade)
            return;

        ElapsedSeconds = MathF.Min(ElapsedSeconds + elapsedSeconds, SurvivalSeconds);
        InvulnerableSecondsLeft = MathF.Max(0f, InvulnerableSecondsLeft - elapsedSeconds);
        AddArrivedEnemyTypes();

        Ship.Update(moveDirection, aimTarget, elapsedSeconds);

        foreach (var laser in _lasers)
            laser.Update(elapsedSeconds);
        foreach (var shot in _firingTimer.Update(elapsedSeconds, Ship.Nose, Ship.Heading, Stats.AttacksPerSecond))
            _lasers.AddRange(Volley(shot.Position, shot.Heading));

        AddArrivedBosses(width, height);
        AddArrivedRings();
        // Regular spawns wait while a boss is alive or during a lull; the spawner's timing is frozen, not saved up.
        if (!IsBossActive && !IsSpawningPaused)
            AddEnemies(Spawner.Update(elapsedSeconds, width, height));
        foreach (var enemy in _enemies)
        {
            var prevChargeSeg = (enemy.Movement as ChargeAlongLine)?.LastChargeSegment;
            enemy.Update(Ship.Position, elapsedSeconds);
            if (enemy.Movement is ChargeAlongLine charge)
            {
                var seg = charge.LastChargeSegment;
                if (seg != prevChargeSeg && Vector2.DistanceSquared(seg.Start, seg.End) > 0.01f)
                    AddHazard(Hazard.CreateTrail(seg.Start, seg.End));
            }
            if (enemy.Movement is IHazardProducer moveProds)
                AddHazards(moveProds.TakeHazards());
        }
        // Both passes walk a snapshot, so enemies they add join play without taking part in the same pass.
        foreach (var enemy in SnapshotEnemies())
        {
            AddEnemies(enemy.UseAbility(Ship.Position, elapsedSeconds, _random));
            if (enemy.Ability is IHazardProducer abilityProds)
                AddHazards(abilityProds.TakeHazards());
        }

        foreach (var hazard in _hazards)
            hazard.Update(elapsedSeconds);
        _hazards.RemoveAll(hazard => hazard.IsExpired);

        ResolveLaserHits();
        // A boss can respawn its shield when a hit moves it into a new phase.
        foreach (var enemy in SnapshotEnemies())
            _enemies.AddRange(enemy.TakeNewShields());
        _lasers.RemoveAll(laser => laser.IsExpired(width, height));
        ResolveContact();

        var claudeBossKilled = _bosses.RemoveAll(boss => boss.IsDead && boss.Type == EnemyRoster.ClaudeBoss) > 0;
        _bosses.RemoveAll(boss => boss.IsDead);
        if (claudeBossKilled && !IsGameOver)
            IsVictory = true;
        if (!IsFinished)
            OfferNextUpgrade();
    }

    // Copies the current enemies into a buffer reused every pass, instead of a fresh array each time.
    private List<Enemy> SnapshotEnemies()
    {
        _enemySnapshot.Clear();
        _enemySnapshot.AddRange(_enemies);
        return _enemySnapshot;
    }

    private void AddArrivedEnemyTypes()
    {
        foreach (var type in _roster)
        {
            if (type.ArrivesAtSeconds <= ElapsedSeconds)
                Spawner.Add(type);
        }
    }

    private void AddArrivedBosses(float width, float height)
    {
        foreach (var arrival in _bossesToCome.Where(arrival => arrival.AtSeconds <= ElapsedSeconds).ToArray())
        {
            _bossesToCome.Remove(arrival);
            var boss = Spawner.SpawnAtEdge(arrival.Type, width, height);
            _bosses.Add(boss);
            AddEnemies([boss]);
        }
    }

    private void AddEnemies(IEnumerable<Enemy> enemies)
    {
        foreach (var enemy in enemies)
        {
            _enemies.Add(enemy);
            _enemies.AddRange(enemy.TakeNewShields());
        }
    }

    private void AddArrivedRings()
    {
        foreach (var ring in _ringsToCome.Where(ring => ring.AtSeconds <= ElapsedSeconds).ToArray())
        {
            _ringsToCome.Remove(ring);
            AddEnemies(ring.Spawn(Ship.Position, _random));
        }
    }

    private IEnumerable<Projectile> Volley(Vector2 nose, float heading)
    {
        var count = Stats.ProjectileCount;
        for (var i = 0; i < count; i++)
        {
            var offset = (i - (count - 1) / 2f) * VolleySpreadRadians;
            yield return Stats.WeaponPath switch
            {
                // A ballista bolt passes through two more enemies than the player's piercing alone allows.
                WeaponPath.Ballista => new Ballista(nose, heading + offset, Stats.ProjectileScale, Stats.Pierce + 2),
                WeaponPath.Bola => new Bola(nose, heading + offset, Stats.ProjectileScale, Stats.Pierce),
                _ => new Laser(nose, heading + offset, Stats.ProjectileScale, Stats.Pierce),
            };
        }
    }

    private void ResolveLaserHits()
    {
        _lasers.RemoveAll(laser =>
        {
            var shape = laser.GetShape();
            var bounds = shape.GetBounds();
            foreach (var enemy in _enemies)
            {
                // The bounds check only skips enemies the shape provably can't reach, before the exact test.
                if (enemy.IsDead || laser.HasHit(enemy) || !bounds.Reaches(enemy) || !shape.Touches(enemy.GetStrokes()))
                    continue;

                // A bola hit also slows what it hits, unless a shield blocks the hit.
                enemy.TakeDamage(Stats.Damage, slow: laser is Bola);
                if (enemy.IsDead)
                {
                    Score += PointsPerKill;
                    _pendingLevelUps += Leveling.Add(enemy.Experience);
                }
                if (laser.RegisterHit(enemy))
                    return true;
            }
            return false;
        });
        _enemies.RemoveAll(enemy => enemy.IsDead);
    }

    private void ResolveContact()
    {
        if (InvulnerableSecondsLeft > 0f)
            return;

        var shipStrokes = Ship.GetStrokes();
        var touching = _enemies.Where(enemy => Collision.StrokesTouch(shipStrokes, enemy.GetStrokes())).ToList();
        var touchingHazards = _hazards.Where(hazard => hazard.IsArmed && hazard.Touches(shipStrokes)).ToList();
        if (touching.Count == 0 && touchingHazards.Count == 0)
            return;

        Stats.HitPoints = Math.Max(0, Stats.HitPoints - 1);
        if (Stats.HitPoints == 0)
        {
            // Leave the enemy that landed the final blow on screen behind the GAME OVER text.
            IsGameOver = true;
            return;
        }

        // Surviving a hit destroys what touched the ship (for no points) and grants a moment of immunity.
        // Bosses survive the collision; they can only be beaten with lasers.
        foreach (var enemy in touching.Where(enemy => !_bosses.Contains(enemy)))
            _enemies.Remove(enemy);
        foreach (var hazard in touchingHazards.Where(h => h.ExpireOnContact))
            _hazards.Remove(hazard);
        InvulnerableSecondsLeft = InvulnerableSeconds;
    }

    private void OfferNextUpgrade()
    {
        if (UpgradeOffer is not null || _pendingLevelUps <= 0)
            return;

        // Rounds are offered oldest first, so this round grants the lowest level still waiting for its cards.
        var levelGranted = Leveling.Level - _pendingLevelUps + 1;
        UpgradeOffer = levelGranted == WeaponPathLevel
            ? Upgrades.WeaponPaths
            : Upgrades.Offer(Stats, _random, UpgradeChoices);
    }
}
