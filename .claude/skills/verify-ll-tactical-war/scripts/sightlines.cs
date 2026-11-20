// Read-only sight-line check for verify-ll-tactical-war. Run with:  scripts/verify.sh sightlines
// Sight lines from the real map camera. For every pose the tactics camera takes (the cursor on
// every tile and every half tile between, with CM_Tactic's own offset, pitch and lens, at the Game
// view's aspect), cast a ray to every tile in view: its ground (centre and four inner corners) and a
// figure's chest and head. Anything of the scenery in the way is reported. A tile's own obstacle is
// never counted against it. Scenery is static-batched, so each piece's own mesh is instantiated
// again as a temporary collider for the test and removed after.
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using LLTacticalWar;

string Sight()
{
    var map = TileMap.instance;
    var scenery = map.transform.Find("Scenery");
    var clouds = map.transform.Find("Clouds");
    var rig = GameObject.Find("CM_Tactic");
    var follow = rig.GetComponent<Unity.Cinemachine.CinemachineFollow>();
    var lens = rig.GetComponent<Unity.Cinemachine.CinemachineCamera>().Lens;
    var offset = follow.FollowOffset;
    var rot = rig.transform.rotation;
    float aspect = Camera.main.aspect;
    float tanV = Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * aspect;

    // temporary colliders from each piece's own meshes
    var temp = new GameObject("SightProbe");
    var owner = new Dictionary<Collider, (string name, int tile)>();
    var pieces = new List<Transform>();
    foreach (Transform t in scenery) if (t.name != "Board" && t.name != "Ground") pieces.Add(t);
    if (clouds != null) foreach (Transform t in clouds) pieces.Add(t);
    foreach (var t in pieces)
    {
        var src = Resources.Load<GameObject>(SceneryLayout.Dir + t.name);
        if (src == null) continue;
        var copy = Object.Instantiate(src, t.position, t.rotation, temp.transform);
        copy.transform.localScale = t.lossyScale;
        // a piece belongs to the tile under its mesh's centre (some pack pivots sit off the mesh)
        var mfs = copy.GetComponentsInChildren<MeshFilter>();
        var at = mfs.Select(mf => mf.transform.TransformPoint(mf.sharedMesh.bounds.center)).Aggregate(Vector3.zero, (a, b) => a + b) / mfs.Length;
        bool onBoard = t.parent == scenery && at.x > 0f && at.x < map.tileX * map.tileSize && at.z > 0f && at.z < map.tileZ * map.tileSize;
        int tile = onBoard ? map.WorldToTilePoint(at) : -1;
        foreach (var mf in mfs)
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            owner[mc] = (t.name, tile);
            mf.GetComponent<MeshRenderer>().enabled = false;
        }
    }
    Physics.SyncTransforms();

    // obstacles must sit inside their tile and stay under a figure's height
    var sb = new System.Text.StringBuilder();
    float worstOut = -9f, tallest = 0f;
    foreach (var t in map.tiles.Where(t => t.blocked))
    {
        foreach (var kv in owner.Where(kv => kv.Value.tile == t.index))
        {
            var mf = kv.Key.GetComponent<MeshFilter>();
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = mf.transform.TransformPoint(v);
                float outBy = Mathf.Max(Mathf.Abs(w.x - t.position.x), Mathf.Abs(w.z - t.position.z)) - map.tileSize / 2f;
                worstOut = Mathf.Max(worstOut, outBy);
                tallest = Mathf.Max(tallest, w.y);
            }
        }
    }
    sb.AppendLine("obstacles: " + map.tiles.Count(t => t.blocked) + " tiles, " + owner.Values.Where(o => o.tile >= 0).Select(o => o.tile).Distinct().Count() + " dressed; furthest vertex past its tile edge " + worstOut.ToString("F2") + " (negative = inside), tallest " + tallest.ToString("F2"));

    int poses = 0, rays = 0;
    var offBoard = new Dictionary<string, int>();
    int obstacleBody = 0, obstacleGround = 0;
    var bodyExamples = new List<string>();
    int W = map.tileX * 2 - 1, H = map.tileZ * 2 - 1;
    for (int cz = 0; cz < H; cz++)
        for (int cx = 0; cx < W; cx++)
        {
            var target = new Vector3((cx / 2f + 0.5f) * map.tileSize, 0f, (cz / 2f + 0.5f) * map.tileSize);
            var cam = target + offset;
            var inv = Quaternion.Inverse(rot);
            poses++;
            foreach (var t in map.tiles)
            {
                var c = t.position;
                float s = map.tileSize / 2f - 0.3f;
                var points = new[] { (c + Vector3.up * 0.05f, false), (c + new Vector3(-s, 0.05f, -s), false), (c + new Vector3(s, 0.05f, -s), false), (c + new Vector3(-s, 0.05f, s), false), (c + new Vector3(s, 0.05f, s), false), (c + Vector3.up * 1.0f, true), (c + Vector3.up * 1.7f, true) };
                foreach (var (p, body) in points)
                {
                    var local = inv * (p - cam);
                    if (local.z < 0.3f || Mathf.Abs(local.y / local.z) > tanV || Mathf.Abs(local.x / local.z) > tanH) continue;
                    if (body && t.blocked) continue;
                    rays++;
                    var d = p - cam;
                    if (!Physics.Raycast(cam, d.normalized, out var hit, d.magnitude - 0.02f)) continue;
                    if (!owner.TryGetValue(hit.collider, out var o)) continue;
                    if (o.tile == t.index) continue;
                    if (o.tile < 0) { offBoard.TryGetValue(o.name, out var n); offBoard[o.name] = n + 1; }
                    else if (body) { obstacleBody++; if (bodyExamples.Count < 5) bodyExamples.Add("cam over (" + (cx / 2f) + "," + (cz / 2f) + ") -> tile (" + t.x + "," + t.z + ") y" + p.y + " by " + o.name + "@" + o.tile); }
                    else obstacleGround++;
                }
            }
        }
    Object.DestroyImmediate(temp);
    Physics.SyncTransforms();
    sb.AppendLine("camera: offset " + offset + " pitch " + rot.eulerAngles.x.ToString("F1") + " fov " + lens.FieldOfView + " aspect " + aspect.ToString("F3") + "  poses " + poses + "  rays " + rays);
    sb.AppendLine("blocked by off-board scenery: " + (offBoard.Count == 0 ? "none" : string.Join(", ", offBoard.Select(kv => kv.Key + " x" + kv.Value))));
    sb.AppendLine("figures hidden by another tile's obstacle: " + obstacleBody + (bodyExamples.Count > 0 ? "  e.g. " + string.Join("; ", bodyExamples) : ""));
    sb.AppendLine("ground points behind another tile's obstacle: " + obstacleGround);
    return sb.ToString();
}

Sight()
