using System.Collections.Generic;

namespace LLTacticalWar
{
    // A* over the tile grid, ported from the 2015 PathFinder. Fixes:
    //  - a search that found nothing used to mark the destination unreachable forever;
    //  - GetSmallestFValue ran Mathf.Min on an empty list when the open list emptied;
    //  - the returned path is now start -> destination instead of reversed.
    public static class PathFinder
    {
        const int baseMovementCost = 1;

        public static List<TileMapInfo> FindPath(TileMapInfo start, TileMapInfo destination, ICollection<TileMapInfo> allowed = null)
        {
            var path = new List<TileMapInfo>();
            if (start == null || destination == null) return path;
            if (start == destination) { path.Add(start); return path; }

            var open = new List<TileMapInfo> { start };
            var closed = new HashSet<TileMapInfo>();
            var g = new Dictionary<TileMapInfo, int> { [start] = 0 };
            var f = new Dictionary<TileMapInfo, int> { [start] = start.Distance(destination) };
            var parent = new Dictionary<TileMapInfo, TileMapInfo>();

            while (open.Count > 0)
            {
                var current = open[0];
                for (int i = 1; i < open.Count; i++)
                    if (f[open[i]] < f[current]) current = open[i];

                if (current == destination)
                {
                    var t = destination;
                    while (t != null)
                    {
                        path.Add(t);
                        parent.TryGetValue(t, out t);
                    }
                    path.Reverse();
                    return path;
                }

                open.Remove(current);
                closed.Add(current);

                foreach (var next in current.Neighbours)
                {
                    if (next == null || closed.Contains(next)) continue;
                    if (next != destination && !next.Reachable) continue;
                    if (next == destination && next.blocked) continue;
                    if (allowed != null && !allowed.Contains(next)) continue;

                    int cost = g[current] + baseMovementCost;
                    if (!g.TryGetValue(next, out int old) || cost < old)
                    {
                        g[next] = cost;
                        f[next] = cost + next.Distance(destination);
                        parent[next] = current;
                        if (!open.Contains(next)) open.Add(next);
                    }
                }
            }
            return path; // empty: no route
        }
    }
}
