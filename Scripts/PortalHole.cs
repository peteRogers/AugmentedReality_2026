using UnityEngine;

// Turns the marker into a hole you can look down into.
//
// Put this on the ROOT of your prefab (built 1 unit wide, like the others).
// At start it builds:
//   - an invisible frame around the hole (DepthMask material) that hides
//     everything below the paper except what you see through the hole
//   - four walls and a floor so the hole looks like a pit
//
// Put your own scene (cave, mountain, ocean...) as children of the root,
// BELOW y = 0 and inside the hole's footprint.
public class PortalHole : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] Material occluderMaterial;   // a material using AR/DepthMask
    [SerializeField] Material wallMaterial;       // any URP/Lit material
    [SerializeField] Material floorMaterial;      // leave empty to reuse the wall material

    [Header("Size (in prefab units, paper = 1)")]
    [SerializeField] float holeWidth = 1f;
    [SerializeField] float holeDepth = 1f;        // how far the hole runs along the paper (Z)
    [SerializeField] float pitDepth = 0.6f;       // how deep it goes
    [SerializeField] float frameSize = 2f;        // how far the invisible frame reaches outwards
    [SerializeField] bool buildWalls = true;

    const float Thin = 0.002f;

    void Start()
    {
        if (!occluderMaterial)
        {
            var shader = Shader.Find("AR/DepthMask");
            if (shader) occluderMaterial = new Material(shader);
            else { Debug.LogError("PortalHole: assign a material that uses AR/DepthMask."); return; }
        }

        float hx = holeWidth * 0.5f, hz = holeDepth * 0.5f, f = frameSize;

        // Invisible frame: four flat slabs around the hole, at paper level.
        Block("Occluder N", new Vector3(0, -Thin, hz + f / 2), new Vector3(holeWidth + 2 * f, Thin, f), occluderMaterial);
        Block("Occluder S", new Vector3(0, -Thin, -hz - f / 2), new Vector3(holeWidth + 2 * f, Thin, f), occluderMaterial);
        Block("Occluder E", new Vector3(hx + f / 2, -Thin, 0), new Vector3(f, Thin, holeDepth), occluderMaterial);
        Block("Occluder W", new Vector3(-hx - f / 2, -Thin, 0), new Vector3(f, Thin, holeDepth), occluderMaterial);

        if (!buildWalls || !wallMaterial) return;

        // Pit walls sit just outside the hole edge, so they only show through it.
        float y = -pitDepth / 2, t = 0.01f;
        Block("Wall N", new Vector3(0, y, hz + t / 2), new Vector3(holeWidth + 2 * t, pitDepth, t), wallMaterial);
        Block("Wall S", new Vector3(0, y, -hz - t / 2), new Vector3(holeWidth + 2 * t, pitDepth, t), wallMaterial);
        Block("Wall E", new Vector3(hx + t / 2, y, 0), new Vector3(t, pitDepth, holeDepth), wallMaterial);
        Block("Wall W", new Vector3(-hx - t / 2, y, 0), new Vector3(t, pitDepth, holeDepth), wallMaterial);
        Block("Floor", new Vector3(0, -pitDepth - t / 2, 0), new Vector3(holeWidth, t, holeDepth),
              floorMaterial ? floorMaterial : wallMaterial);
    }

    void Block(string name, Vector3 localPos, Vector3 localSize, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localSize;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Shows the hole and pit as a cyan box in the Scene view.
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(0, -pitDepth / 2, 0), new Vector3(holeWidth, pitDepth, holeDepth));
    }
}
