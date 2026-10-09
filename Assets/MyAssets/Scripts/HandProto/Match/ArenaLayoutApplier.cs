using HandHero.Core;
using UnityEngine;

// Terrain variety, first slice (round 4, S7 / D8): moves the existing arena pieces
// (the pillars) and the run bot spawn points to a seeded layout (ArenaLayout,
// HandHero.Core) and puts them back where the scene builder placed them. The same
// objects move, so cover, aim assist and raycasts keep working; positions are
// arena-local, so the tabletop scale works too. RunDirector applies a layout at
// every island Intro and restores the defaults when the run ends (Quick Match and
// the tutorial keep today's layout). Nothing here moves the XR Origin.
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
    [Tooltip("Fairness rules: margins, clear radii, the spawn area (arena-local)")]
    [SerializeField] private ArenaLayoutParams rules = ArenaLayoutParams.Default;

    private Vector3[] _defaultPieces;
    private Vector3[] _defaultSpawns;
    private Vector3[] _pieceSizes;
    private Vector3[] _keepCenters;
    private Vector3[] _keepSizes;
    private bool _moved;

    // Last applied layout: false when the rules could not be met and the defaults stayed.
    public bool LastUsedFallback { get; private set; }

    private void Awake()
    {
        pieces ??= new Transform[0];
        spawnPoints ??= new Transform[0];
        keepClear ??= new Transform[0];
        _defaultPieces = LocalPositions(pieces);
        _defaultSpawns = LocalPositions(spawnPoints);
        _pieceSizes = new Vector3[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
            _pieceSizes[i] = pieces[i] != null ? pieces[i].localScale : Vector3.zero;
        _keepCenters = LocalPositions(keepClear);
        _keepSizes = new Vector3[keepClear.Length];
        for (int i = 0; i < keepClear.Length; i++)
            _keepSizes[i] = keepClear[i] != null ? keepClear[i].localScale : Vector3.zero;
    }

    // Moves the pieces and spawn points to the layout for this seed (during the
    // island countdown, before bots spawn). Returns false when it fell back to the
    // default layout.
    public bool Apply(int seed)
    {
        if (_defaultPieces == null) return false;
        ArenaLayoutResult layout = ArenaLayout.Generate(arenaSize, playerStart, _pieceSizes, spawnPoints.Length, rules,
            seed, _defaultPieces, _defaultSpawns, _keepCenters, _keepSizes);
        LastUsedFallback = layout.UsedFallback;
        if (layout.UsedFallback)
            Debug.LogWarning($"[ArenaLayoutApplier] Layout seed {seed} met no rules; default layout kept");
        SetLocalPositions(pieces, layout.Pieces);
        SetLocalPositions(spawnPoints, layout.SpawnPoints);
        _moved = true;
        Physics.SyncTransforms();
        return !layout.UsedFallback;
    }

    // Today's layout again (run end); does nothing when nothing moved.
    public void RestoreDefaults()
    {
        if (!_moved || _defaultPieces == null) return;
        SetLocalPositions(pieces, _defaultPieces);
        SetLocalPositions(spawnPoints, _defaultSpawns);
        _moved = false;
        Physics.SyncTransforms();
    }

    private static Vector3[] LocalPositions(Transform[] transforms)
    {
        var positions = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            positions[i] = transforms[i] != null ? transforms[i].localPosition : Vector3.zero;
        return positions;
    }

    private static void SetLocalPositions(Transform[] transforms, Vector3[] positions)
    {
        int n = Mathf.Min(transforms.Length, positions.Length);
        for (int i = 0; i < n; i++)
            if (transforms[i] != null) transforms[i].localPosition = positions[i];
    }
}
