using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// One play-through: the ship, its lasers and the enemies, plus the player's level and upgrades.
/// The ship starts with 100 hit points. Whatever touches it (an enemy, a boss, an armed trail or ring band)
/// deals its contact damage the moment it first touches and again every ContactTickSeconds while it stays in
/// contact, each source on its own timer; enemies and bosses survive the contact, and an armed bullet deals its
/// damage once and is gone. Contact damage grows with the clock (EnemyRoster.ScaledContactDamage).
/// Each hit then passes through the ship's enhancements in order: shield, then armor (see DamageShip).
/// It ends in defeat when the ship's hit points reach 0, or in victory when the Claude Boss is killed.
/// It opens paused on the starting-weapon pick; each level gained then pauses play until the
/// player picks one of up to three offered cards (a level with no card left to offer doesn't pause).
/// Every owned weapon fires on its own timer with its own stats.
/// </summary>
public sealed class GameSession
{
    /// <summary>Points for each enemy the player kills.</summary>
    public const int PointsPerKill = 1;

    /// <summary>Arrival time of the final boss (12:00) and the upper limit for the --time start option.</summary>
    public const float SurvivalSeconds = EnemyRoster.ClaudeBossArrivesAtSeconds;

    /// <summary>Angle between neighbouring lasers in a multi-laser volley.</summary>
    public const float VolleySpreadRadians = 10f * MathF.PI / 180f;

    /// <summary>While a source stays in contact with the ship, it deals its damage again after each this many seconds.</summary>
    public const float ContactTickSeconds = 0.5f;

    /// <summary>Maximum number of live regular (non-boss) enemies in play at once.</summary>
    public const int MaxRegularEnemies = 300;

    // The ship's outline reaches 24 px from its centre; the slack absorbs float rounding in the transformed strokes.
    private const float ShipContactRadius = 24.5f;

    // Absorbs float drift in a contact timer summed from many small frames, so a tick due at 0.5 s isn't a frame late.
    private const float ContactTimerSlack = 1e-4f;

    public const int UpgradeChoices = 3;

    private readonly Random _random;
    private readonly IReadOnlyList<EnemyType> _roster;
    private readonly List<BossArrival> _bossesToCome;
    private readonly List<Enemy> _bosses = [];
    private readonly IReadOnlyList<SpawnPause> _spawnPauses;
    private readonly List<RingSpawn> _ringsToCome;
    private readonly List<Projectile> _lasers = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<Hazard> _hazards = [];
    private readonly List<Enemy> _enemySnapshot = [];
    private readonly List<LightningSegment> _lightningSegments = [];
    private readonly List<RicochetDisc> _discs = [];
    private readonly ProximityMines _mines = new();
    private readonly Action<Enemy, float> _damageEnemy;
    // Seconds until each touching source (enemy or hazard) deals its next tick; a source not touching has no entry.
    // Two maps swapped each frame, so a source that stops touching drops its timer without a per-frame allocation.
    private Dictionary<object, float> _contactTimers = new(ReferenceEqualityComparer.Instance);
    private Dictionary<object, float> _nextContactTimers = new(ReferenceEqualityComparer.Instance);
    private int _pendingLevelUps;
    // The Shield level the shield was last filled for, so a level gained (from a card or straight on Stats) fills it.
    private int _shieldLevelFilled;
    // Seconds until a broken shield refills; runs only while the shield is broken.
    private float _shieldRechargeTimer;
    private float _regenTimer;

