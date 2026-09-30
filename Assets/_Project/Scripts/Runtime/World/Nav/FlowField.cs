using UnityEngine;

namespace ZombieWar.WorldNav
{
    /// <summary>
    /// Flow field toward one target over a square window of the world (2026-10-01).
    ///
    /// Basins full of water, lava or toxic are obstacles (colliders on the NavObstacle layer). A
    /// horde cannot steer around a 30 m river by probing ahead; it needs to know where the bridges
    /// are. So one field is solved for everyone: a Dijkstra walk from the target over a 0.5 m grid
    /// gives every cell its path distance, and an enemy just moves toward its cheapest neighbour.
    /// One solve costs the same for 10 enemies as for 300.
    ///
    ///  - <see cref="Rasterize"/> marks obstacle cells from the colliders inside the window. Call it
    ///    when the window moves (the world streams), not every frame.
    ///  - <see cref="Solve"/> refreshes the distances; a few times a second is enough.
    ///  - <see cref="Direction"/> is the per-enemy query: a table lookup, no physics.
    /// Cells next to an obstacle cost a little more, so paths keep off the banks and cross a bridge
    /// down its middle. Everything is allocated once.
    /// </summary>
    public sealed class FlowField
    {
        public readonly int Size;             // cells per side
        public readonly float CellSize;
        public Vector3 Origin { get; private set; }   // world position of cell (0,0)'s corner
        public bool HasSolution { get; private set; }

        readonly LayerMask _mask;
        readonly bool[] _blocked;
        readonly float[] _extra;              // bank cost
        readonly float[] _cost;               // path distance to the target, metres
        readonly int[] _heap;
        readonly int[] _heapPos;
        int _heapCount;
        readonly Collider[] _hits;

        const float Unreached = float.MaxValue;
        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly float[] Step = { 1f, 1f, 1f, 1f, 1.41421f, 1.41421f, 1.41421f, 1.41421f };

        public FlowField(float windowMetres, float cellSize, LayerMask obstacleMask, int maxColliders = 4096)
        {
            CellSize = cellSize;
            Size = Mathf.CeilToInt(windowMetres / cellSize);
            _mask = obstacleMask;
            int n = Size * Size;
            _blocked = new bool[n];
            _extra = new float[n];
            _cost = new float[n];
            _heap = new int[n];
            _heapPos = new int[n];
            _hits = new Collider[maxColliders];
        }

