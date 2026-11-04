namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Every enemy type in the game. The four regular types (All) join the spawner one at a time: Gemini from the
/// start, then one more as each of the first three bosses arrives (Hugging Face at 3:00, ChatGPT at 6:00, Claude
/// at 9:00), so the pressure builds over the 12-minute run. Once a type has joined it stays for the rest of the run.
/// Tune the numbers here.
/// For reference, the ship moves at 200 px/s.
/// </summary>
public static class EnemyRoster
{
    /// <summary>
    /// The one owner of the boss schedule. Each regular type after Gemini joins at its paired boss's arrival,
    /// so its ArrivesAtSeconds names the same constant.
    /// </summary>
    public const float GeminiBossArrivesAtSeconds = 3 * 60;
    /// <summary>Autocomplete's arrival (6:00); ChatGPT joins then.</summary>
    public const float AutocompleteArrivesAtSeconds = 6 * 60;
    /// <summary>Hallucination's arrival (9:00); Claude joins then.</summary>
    public const float HallucinationArrivesAtSeconds = 9 * 60;
    /// <summary>The final Claude Boss's arrival (12:00); GameSession.SurvivalSeconds reads it.</summary>
    public const float ClaudeBossArrivesAtSeconds = 12 * 60;

    /// <summary>Fragile and fast; swarms in from 3:00, joining with the Gemini Boss.</summary>
    public static readonly EnemyType HuggingFace = new(
        "Hugging Face", Radius: 14f, HitPoints: 1f, Speed: 160f, Experience: 1,
        DefaultSpawnsPerMinute: 240f, ArrivesAtSeconds: GeminiBossArrivesAtSeconds, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.HuggingFace(14f),
        ContactDamage: 5f);

    /// <summary>The steady baseline chaser; the only regular type until 3:00.</summary>
    public static readonly EnemyType Gemini = new(
        "Gemini", Radius: 20f, HitPoints: 3f, Speed: 90f, Experience: 2,
        DefaultSpawnsPerMinute: 160f, ArrivesAtSeconds: 0f, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.Gemini(20f),
        ContactDamage: 5f);

    /// <summary>Snakes toward the player, harder to line up a shot on; joins at 6:00 with Autocomplete.</summary>
    public static readonly EnemyType ChatGpt = new(
        "ChatGPT", Radius: 22f, HitPoints: 4f, Speed: 85f, Experience: 3,
        DefaultSpawnsPerMinute: 50f, ArrivesAtSeconds: AutocompleteArrivesAtSeconds, SpinSpeed: 1f,
        speed => new Weave(speed, amplitude: 60f, cyclesPerSecond: 0.8f), LogoShapes.ChatGpt(22f),
        ContactDamage: 6f);

    /// <summary>Creeps, then lunges in a straight line; sidestep the lunge. Joins at 9:00 with Hallucination.</summary>
    public static readonly EnemyType Claude = new(
        "Claude", Radius: 20f, HitPoints: 3f, Speed: 60f, Experience: 3,
        DefaultSpawnsPerMinute: 20f, ArrivesAtSeconds: HallucinationArrivesAtSeconds, SpinSpeed: 1.5f,
        speed => new Dash(speed, dashSpeed: 420f, creepSeconds: 1.6f, dashSeconds: 0.45f), LogoShapes.Claude(20f),
        ContactDamage: 6f);

    /// <summary>Base contact damage per tick of every boss, before time scaling.</summary>
    public const float BossContactDamage = 15f;

    /// <summary>
    /// The one owner of how contact damage grows with the clock: base damage x (1 + elapsedMinutes / 10), capped at 2x
    /// from 10:00 and rounded to hundredths. So the base value at 0:00, and twice it at 10:00 and beyond. Every contact
    /// source (enemy, boss, bullet, trail, ring) reads its damage through here.
    /// </summary>
    public static float ScaledContactDamage(float baseDamage, float elapsedSeconds) =>
        MathF.Round(baseDamage * ContactDamageScale(elapsedSeconds), 2);

    /// <summary>The time multiplier on contact damage: 1 + elapsedMinutes / 10, capped at 2x from 10:00.</summary>
    public static float ContactDamageScale(float elapsedSeconds) => MathF.Min(1f + elapsedSeconds / 600f, 2f);

    private const float GeminiBossSpeed = 30f;