    /// <param name="roster">Enemy types that join the spawner at their arrival times; defaults to EnemyRoster.All.</param>
    /// <param name="bosses">Bosses and when they enter; defaults to EnemyRoster.Bosses.</param>
    /// <param name="spawnPauses">When regular spawning stops; defaults to EnemyRoster.SpawnPauses.</param>
    /// <param name="rings">Rings of enemies around the ship and when they appear; defaults to EnemyRoster.Rings.</param>
    /// <param name="start">
    /// Debug start point. The clock starts at its time, with every enemy type that has arrived by then; bosses
    /// due earlier are skipped, while one due at exactly that time enters on the first frame. Starting above
    /// level 1 offers the upgrade cards for each level skipped, after the starting pick and before anything moves.
    /// Rings follow the same rule as bosses.
    /// </param>
    /// <remarks>
    /// Every session opens on the starting pick: play is paused on Upgrades.StartingWeapons and the ship owns no
    /// weapon until one is chosen.
    /// </remarks>
    public GameSession(Vector2 shipPosition, Random random, IReadOnlyList<EnemyType>? roster = null,
        IReadOnlyList<BossArrival>? bosses = null, StartOptions? start = null,
        IReadOnlyList<SpawnPause>? spawnPauses = null, IReadOnlyList<RingSpawn>? rings = null)
    {
        start ??= StartOptions.Default;
        _random = random;
        _damageEnemy = DamageEnemy;
        _roster = roster ?? EnemyRoster.All;
        ElapsedSeconds = start.StartSeconds;
        _bossesToCome = [.. (bosses ?? EnemyRoster.Bosses).Where(arrival => arrival.AtSeconds >= ElapsedSeconds)];
        _spawnPauses = spawnPauses ?? EnemyRoster.SpawnPauses;
        _ringsToCome = [.. (rings ?? EnemyRoster.Rings).Where(ring => ring.AtSeconds >= ElapsedSeconds)];
        Ship = new Ship(shipPosition);
        Spawner = new EnemySpawner(random);
        Leveling = new Leveling(start.StartLevel);
        // The skipped levels' rounds wait until the starting pick is made.
        _pendingLevelUps = start.StartLevel - 1;
        AddArrivedEnemyTypes();
        IsStartingPick = true;
        UpgradeOffer = Upgrades.StartingWeapons;
    }

    public Ship Ship { get; }

    /// <summary>Which enemy types spawn and how often; rates can be changed mid-game.</summary>
    public EnemySpawner Spawner { get; }

    public PlayerStats Stats { get; } = new();
    public Leveling Leveling { get; }

    /// <summary>Projectiles in flight, fired by the owned weapons.</summary>
    public IReadOnlyList<Projectile> Lasers => _lasers;
    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>The lightning drawn this frame: ship to first target, then each hop; cleared at the start of every played frame.</summary>
    public IReadOnlyList<LightningSegment> LightningSegments => _lightningSegments;

    /// <summary>Ricochet Discs in flight, out or back (position, radius, leg).</summary>
    public IReadOnlyList<RicochetDisc> Discs => _discs;

    /// <summary>Proximity Mines lying in play, oldest first (position, armed).</summary>
    public IReadOnlyList<Mine> Mines => _mines.Mines;

    /// <summary>Mine explosions this frame (centre, blast radius); cleared at the start of every played frame's hit pass.</summary>
    public IReadOnlyList<Circle> MineExplosions => _mines.Explosions;

    /// <summary>Bosses currently alive. While there are any, the regular spawner is paused.</summary>
    public IReadOnlyList<Enemy> Bosses => _bosses;

    /// <summary>Hazards currently in play (trails, rings, bullets).</summary>
    public IReadOnlyList<Hazard> Hazards => _hazards;

    public bool IsBossActive => _bosses.Count > 0;

    /// <summary>Number of live regular (non-boss) enemies currently in play.</summary>
    public int LiveRegularEnemyCount => _enemies.Count(enemy => !enemy.IsDead && !_bosses.Contains(enemy));

    /// <summary>True during a scheduled lull, when regular spawning stops.</summary>
    public bool IsSpawningPaused => _spawnPauses.Any(pause => pause.Covers(ElapsedSeconds));

    /// <summary>Seconds survived, stopping when the run is won or lost.</summary>
    public float ElapsedSeconds { get; private set; }