        /// Re-reads the obstacles in a window centred on <paramref name="centre"/>.
        public void Rasterize(Vector3 centre)
        {
            float half = Size * CellSize * 0.5f;
            Origin = new Vector3(Mathf.Floor((centre.x - half) / CellSize) * CellSize, 0f, Mathf.Floor((centre.z - half) / CellSize) * CellSize);
            System.Array.Clear(_blocked, 0, _blocked.Length);
            System.Array.Clear(_extra, 0, _extra.Length);
            HasSolution = false;

            var boxCentre = new Vector3(Origin.x + half, 0f, Origin.z + half);
            int count = Physics.OverlapBoxNonAlloc(boxCentre, new Vector3(half, 50f, half), _hits, Quaternion.identity, _mask, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < count; h++)
            {
                // Obstacle boxes are axis-aligned in the world (tiles bake their rotation in), so the
                // bounds are the box.
                Bounds b = _hits[h].bounds;
                int i0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - Origin.x) / CellSize + 0.01f));
                int i1 = Mathf.Min(Size - 1, Mathf.CeilToInt((b.max.x - Origin.x) / CellSize - 0.01f) - 1);
                int j0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - Origin.z) / CellSize + 0.01f));
                int j1 = Mathf.Min(Size - 1, Mathf.CeilToInt((b.max.z - Origin.z) / CellSize - 0.01f) - 1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        _blocked[j * Size + i] = true;
            }

            // Bank cost: 1.5 m of margin, highest right at the edge.
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    if (!_blocked[j * Size + i]) continue;
                    for (int dj = -3; dj <= 3; dj++)
                        for (int di = -3; di <= 3; di++)
                        {
                            int ni = i + di, nj = j + dj;
                            if (ni < 0 || nj < 0 || ni >= Size || nj >= Size) continue;
                            int k = nj * Size + ni;
                            if (_blocked[k]) continue;
                            float d = Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj));
                            _extra[k] = Mathf.Max(_extra[k], (4f - d) * 0.35f);
                        }
                }
        }

        public bool Contains(Vector3 p)
        {
            float x = (p.x - Origin.x) / CellSize, z = (p.z - Origin.z) / CellSize;
            return x >= 0f && z >= 0f && x < Size && z < Size;
        }

        int CellOf(Vector3 p)
        {
            int i = Mathf.Clamp((int)((p.x - Origin.x) / CellSize), 0, Size - 1);
            int j = Mathf.Clamp((int)((p.z - Origin.z) / CellSize), 0, Size - 1);
            return j * Size + i;
        }

        Vector3 CentreOf(int k) => new(Origin.x + (k % Size + 0.5f) * CellSize, 0f, Origin.z + (k / Size + 0.5f) * CellSize);

        public bool IsBlocked(Vector3 p) => Contains(p) && _blocked[CellOf(p)];

        /// Path distances from every cell to <paramref name="target"/>.
        public void Solve(Vector3 target)
        {
            for (int k = 0; k < _cost.Length; k++) _cost[k] = Unreached;
            _heapCount = 0;
            int start = CellOf(target);
            if (_blocked[start]) start = NearestFree(start);
            if (start < 0) { HasSolution = false; return; }
            _cost[start] = 0f;
            Push(start);
            while (_heapCount > 0)
            {
                int k = Pop();
                int i = k % Size, j = k / Size;
                float c = _cost[k];
                for (int d = 0; d < 8; d++)
                {
                    int ni = i + DX[d], nj = j + DZ[d];
                    if (ni < 0 || nj < 0 || ni >= Size || nj >= Size) continue;
                    int nk = nj * Size + ni;
                    if (_blocked[nk]) continue;
                    // No corner cutting: a diagonal step needs both side cells open.
                    if (d >= 4 && (_blocked[j * Size + ni] || _blocked[nj * Size + i])) continue;
                    float nc = c + (Step[d] + _extra[nk]) * CellSize;
                    if (nc >= _cost[nk]) continue;
                    bool queued = _cost[nk] != Unreached;
                    _cost[nk] = nc;
                    if (queued) Up(_heapPos[nk]); else Push(nk);
                }
            }
            HasSolution = true;
        }

        /// Which way to walk from <paramref name="p"/>: toward the cheapest neighbour cell. Zero when
        /// the field has no answer here (outside the window, or unreachable); the caller then falls
        /// back to heading straight for the target.
        public Vector3 Direction(Vector3 p)
        {
            if (!HasSolution || !Contains(p)) return Vector3.zero;
            int k = CellOf(p);
            if (_blocked[k]) { k = NearestFree(k); if (k < 0) return Vector3.zero; return (CentreOf(k) - p).WithY0().normalized; }
            float here = _cost[k];
            if (here == Unreached) return Vector3.zero;
            int i = k % Size, j = k / Size;
            int best = -1; float bestCost = here;
            for (int d = 0; d < 8; d++)
            {
                int ni = i + DX[d], nj = j + DZ[d];
                if (ni < 0 || nj < 0 || ni >= Size || nj >= Size) continue;
                int nk = nj * Size + ni;
                if (_blocked[nk] || _cost[nk] >= bestCost) continue;
                if (d >= 4 && (_blocked[j * Size + ni] || _blocked[nj * Size + i])) continue;
                best = nk; bestCost = _cost[nk];
            }
            if (best < 0) return Vector3.zero;       // this is the target's cell
            return (CentreOf(best) - p).WithY0().normalized;
        }

        int NearestFree(int k)
        {
            int i0 = k % Size, j0 = k / Size;
            for (int r = 1; r < 12; r++)
                for (int dj = -r; dj <= r; dj++)
                    for (int di = -r; di <= r; di++)
                    {
                        if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                        int i = i0 + di, j = j0 + dj;
                        if (i < 0 || j < 0 || i >= Size || j >= Size) continue;
                        if (!_blocked[j * Size + i]) return j * Size + i;
                    }
            return -1;
        }

        // ── binary min-heap on _cost

        void Push(int k) { _heap[_heapCount] = k; _heapPos[k] = _heapCount; _heapCount++; Up(_heapCount - 1); }

        int Pop()
        {
            int top = _heap[0];
            _heapCount--;
            if (_heapCount > 0) { _heap[0] = _heap[_heapCount]; _heapPos[_heap[0]] = 0; Down(0); }
            return top;
        }

        void Up(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_cost[_heap[p]] <= _cost[_heap[i]]) break;
                Swap(i, p); i = p;
            }
        }

        void Down(int i)
        {
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < _heapCount && _cost[_heap[l]] < _cost[_heap[m]]) m = l;
                if (r < _heapCount && _cost[_heap[r]] < _cost[_heap[m]]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
        }

        void Swap(int a, int b)
        {
            (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
            _heapPos[_heap[a]] = a; _heapPos[_heap[b]] = b;
        }
    }

    static class FlowFieldVectorExt
    {
        public static Vector3 WithY0(this Vector3 v) { v.y = 0f; return v; }
    }
}