    /// <summary>
    /// A giant Gemini (4x size) that crawls toward the player and every 3 seconds throws a regular Gemini
    /// at a random spot near them; the thrown Gemini flies at 4x its speed, then lands and chases normally.
    /// It gets more dangerous as it's worn down:
    /// at 75% health it throws 3 at a time, and they fly twice as fast (8x Gemini speed);
    /// at 50% it throws 5;
    /// at 25% it throws two waves of 5, 0.25 seconds apart, and starts moving like Claude at 4x its own speed.
    /// Five regular Geminis orbit it at 10x Gemini speed as a shield; it takes no damage until all five are dead.
    /// The shield respawns at each phase: 5 again at 75%, 10 at 50%, and 10 orbiting twice as far out at 25%.
    /// It never spawns on the regular schedule; see Bosses.
    /// </summary>
    public static readonly EnemyType GeminiBoss = new(
        "Gemini Boss", Radius: 80f, HitPoints: 40f, Speed: GeminiBossSpeed, Experience: 60,
        DefaultSpawnsPerMinute: 0f, ArrivesAtSeconds: float.PositiveInfinity, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.Gemini(80f),
        CreateAbility: () => ThrowGeminis(flightSpeedMultiplier: 4f),
        Phases:
        [
            new(0.75f, CreateAbility: () => ThrowGeminis(flightSpeedMultiplier: 8f, perWave: 3),
                Shield: GeminiShield(count: 5)),
            new(0.50f, CreateAbility: () => ThrowGeminis(flightSpeedMultiplier: 8f, perWave: 5),
                Shield: GeminiShield(count: 10)),
            new(0.25f,
                CreateAbility: () => ThrowGeminis(flightSpeedMultiplier: 8f, perWave: 5, waves: 2, secondsBetweenWaves: 0.25f),
                CreateMovement: () => Claude.CreateMovement(4f * GeminiBossSpeed),
                Shield: GeminiShield(count: 10, orbitRadius: 2f * GeminiShieldRadius)),
        ],
        Shield: GeminiShield(count: 5),
        ContactDamage: BossContactDamage);