    public int Score { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsFinished => IsGameOver || IsVictory;

    /// <summary>Shield points left (read from Stats.ShieldPoints, their one owner); a hit takes from these first.</summary>
    public float ShieldPoints => Stats.ShieldPoints;

    /// <summary>Shield points a full shield holds at the owned Shield level (Stats.ShieldCapacity).</summary>
    public float ShieldCapacity => Stats.ShieldCapacity;

    /// <summary>Seconds until a broken shield refills to capacity; 0 while the shield is up or not owned.</summary>
    public float ShieldRechargeSecondsLeft => _shieldRechargeTimer;

    /// <summary>The cards on offer while play is paused for the starting pick or a level-up; null while playing.</summary>
    public IReadOnlyList<Upgrade>? UpgradeOffer { get; private set; }

    public bool IsChoosingUpgrade => UpgradeOffer is not null;

    /// <summary>True while the offer is the starting-weapon pick rather than a level-up round.</summary>
    public bool IsStartingPick { get; private set; }

    /// <summary>Adds an enemy (and any shield orbiting it) directly, for spawns that don't come from the edge spawner.</summary>
    public void AddEnemy(Enemy enemy) => AddEnemies([enemy]);

    /// <summary>Adds a hazard directly to the session.</summary>
    public void AddHazard(Hazard hazard) => _hazards.Add(hazard);

    /// <summary>Adds multiple hazards directly to the session.</summary>
    public void AddHazards(IEnumerable<Hazard> hazards) => _hazards.AddRange(hazards);

    /// <summary>Applies the chosen card, then offers the next level-up's cards or resumes play.</summary>
    /// <exception cref="InvalidOperationException">No card is on offer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index names no card on offer.</exception>
    public void ChooseUpgrade(int index)
    {
        if (UpgradeOffer is null)
            throw new InvalidOperationException("No upgrade is on offer.");
        if (index < 0 || index >= UpgradeOffer.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Upgrades.Apply(UpgradeOffer[index], Stats);
        GrantShieldCapacity();
        // The starting pick grants no level, so it uses up no waiting round.
        if (IsStartingPick)
            IsStartingPick = false;
        else
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

        ElapsedSeconds += elapsedSeconds;
        AddArrivedEnemyTypes();

        UpdateShieldRecharge(elapsedSeconds);
        UpdateRegeneration(elapsedSeconds);

        Ship.SpeedMultiplier = ShipEnhancements.SpeedMultiplier(ShipEnhancements.LevelOf(Stats, ShipEnhancements.MoveSpeed));
        Ship.Update(moveDirection, aimTarget, elapsedSeconds);

        foreach (var laser in _lasers)
            laser.Update(elapsedSeconds);
        foreach (var disc in _discs)
            disc.Update(elapsedSeconds, Ship.Position);
        _lightningSegments.Clear();
        foreach (var weapon in Stats.Weapons)
            FireWeapon(weapon, elapsedSeconds);

        AddArrivedBosses(width, height);
        AddArrivedRings();
        // Regular spawns wait while a boss is alive, during a lull, or while live regular enemies are at or above the cap; the spawner's timing is frozen, not saved up.
        if (!IsBossActive && !IsSpawningPaused && LiveRegularEnemyCount < MaxRegularEnemies)
            AddEnemies(Spawner.Update(elapsedSeconds, width, height));

        // Existing hazards age and expire before any newly produced hazards this frame are added.
        // Hazards newly produced by abilities or movement this frame are added below after aging,
        // so their AgeSeconds remains 0 at the subsequent contact check of frame N.
        foreach (var hazard in _hazards)
            hazard.Update(elapsedSeconds);
        _hazards.RemoveAll(hazard => hazard.IsExpired);

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
            // A split is told how many copies of its type are alive, from every phase, so it never passes its cap.
            AddEnemies(enemy.Ability is SplitIntoCopies split
                ? split.Update(enemy, Ship.Position, elapsedSeconds, _random, LiveCopiesOf(enemy.Type))
                : enemy.UseAbility(Ship.Position, elapsedSeconds, _random));
            if (enemy.Ability is IHazardProducer abilityProds)
                AddHazards(abilityProds.TakeHazards());
        }

        ResolveWeaponHits(elapsedSeconds);
        // A boss can respawn its shield when a hit moves it into a new phase.
        foreach (var enemy in SnapshotEnemies())
            _enemies.AddRange(enemy.TakeNewShields());
        _lasers.RemoveAll(laser => laser.IsExpired(width, height));
        ResolveContact(elapsedSeconds);

        var claudeBossKilled = _bosses.RemoveAll(boss => boss.IsDead && boss.Type == EnemyRoster.ClaudeBoss) > 0;
        var hallucinationBossKilled = _bosses.RemoveAll(boss => boss.IsDead && boss.Type == EnemyRoster.Hallucination) > 0;
        if (hallucinationBossKilled)
            _enemies.RemoveAll(enemy => enemy.IsCopy && enemy.Type == EnemyRoster.Hallucination);
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

    private int LiveCopiesOf(EnemyType type) => _enemies.Count(enemy => enemy.IsCopy && !enemy.IsDead && enemy.Type == type);

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

    /// <summary>Advances one weapon's own timer and fires what it emits this frame.</summary>
    private void FireWeapon(OwnedWeapon weapon, float elapsedSeconds)
    {
        switch (weapon.Kind)
        {
            case WeaponKind.Laser:
                foreach (var shot in weapon.Fire(elapsedSeconds, Ship.Nose, Ship.Heading))
                    _lasers.AddRange(Volley(weapon, shot));
                break;
            case WeaponKind.Lightning:
                // With nothing to strike, the shot waits rather than being spent, so the next enemy is struck at once.
                if (!Lightning.HasTarget(_enemies))
                {
                    weapon.Charge(elapsedSeconds);
                    break;
                }
                foreach (var _ in weapon.Fire(elapsedSeconds, Ship.Nose, Ship.Heading))
                    Lightning.Strike(Ship.Position, weapon.Damage, weapon.Count, _enemies, _random, _damageEnemy, _lightningSegments);
                break;
            case WeaponKind.Orbs:
                foreach (var (enemy, damage) in weapon.Orbs!.Update(elapsedSeconds, Ship.Position, _enemies))
                    DamageEnemy(enemy, damage);
                break;
            case WeaponKind.RicochetDisc:
                foreach (var _ in weapon.Fire(elapsedSeconds, Ship.Nose, Ship.Heading))
                    _discs.AddRange(RicochetDisc.Throw(weapon, Ship.Position, Ship.Forward, _enemies));
                break;
            case WeaponKind.ProximityMines:
                foreach (var _ in weapon.Fire(elapsedSeconds, Ship.Nose, Ship.Heading))
                    _mines.Drop(weapon, weapon.MineDropPositions(Ship.Position, _random));
                break;
            default:
                throw new InvalidOperationException($"{weapon.Kind} has no firing rule.");
        }
    }

    private static IEnumerable<Projectile> Volley(OwnedWeapon laser, ShotPose shot)
    {
        var count = laser.Count;
        for (var i = 0; i < count; i++)
        {
            var offset = (i - (count - 1) / 2f) * VolleySpreadRadians;
            yield return new Laser(shot.Position, shot.Heading + offset, pierce: laser.Pierce, damage: laser.Damage);
        }
    }

    private void ResolveWeaponHits(float elapsedSeconds)
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

                // Each projectile deals its own damage, fixed when it was fired.
                DamageEnemy(enemy, laser.Damage);
                if (laser.RegisterHit(enemy))
                    return true;
            }
            return false;
        });
        ResolveDiscAndMineHits(elapsedSeconds);
        _enemies.RemoveAll(enemy => enemy.IsDead);
    }

    /// <summary>
    /// Disc and mine damage, all dealt through DamageEnemy so each kill is credited once; each skips enemies
    /// already dead, so overlapping blasts never credit a kill twice.
    /// </summary>
    private void ResolveDiscAndMineHits(float elapsedSeconds)
    {
        foreach (var disc in _discs)
            disc.Strike(_enemies, _damageEnemy);
        _discs.RemoveAll(disc => disc.IsCaught);
        _mines.Update(elapsedSeconds, _enemies, _damageEnemy);
    }

    /// <summary>A weapon hit: deals damage to the enemy and credits the kill if this hit is what killed it.</summary>
    private void DamageEnemy(Enemy enemy, float damage)
    {
        var wasAlive = !enemy.IsDead;
        enemy.TakeDamage(damage);
        CreditKill(enemy, wasAlive);
    }

    /// <summary>
    /// The one place a kill is credited, for every kind of weapon damage (hits, strikes, hops, orbs, discs and
    /// mine blasts): adds the kill's points and experience when the enemy was alive before the damage and is
    /// dead after it, so damage to an enemy already dead earns nothing and a kill is credited once.
    /// </summary>
    private void CreditKill(Enemy enemy, bool wasAlive)
    {
        if (wasAlive && enemy.IsDead)
        {
            Score += PointsPerKill;
            _pendingLevelUps += Leveling.Add(enemy.Experience);
        }
    }

    /// <summary>
    /// Contact damage: every enemy, boss or armed hazard touching the ship deals one tick of its time-scaled damage
    /// the moment it first touches, then another every ContactTickSeconds while it stays in contact, each on its own
    /// timer. Enemies and bosses survive the contact; an armed bullet deals its tick and is removed. A source that
    /// stops touching loses its timer, so touching again deals a fresh tick at once.
    /// </summary>
    private void ResolveContact(float elapsedSeconds)
    {
        _nextContactTimers.Clear();
        var shipStrokes = Ship.GetStrokes();
        var shipPosition = Ship.Position;

        foreach (var enemy in _enemies)
        {
            if (IsGameOver)
                break;
            if (enemy.IsDead || enemy.Movement is Thrown { IsHarmlessInFlight: true })
                continue;
            // The bounding circles only skip enemies that provably can't reach the ship, before the exact test.
            var reach = ShipContactRadius + enemy.Type.Radius;
            if (Vector2.DistanceSquared(shipPosition, enemy.Position) > reach * reach
                || !Collision.StrokesTouch(shipStrokes, enemy.GetStrokes()))
                continue;

            var damage = EnemyRoster.ScaledContactDamage(enemy.Type.ContactDamage, ElapsedSeconds);
            for (var tick = ContactTicksDue(enemy, elapsedSeconds); tick > 0 && !IsGameOver; tick--)
                DamageShip(damage);
        }

        foreach (var hazard in _hazards)
        {
            if (IsGameOver)
                break;
            if (!hazard.IsArmed || !hazard.Touches(shipStrokes))
                continue;

            var damage = EnemyRoster.ScaledContactDamage(hazard.ContactDamage, ElapsedSeconds);
            if (hazard.ExpireOnContact)
            {
                // A bullet hurts once and is gone; it keeps no timer.
                DamageShip(damage);
                hazard.MarkSpent();
                continue;
            }
            for (var tick = ContactTicksDue(hazard, elapsedSeconds); tick > 0 && !IsGameOver; tick--)
                DamageShip(damage);
        }

        // Leave whatever landed the final blow on screen behind the GAME OVER text.
        if (!IsGameOver)
            _hazards.RemoveAll(hazard => hazard.IsSpent);
        (_contactTimers, _nextContactTimers) = (_nextContactTimers, _contactTimers);
    }

    /// <summary>
    /// How many ticks a touching source deals this frame: one on first contact, then one each time its own
    /// ContactTickSeconds timer runs out (several, if a long frame covers several). Records its timer for next frame.
    /// </summary>
    private int ContactTicksDue(object source, float elapsedSeconds)
    {
        int ticks;
        float left;
        if (_contactTimers.TryGetValue(source, out var previous))
        {
            left = previous - elapsedSeconds;
            ticks = left > ContactTimerSlack ? 0 : 1 + (int)MathF.Floor((ContactTimerSlack - left) / ContactTickSeconds);
            left += ticks * ContactTickSeconds;
        }
        else
        {
            ticks = 1;
            left = ContactTickSeconds;
        }
        _nextContactTimers[source] = left;
        return ticks;
    }

    /// <summary>
    /// The one place the ship loses hit points, for every source (enemy, boss, bullet, trail, ring). Takes damage
    /// already scaled for the clock and applies it in order: the shield absorbs first, then armor takes its
    /// reduction off what is left, never below its minimum. At 0 hit points the run is lost.
    /// </summary>
    private void DamageShip(float damage)
    {
        damage = AbsorbWithShield(damage);
        if (damage <= 0f)
            return;

        var armorLevel = ShipEnhancements.LevelOf(Stats, ShipEnhancements.Armor);
        Stats.HitPoints -= ShipEnhancements.ArmoredDamage(damage, armorLevel);
        if (Stats.HitPoints <= 0f)
            IsGameOver = true;
    }

    private void UpdateRegeneration(float elapsedSeconds)
    {
        _regenTimer += elapsedSeconds;
        while (_regenTimer + ContactTimerSlack >= ShipEnhancements.RegenTickSeconds)
        {
            _regenTimer = MathF.Max(0f, _regenTimer - ShipEnhancements.RegenTickSeconds);
            var regenLevel = ShipEnhancements.LevelOf(Stats, ShipEnhancements.Regeneration);
            if (regenLevel != 0 && Stats.HitPoints > 0f)
                Stats.HitPoints += ShipEnhancements.RegenPerTick(regenLevel);
        }
    }

    /// <summary>
    /// Takes the damage out of the shield first and returns what gets through. A hit that empties the shield
    /// starts its recharge wait (ShipEnhancements.ShieldRechargeSeconds at the owned level).
    /// </summary>
    private float AbsorbWithShield(float damage)
    {
        // Picks up a level applied straight to Stats as well as one chosen from the cards.
        GrantShieldCapacity();
        var shieldLevel = ShipEnhancements.LevelOf(Stats, ShipEnhancements.Shield);
        if (shieldLevel == 0)
            return damage;
        // A broken shield absorbs nothing, and a hit leaves its running wait alone: the wait starts on the break
        // and runs out even while the ship stays under contact.
        if (Stats.ShieldPoints <= 0f)
            return damage;

        var absorbed = MathF.Min(Stats.ShieldPoints, damage);
        Stats.ShieldPoints -= absorbed;
        if (Stats.ShieldPoints <= 0f)
            _shieldRechargeTimer = ShipEnhancements.ShieldRechargeSeconds(shieldLevel);
        return damage - absorbed;
    }

    /// <summary>Fills the shield to capacity whenever a Shield level has been gained since it was last filled.</summary>
    private void GrantShieldCapacity()
    {
        var shieldLevel = ShipEnhancements.LevelOf(Stats, ShipEnhancements.Shield);
        if (shieldLevel <= _shieldLevelFilled)
            return;
        _shieldLevelFilled = shieldLevel;
        Stats.ShieldPoints = Stats.ShieldCapacity;
        _shieldRechargeTimer = 0f;
    }

    /// <summary>
    /// Runs a broken shield's wait down on each played frame; when it reaches 0 the shield refills to capacity
    /// (ShipEnhancements.ShieldCapacity at the owned level).
    /// </summary>
    private void UpdateShieldRecharge(float elapsedSeconds)
    {
        GrantShieldCapacity();
        if (_shieldRechargeTimer <= 0f)
            return;
        _shieldRechargeTimer -= elapsedSeconds;
        if (_shieldRechargeTimer > ContactTimerSlack)
            return;
        _shieldRechargeTimer = 0f;
        Stats.ShieldPoints = Stats.ShieldCapacity;
    }

    private void OfferNextUpgrade()
    {
        while (UpgradeOffer is null && _pendingLevelUps > 0)
        {
            var offer = Upgrades.Offer(Stats, _random, UpgradeChoices);
            if (offer.Count > 0)
            {
                UpgradeOffer = offer;
                return;
            }

            // Nothing left to take: the level is used up without pausing play.
            _pendingLevelUps--;
        }
    }
}
