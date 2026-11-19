using System.Collections.Generic;
using LESurvivors.Core;
using UnityEngine;

namespace LESurvivors.Mod.Game;

/// <summary>Lightweight pooled pickups (XP motes, orbs, caches). Plain Unity primitives with no colliders or physics.</summary>
internal sealed class Pickups
{
    private sealed class P { public GameObject Go; public Drop Drop; public bool Magnet; public float Bob; }
    private readonly List<P> _live = new();
    private readonly Stack<GameObject> _pool = new();
    private GameObject _root;
    private static Material _mat;
    public int Count => _live.Count;

    private static Color ColourOf(Drop d) => d.Kind switch
    {
        DropKind.XpMote => d.Value >= 25 ? new Color(1f, 0.35f, 0.35f) : d.Value >= 5 ? new Color(0.35f, 1f, 0.5f) : new Color(0.35f, 0.7f, 1f),
        DropKind.HealthOrb => new Color(1f, 0.15f, 0.2f),
        DropKind.Lodestone => new Color(0.8f, 0.8f, 0.9f),
        DropKind.TimeFracture => new Color(0.6f, 0.9f, 1f),
        DropKind.Cache => d.Rarity switch { Rarity.Legendary => new Color(1f, .55f, .17f), Rarity.Exalted => new Color(.7f, .36f, 1f), Rarity.Rare => new Color(.95f, .82f, .3f), Rarity.Magic => new Color(.3f, .55f, 1f), _ => Color.white },
        _ => new Color(1f, 0.9f, 0.4f),
    };

    private static float SizeOf(Drop d) => d.Kind switch
    {
        DropKind.XpMote => d.Value >= 25 ? 0.5f : d.Value >= 5 ? 0.38f : 0.28f,
        DropKind.Cache => 0.9f,
        _ => 0.55f,
    };

    private GameObject Make()
    {
        if (_root == null) { _root = new GameObject("LESV_Pickups"); Object.DontDestroyOnLoad(_root); }
        if (_pool.Count > 0) { var g = _pool.Pop(); g.SetActive(true); return g; }
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_root.transform, true);
        var r = go.GetComponent<Renderer>();
        if (_mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            _mat = sh != null ? new Material(sh) : r.material;
        }
        r.sharedMaterial = _mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    public void Spawn(Drop d, Vector3 at)
    {
        var go = Make();
        go.transform.position = at + Vector3.up * 0.6f + new Vector3(Random.Range(-0.6f, 0.6f), 0, Random.Range(-0.6f, 0.6f));
        go.transform.localScale = Vector3.one * SizeOf(d);
        var mpb = new MaterialPropertyBlock();
        var c = ColourOf(d);
        mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c);
        go.GetComponent<Renderer>().SetPropertyBlock(mpb);
        _live.Add(new P { Go = go, Drop = d, Bob = Random.Range(0, 6.28f) });
    }

    /// <summary>Moves magnetised pickups toward the player and returns the ones collected this frame.</summary>
    public List<Drop> Update(Vector3 player, float pickupRadius, float dt, bool lodestone)
    {
        var got = new List<Drop>();
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var p = _live[i];
            if (p.Go == null) { _live.RemoveAt(i); continue; }
            var pos = p.Go.transform.position;
            var flat = new Vector3(player.x - pos.x, 0, player.z - pos.z);
            float dist = flat.magnitude;
            if (lodestone && p.Drop.Kind == DropKind.XpMote) p.Magnet = true;
            bool autoMagnet = p.Drop.Kind is DropKind.XpMote or DropKind.Shards;
            if (autoMagnet && dist < pickupRadius) p.Magnet = true;
            if (p.Magnet)
            {
                float speed = 9f + (20f - Mathf.Min(dist, 20f));
                pos += flat.normalized * Mathf.Min(dist, speed * dt);
            }
            p.Bob += dt * 3f;
            pos.y = player.y + 0.6f + Mathf.Sin(p.Bob) * 0.12f;
            p.Go.transform.position = pos;
            if (dist < 1.1f) { got.Add(p.Drop); Recycle(p); _live.RemoveAt(i); }
        }
        return got;
    }

    private void Recycle(P p) { if (p.Go != null) { p.Go.SetActive(false); _pool.Push(p.Go); } }

    public void Clear()
    {
        foreach (var p in _live) Recycle(p);
        _live.Clear();
    }

    public void Destroy()
    {
        Clear();
        if (_root != null) Object.Destroy(_root);
        _root = null; _pool.Clear();
    }
}
