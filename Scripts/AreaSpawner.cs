using System.Collections.Generic;
using UnityEngine;

// Spawns a number of objects at random spots inside a square area.
//
// The square is centred on this GameObject and lies flat on its local X/Z
// plane, so it moves, rotates and scales with whatever it is attached to.
// Put it on your prefab root (alongside MarkerSpawner/TapToPlace content) and
// the default size of 1 matches the paper (-0.5 to +0.5).
//
// Spawned objects are made children of this object, so they stay attached to
// the marker / placed content.
public class AreaSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [SerializeField] GameObject[] prefabs;      // one is picked at random for each spawn
    [SerializeField] int count = 10;

    [Header("Area (in local units, centred on this object)")]
    [SerializeField] float size = 1f;           // side length of the square
    [SerializeField] float edgeMargin = 0.05f;  // keep this far inside the edge
    [SerializeField] float height = 0f;         // local Y offset for spawned objects

    [Header("Spacing")]
    [SerializeField] float minSpacing = 0.05f;  // 0 = allow overlaps
    [SerializeField] int maxAttemptsPerObject = 30;

    [Header("Variation")]
    [SerializeField] bool randomRotation = true;          // random spin around Y
    [SerializeField] Vector2 scaleRange = new Vector2(1f, 1f);

    [Header("When")]
    [SerializeField] bool spawnOnStart = true;

    readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        if (spawnOnStart) Spawn();
    }

    // Removes anything spawned previously, then spawns a fresh set.
    public void Spawn()
    {
        Clear();

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"{name}: AreaSpawner has no prefabs assigned.", this);
            return;
        }

        float half = Mathf.Max(0f, size * 0.5f - edgeMargin);
        var positions = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            if (!TryFindSpot(half, positions, out Vector3 localPos))
            {
                Debug.LogWarning($"{name}: AreaSpawner could only fit {positions.Count} of {count} " +
                                 "objects. Lower Min Spacing or Count, or increase Size.", this);
                break;
            }
            positions.Add(localPos);

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;

            GameObject obj = Instantiate(prefab, transform);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = randomRotation
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;
            obj.transform.localScale = prefab.transform.localScale *
                                       Random.Range(scaleRange.x, scaleRange.y);
            spawned.Add(obj);
        }
    }

    // Destroys everything this spawner has created.
    public void Clear()
    {
        foreach (var obj in spawned)
            if (obj != null) Destroy(obj);
        spawned.Clear();
    }

    bool TryFindSpot(float half, List<Vector3> taken, out Vector3 pos)
    {
        float minSqr = minSpacing * minSpacing;
        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            pos = new Vector3(Random.Range(-half, half), height, Random.Range(-half, half));
            if (minSpacing <= 0f) return true;

            bool clear = true;
            foreach (var p in taken)
            {
                if ((p - pos).sqrMagnitude < minSqr) { clear = false; break; }
            }
            if (clear) return true;
        }
        pos = default;
        return false;
    }

    // Shows the spawn area in the Scene view.
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, height, 0f), new Vector3(size, 0f, size));
        float inner = Mathf.Max(0f, size - edgeMargin * 2f);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireCube(new Vector3(0f, height, 0f), new Vector3(inner, 0f, inner));
    }
}
