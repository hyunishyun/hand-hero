using HandHero.Core;
using UnityEngine;

// Terrain variety, first slice (round 4, S7 / D8): moves the existing arena pieces
// (the pillars) and the run bot spawn points to a seeded layout (ArenaLayout,
// HandHero.Core) and puts them back where the scene builder placed them. The same
// objects move, so cover, aim assist and raycasts keep working; positions are
// arena-local, so the tabletop scale works too. RunDirector applies a layout at
// every island Intro and restores the defaults when the run ends (Quick Match and
// the tutorial keep today's layout). Nothing here moves the XR Origin.
//
// Terrain stage 2 (round 5, T3 / D5): every island also turns on a seeded number
// of pooled low walls, floating platforms and thin pillars (ArenaPieceTable, by
// island depth), placed under the same rules, and gets a colour theme
// (ArenaThemeApplier). A fallback keeps today's two pillars only. The pools stay
// disabled outside runs.
public class ArenaLayoutApplier : MonoBehaviour
{
    [Tooltip("Arena box size (m, arena-local); the floor is at -y/2")]
    [SerializeField] private Vector3 arenaSize = new Vector3(35f, 20f, 35f);
    [Tooltip("The player hero's start point (arena-local)")]
    [SerializeField] private Vector3 playerStart = Vector3.zero;
    [Tooltip("Terrain pieces that move (children of the arena root); their size is their local scale")]
    [SerializeField] private Transform[] pieces;
    [Tooltip("Run bot spawn points that move (children of the arena root)")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Objects that stay put and that pieces keep off (the greybox targets); their size is their local scale")]
    [SerializeField] private Transform[] keepClear;
    [Tooltip("Fairness rules: margins, clear radii, the spawn area, the platform band (arena-local)")]
    [SerializeField] private ArenaLayoutParams rules = ArenaLayoutParams.Default;

    [Header("Terrain stage 2 (round 5, T3)")]
    [Tooltip("Pooled low walls (children of the arena root, disabled until an island uses them); size = local scale")]
    [SerializeField] private Transform[] lowWalls;
    [Tooltip("Pooled floating platforms (disabled until an island uses them); size = local scale")]
    [SerializeField] private Transform[] platforms;
    [Tooltip("Pooled thin pillars (disabled until an island uses them); size = local scale")]
    [SerializeField] private Transform[] thinPillars;
    [Tooltip("Extra pieces per island depth: the row with the highest From Island at or below the island; counts drawn from the island's layout seed")]
    [SerializeField] private ArenaPieceTier[] pieceTable = ArenaPieceTable.Defaults();
    [Tooltip("Optional: island colour themes; none = today's look on every island")]
    [SerializeField] private ArenaThemeApplier themes;

    private Vector3[] _defaultPieces;
    private Vector3[] _defaultSpawns;
    private Vector3[] _pieceSizes;
    private Vector3[] _keepCenters;
    private Vector3[] _keepSizes;
    private Vector3[] _wallSizes;
    private Vector3[] _platformSizes;
    private Vector3[] _thinSizes;
    private bool _moved;

    // Last applied layout: false when the rules could not be met and the defaults stayed.
    public bool LastUsedFallback { get; private set; }

    private void Awake()
    {
        pieces ??= new Transform[0];
        spawnPoints ??= new Transform[0];
        keepClear ??= new Transform[0];
        lowWalls ??= new Transform[0];
        platforms ??= new Transform[0];
        thinPillars ??= new Transform[0];
        _defaultPieces = LocalPositions(pieces);
        _defaultSpawns = LocalPositions(spawnPoints);
        _pieceSizes = Scales(pieces);
        _keepCenters = LocalPositions(keepClear);
        _keepSizes = Scales(keepClear);
        _wallSizes = Scales(lowWalls);
        _platformSizes = Scales(platforms);
        _thinSizes = Scales(thinPillars);
    }

    // The island's layout, extra pieces and theme (during the island countdown,
    // before bots spawn). The layout seed is ArenaLayout.IslandSeed(runSeed, island),
    // as in round 4.
    public ArenaIslandTerrain ApplyIsland(int runSeed, int island)
    {
        var terrain = new ArenaIslandTerrain
        {
            LayoutSeed = ArenaLayout.IslandSeed(runSeed, island),
            UsedFallback = true,
            Theme = "",
        };
        if (_defaultPieces == null) return terrain;

        var pool = new ArenaPieceCounts(lowWalls.Length, platforms.Length, thinPillars.Length);
        ArenaPieceCounts counts = ArenaPieceTable.Roll(pieceTable, island, terrain.LayoutSeed, pool);
        ArenaPieceTable.Compose(_pieceSizes, _wallSizes, _platformSizes, _thinSizes, counts,
            out Vector3[] sizes, out bool[] floating);
        // No fallback arrays: a fallback is handled here (today's pillars, no extra pieces).
        ArenaLayoutResult layout = ArenaLayout.Generate(arenaSize, playerStart, sizes, spawnPoints.Length, rules,
            terrain.LayoutSeed, null, null, _keepCenters, _keepSizes, floating);
        LastUsedFallback = layout.UsedFallback;
        terrain.UsedFallback = layout.UsedFallback;

        if (layout.UsedFallback)
        {
            Debug.LogWarning($"[ArenaLayoutApplier] Layout seed {terrain.LayoutSeed} met no rules; default layout kept");
            SetLocalPositions(pieces, _defaultPieces);
            SetLocalPositions(spawnPoints, _defaultSpawns);
            ShowPool(lowWalls, 0, null, 0);
            ShowPool(platforms, 0, null, 0);
            ShowPool(thinPillars, 0, null, 0);
        }
        else
        {
            // Same order as ArenaPieceTable.Compose: fixed pieces, walls, platforms, thin pillars.
            SetLocalPositions(pieces, layout.Pieces);
            int next = pieces.Length;
            next = ShowPool(lowWalls, counts.LowWalls, layout.Pieces, next);
            next = ShowPool(platforms, counts.Platforms, layout.Pieces, next);
            ShowPool(thinPillars, counts.ThinPillars, layout.Pieces, next);
            SetLocalPositions(spawnPoints, layout.SpawnPoints);
            terrain.Pieces = counts; // Roll never asks for more than a pool holds
        }

        if (themes != null) terrain.Theme = themes.ApplyForIsland(runSeed, island);
        _moved = true;
        Physics.SyncTransforms();
        HHLog.Info($"[ArenaLayoutApplier] Island {island}: theme {terrain.Theme}, walls {terrain.Pieces.LowWalls}, " +
                   $"platforms {terrain.Pieces.Platforms}, thin pillars {terrain.Pieces.ThinPillars}" +
                   (terrain.UsedFallback ? " (fallback)" : ""));
        return terrain;
    }

    // Today's layout and look again (run end); does nothing when nothing moved.
    public void RestoreDefaults()
    {
        if (!_moved || _defaultPieces == null) return;
        SetLocalPositions(pieces, _defaultPieces);
        SetLocalPositions(spawnPoints, _defaultSpawns);
        ShowPool(lowWalls, 0, null, 0);
        ShowPool(platforms, 0, null, 0);
        ShowPool(thinPillars, 0, null, 0);
        if (themes != null) themes.RestoreDefault();
        _moved = false;
        Physics.SyncTransforms();
    }

    // Turns on the first `count` pool pieces at positions[next..] and turns off the
    // rest; returns the next unread position.
    private static int ShowPool(Transform[] pool, int count, Vector3[] positions, int next)
    {
        for (int i = 0; i < pool.Length; i++)
        {
            bool used = i < count;
            Transform piece = pool[i];
            if (piece != null)
            {
                if (used && positions != null && next < positions.Length) piece.localPosition = positions[next];
                if (piece.gameObject.activeSelf != used) piece.gameObject.SetActive(used);
            }
            if (used) next++;
        }
        return next;
    }

    private static Vector3[] LocalPositions(Transform[] transforms)
    {
        var positions = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            positions[i] = transforms[i] != null ? transforms[i].localPosition : Vector3.zero;
        return positions;
    }

    private static Vector3[] Scales(Transform[] transforms)
    {
        var sizes = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            sizes[i] = transforms[i] != null ? transforms[i].localScale : Vector3.zero;
        return sizes;
    }

    private static void SetLocalPositions(Transform[] transforms, Vector3[] positions)
    {
        int n = Mathf.Min(transforms.Length, positions.Length);
        for (int i = 0; i < n; i++)
            if (transforms[i] != null) transforms[i].localPosition = positions[i];
    }

    // Round 5 (T2-P4): the world boxes (collider bounds) of the terrain pieces
    // that are turned on, for the run bots' dash and hold spots. Writes at most
    // boxes.Length and returns how many. Every terrain piece array belongs here
    // (T3's pools too), or bots may stop inside those pieces.
    public int ObstacleBoxes(Bounds[] boxes)
    {
        int n = AddObstacleBoxes(pieces, boxes, 0);
        // Round 5 T3: the pooled low walls, platforms and thin pillars (only the ones turned on count).
        n = AddObstacleBoxes(lowWalls, boxes, n);
        n = AddObstacleBoxes(platforms, boxes, n);
        return AddObstacleBoxes(thinPillars, boxes, n);
    }

    private static int AddObstacleBoxes(Transform[] from, Bounds[] boxes, int n)
    {
        if (from == null || boxes == null) return n;
        for (int i = 0; i < from.Length && n < boxes.Length; i++)
        {
            Transform piece = from[i];
            if (piece == null || !piece.gameObject.activeInHierarchy) continue;
            if (piece.TryGetComponent(out Collider c) && c.enabled) boxes[n++] = c.bounds;
        }
        return n;
    }
}