    /// <summary>
    /// Boss arriving at 6:00. Base behaviour alternates a 1.2 s telegraph and a straight charge
    /// at 400 px/s reaching up to 700 px and stopping where the ship was at lock.
    /// At 75% health it charges faster and farther (550 px/s, up to 900 px); at 50% it also fires
    /// rings of slow bullets with an escape gap; at 25% it charges at 700 px/s with a 1.0 s wind-up,
    /// never less, with bullet rings continuing. A charge under way finishes as it started.
    /// Its health bar is notched at 75%, 50% and 25%.
    /// </summary>
    public static readonly EnemyType Autocomplete = new(
        "Autocomplete", Radius: 60f, HitPoints: 60f, Speed: 400f, Experience: 120,
        DefaultSpawnsPerMinute: 0f, ArrivesAtSeconds: float.PositiveInfinity, SpinSpeed: 0f,
        speed => new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: speed, chargeDistance: 700f),
        LogoShapes.Cursor(60f),
        Phases:
        [
            new(0.75f, RetuneMovement: m => ((ChargeAlongLine)m).Retune(chargeSpeed: 550f, chargeDistance: 900f)),
            new(0.50f, CreateAbility: () => new BulletBursts()),
            new(0.25f, RetuneMovement: m => ((ChargeAlongLine)m).Retune(chargeSpeed: 700f, chargeDistance: 900f, telegraphSeconds: 1.0f)),
        ],
        ContactDamage: BossContactDamage);

    /// <summary>
    /// Boss arriving at 9:00. Chases at 70 px/s and splits into 2 look-alike copies every 5 s.
    /// At 75% it splits every 3.5 s; at 50% it makes 3 copies per split and chases at 100 px/s; at 25% it
    /// splits every 2.5 s and moves like Claude Boss, creeping for 1.2 s before each lunge. The split timer
    /// carries over each phase change, so a phase never delays or doubles a split.
    /// </summary>
    public static readonly EnemyType Hallucination = new(
        "Hallucination", Radius: 50f, HitPoints: 80f, Speed: 70f, Experience: 180,
        DefaultSpawnsPerMinute: 0f, ArrivesAtSeconds: float.PositiveInfinity, SpinSpeed: 1f,
        speed => new ChasePlayer(speed), LogoShapes.Perplexity(50f),
        CreateAbility: () => new SplitIntoCopies(secondsBetweenSplits: 5f, copiesPerSplit: 2,
            phaseSchedule: [new(3.5f, 2), new(3.5f, 3), new(2.5f, 3)]),
        Phases:
        [
            new(0.75f),
            new(0.50f, CreateMovement: () => new ChasePlayer(100f)),
            new(0.25f, CreateMovement: () => new Dash(100f, dashSpeed: 420f, creepSeconds: 1.2f, dashSeconds: 0.45f)),
        ],
        ContactDamage: BossContactDamage);

    /// <summary>
    /// Final boss arriving at 12:00. Distinct from regular EnemyRoster.Claude.
    /// Its health bar is notched at 75%, 50% and 25%.
    /// At 75% bullet rings come more often (every 3 s → 2 s) with more bullets (12 → 16), still with an escape gap;
    /// at 50% its closing ring returns sooner (every 10 s → 6 s), shrinks faster (40 → 60 px/s) and lasts 6 s instead
    /// of 8 s, never with two rings out at once, and its lunges get faster (420 → 560 px/s) without restarting its creep;
    /// at 25% it also throws pairs of regular Claudes.
    /// </summary>
    public static readonly EnemyType ClaudeBoss = new(
        "Claude Boss", Radius: 70f, HitPoints: 120f, Speed: 80f, Experience: 250,
        DefaultSpawnsPerMinute: 0f, ArrivesAtSeconds: float.PositiveInfinity, SpinSpeed: 0f,
        speed => new Dash(speed, dashSpeed: 420f, creepSeconds: 1.6f, dashSeconds: 0.45f), LogoShapes.Claude(70f),
        CreateAbility: () => new CompositeAbility(
            new ClosingRing(phaseSchedule: [new RingStep(0.50f, CooldownSeconds: 6f, ContractionSpeed: 60f, LifetimeSeconds: 6f)]),
            new BulletBursts(phaseSchedule: [new BurstStep(2f, 16)]),
            ThrowClaudes()),
        Phases:
        [
            new(0.75f),
            new(0.50f, RetuneMovement: m => { if (m is Dash dash) dash.Retune(dashSpeed: 560f); }),
            new(0.25f),
        ],
        ContactDamage: BossContactDamage);

    private static ThrowMinions ThrowClaudes() =>
        new(Claude, secondsBetweenThrows: 4f, speedMultiplier: 4f, minLandingDistance: 150f, maxLandingDistance: 250f,
            minionsPerWave: 2, firstThrowAfterSeconds: 2f, harmlessInFlight: true,
            gate: owner =>
            {
                if (owner.IsDead)
                    return false;
                var phases = owner.Type.Phases;
                if (phases is null)
                    return false;
                for (var i = 0; i < phases.Count; i++)
                {
                    if (MathF.Abs(phases[i].AtHealthFraction - 0.25f) < 0.001f)
                        return owner.PhasesReached > i;
                }
                return false;
            });

    /// <summary>How far the Gemini Boss's shield orbits from its centre, just clear of its outline.</summary>
    private const float GeminiShieldRadius = 115f;

    private static EnemyShield GeminiShield(int count, float orbitRadius = GeminiShieldRadius) =>
        new(Gemini, count, orbitRadius, SpeedMultiplier: 10f);

    private static ThrowMinions ThrowGeminis(float flightSpeedMultiplier, int perWave = 1, int waves = 1, float secondsBetweenWaves = 0f) =>
        new(Gemini, secondsBetweenThrows: 3f, flightSpeedMultiplier, minLandingDistance: 80f, maxLandingDistance: 180f,
            perWave, waves, secondsBetweenWaves);

    /// <summary>Bosses and when they appear. While any boss is alive, regular spawning pauses.</summary>
    public static IReadOnlyList<BossArrival> Bosses { get; } =
    [
        new(GeminiBossArrivesAtSeconds, GeminiBoss),
        new(AutocompleteArrivesAtSeconds, Autocomplete),
        new(HallucinationArrivesAtSeconds, Hallucination),
        new(ClaudeBossArrivesAtSeconds, ClaudeBoss),
    ];

    /// <summary>Stretches of the clock when regular spawning stops.</summary>
    public static IReadOnlyList<SpawnPause> SpawnPauses { get; } = [new(80f, 90f)];

    /// <summary>
    /// Rings of enemies that appear around the ship. At 1:30, after the lull, 32 Geminis surround it 500 px out:
    /// spaced widely enough that a ship flying straight at the ring can still slip between two of them.
    /// </summary>
    public static IReadOnlyList<RingSpawn> Rings { get; } = [new(90f, Gemini, Count: 32, Radius: 500f)];

    public static IReadOnlyList<EnemyType> All { get; } =
        [HuggingFace, Gemini, ChatGpt, Claude];
}
