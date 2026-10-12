namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Every enemy type in the game, roughly weakest to toughest. Tougher types are worth more experience and
/// arrive later in the 10-minute run, so the pressure builds over time. Tune the numbers here.
/// For reference, the ship moves at 200 px/s.
/// </summary>
public static class EnemyRoster
{
    /// <summary>Fragile and fast; arrives in swarms early on.</summary>
    public static readonly EnemyType HuggingFace = new(
        "Hugging Face", Radius: 14f, HitPoints: 1f, Speed: 160f, Experience: 1,
        DefaultSpawnsPerMinute: 24f, ArrivesAtSeconds: 30f, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.HuggingFace(14f));

    /// <summary>The steady baseline chaser.</summary>
    public static readonly EnemyType Gemini = new(
        "Gemini", Radius: 20f, HitPoints: 3f, Speed: 90f, Experience: 2,
        DefaultSpawnsPerMinute: 30f, ArrivesAtSeconds: 0f, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.Gemini(20f));

    /// <summary>Snakes toward the player, harder to line up a shot on.</summary>
    public static readonly EnemyType ChatGpt = new(
        "ChatGPT", Radius: 22f, HitPoints: 4f, Speed: 85f, Experience: 3,
        DefaultSpawnsPerMinute: 14f, ArrivesAtSeconds: 60f, SpinSpeed: 1f,
        speed => new Weave(speed, amplitude: 60f, cyclesPerSecond: 0.8f), LogoShapes.ChatGpt(22f));

    /// <summary>Creeps, then lunges in a straight line; sidestep the lunge.</summary>
    public static readonly EnemyType Claude = new(
        "Claude", Radius: 20f, HitPoints: 3f, Speed: 60f, Experience: 3,
        DefaultSpawnsPerMinute: 10f, ArrivesAtSeconds: 90f, SpinSpeed: 1.5f,
        speed => new Dash(speed, dashSpeed: 420f, creepSeconds: 1.6f, dashSeconds: 0.45f), LogoShapes.Claude(20f));

    /// <summary>Fast, circling inward in a tight spiral.</summary>
    public static readonly EnemyType Grok = new(
        "Grok", Radius: 20f, HitPoints: 4f, Speed: 130f, Experience: 4,
        DefaultSpawnsPerMinute: 10f, ArrivesAtSeconds: 120f, SpinSpeed: 0f,
        speed => new SpiralIn(speed, offsetDegrees: 60f), LogoShapes.Grok(20f));

    /// <summary>A sturdier weaver with a quick, narrow sway.</summary>
    public static readonly EnemyType Copilot = new(
        "Copilot", Radius: 22f, HitPoints: 6f, Speed: 80f, Experience: 5,
        DefaultSpawnsPerMinute: 8f, ArrivesAtSeconds: 180f, SpinSpeed: 0f,
        speed => new Weave(speed, amplitude: 25f, cyclesPerSecond: 2f), LogoShapes.Copilot(22f));

    /// <summary>Starts slow but keeps accelerating; kill it early.</summary>
    public static readonly EnemyType Cursor = new(
        "Cursor", Radius: 22f, HitPoints: 8f, Speed: 40f, Experience: 7,
        DefaultSpawnsPerMinute: 6f, ArrivesAtSeconds: 240f, SpinSpeed: 0.6f,
        speed => new Accelerate(speed, acceleration: 18f, maxSpeed: 190f), LogoShapes.Cursor(22f));

    /// <summary>Creeps, then teleports a chunk closer every couple of seconds.</summary>
    public static readonly EnemyType Perplexity = new(
        "Perplexity", Radius: 22f, HitPoints: 7f, Speed: 40f, Experience: 6,
        DefaultSpawnsPerMinute: 6f, ArrivesAtSeconds: 300f, SpinSpeed: 0f,
        speed => new Blink(speed, jumpDistance: 90f, secondsBetweenJumps: 1.75f), LogoShapes.Perplexity(22f));

    /// <summary>Tough and spinning, closing in on a wide spiral.</summary>
    public static readonly EnemyType AppleIntelligence = new(
        "Apple Intelligence", Radius: 26f, HitPoints: 10f, Speed: 75f, Experience: 9,
        DefaultSpawnsPerMinute: 4f, ArrivesAtSeconds: 360f, SpinSpeed: 0.8f,
        speed => new SpiralIn(speed, offsetDegrees: 40f), LogoShapes.AppleIntelligence(26f));

    /// <summary>A slow, huge tank that soaks up damage.</summary>
    public static readonly EnemyType Meta = new(
        "Meta", Radius: 32f, HitPoints: 20f, Speed: 50f, Experience: 15,
        DefaultSpawnsPerMinute: 3f, ArrivesAtSeconds: 420f, SpinSpeed: 0f,
        speed => new ChasePlayer(speed), LogoShapes.Meta(32f));

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
        "Gemini Boss", Radius: 80f, HitPoints: 40f, Speed: GeminiBossSpeed, Experience: 30,
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
        Shield: GeminiShield(count: 5));

    /// <summary>How far the Gemini Boss's shield orbits from its centre, just clear of its outline.</summary>
    private const float GeminiShieldRadius = 115f;

    private static EnemyShield GeminiShield(int count, float orbitRadius = GeminiShieldRadius) =>
        new(Gemini, count, orbitRadius, SpeedMultiplier: 10f);

    private static ThrowMinions ThrowGeminis(float flightSpeedMultiplier, int perWave = 1, int waves = 1, float secondsBetweenWaves = 0f) =>
        new(Gemini, secondsBetweenThrows: 3f, flightSpeedMultiplier, minLandingDistance: 80f, maxLandingDistance: 180f,
            perWave, waves, secondsBetweenWaves);

    /// <summary>Bosses and when they appear. While any boss is alive, regular spawning pauses.</summary>
    public static IReadOnlyList<BossArrival> Bosses { get; } = [new(180f, GeminiBoss)];

    /// <summary>Stretches of the clock when regular spawning stops.</summary>
    public static IReadOnlyList<SpawnPause> SpawnPauses { get; } = [new(80f, 90f)];

    /// <summary>
    /// Rings of enemies that appear around the ship. At 1:30, after the lull, 16 Geminis surround it 500 px out:
    /// spaced widely enough that a ship flying straight at the ring can still slip between two of them.
    /// </summary>
    public static IReadOnlyList<RingSpawn> Rings { get; } = [new(90f, Gemini, Count: 16, Radius: 500f)];

    public static IReadOnlyList<EnemyType> All { get; } =
        [HuggingFace, Gemini, ChatGpt, Claude, Grok, Copilot, Cursor, Perplexity, AppleIntelligence, Meta];
}
