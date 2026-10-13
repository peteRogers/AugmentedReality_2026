using UnityEngine;

// Makes an object wander around the marker like a small creature:
// it always walks forward while its heading drifts smoothly left and right
// (driven by Perlin noise), so it traces natural curving paths. Near the
// edge it gently curves back towards the middle. Every so often it stops
// to look around, then sets off again.
//
// Put this on the MODEL (the child inside your prefab), not the prefab root.
// It moves in the root's local space, where the paper runs from -0.5 to +0.5
// (when the prefab is built 1 unit wide and scaled by MarkerSpawner).
public class Wanderer : MonoBehaviour
{
    [Header("Area (in prefab units, paper = -0.5 to 0.5)")]
    [SerializeField] float halfWidth = 0.4f;    // keep a little inside the edge
    [SerializeField] float halfDepth = 0.4f;
    [SerializeField] float edgeMargin = 0.12f;  // start curving away this far from the edge

    [Header("Movement")]
    [SerializeField] float walkSpeed = 0.15f;   // paper widths per second
    [Range(0f, 1f)] [SerializeField] float speedVariation = 0.4f; // how much speed drifts
    [SerializeField] float maxTurnRate = 90f;   // degrees per second at the tightest curve
    [SerializeField] float wanderFrequency = 0.3f; // how often the curve changes direction
    [SerializeField] float edgeTurnRate = 200f; // how hard it steers away from edges

    [Header("Pauses")]
    [SerializeField] Vector2 walkTime = new Vector2(3f, 8f);   // walk this long between pauses
    [Range(0f, 1f)] [SerializeField] float pauseChance = 0.5f;
    [SerializeField] Vector2 pauseTime = new Vector2(0.5f, 2f);

    [Header("Walking bob (optional)")]
    [SerializeField] float bobHeight = 0.01f;
    [SerializeField] float bobRate = 12f;

    [Header("Animator (optional)")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";

    float heading;      // degrees around Y
    float noiseSeed;    // so each creature wanders differently
    float noiseTime;
    float walkTimer;
    float pauseTimer;
    float baseY;
    float bobPhase;
    float currentSpeed;

    void Start()
    {
        baseY = transform.localPosition.y;
        heading = transform.localEulerAngles.y;
        noiseSeed = Random.Range(0f, 1000f);
        walkTimer = Random.Range(walkTime.x, walkTime.y);
    }

    void Update()
    {
        float targetSpeed = 0f;

        if (pauseTimer > 0f)
        {
            // Standing still: occasionally glance around.
            pauseTimer -= Time.deltaTime;
            heading += Mathf.Sin(Time.time * 2f) * 30f * Time.deltaTime;
            if (pauseTimer <= 0f) walkTimer = Random.Range(walkTime.x, walkTime.y);
        }
        else
        {
            noiseTime += Time.deltaTime * wanderFrequency;
            Vector3 pos = transform.localPosition;

            // Smoothly varying turn: noise in -1..1 gives a gentle left/right drift.
            float wander = Mathf.PerlinNoise(noiseSeed, noiseTime) * 2f - 1f;
            float turn = wander * maxTurnRate;

            // Near an edge, blend in a turn back towards the middle.
            float edgeX = Mathf.InverseLerp(halfWidth - edgeMargin, halfWidth, Mathf.Abs(pos.x));
            float edgeZ = Mathf.InverseLerp(halfDepth - edgeMargin, halfDepth, Mathf.Abs(pos.z));
            float edgeAmount = Mathf.Max(edgeX, edgeZ);
            if (edgeAmount > 0f)
            {
                float toCentre = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(heading, toCentre);
                // Only steer if we're actually heading outwards.
                if (Mathf.Abs(diff) > 45f)
                    turn += Mathf.Sign(diff) * edgeTurnRate * edgeAmount;
            }

            heading += turn * Time.deltaTime;

            // Speed drifts a little, and slows on tight turns.
            float speedNoise = Mathf.PerlinNoise(noiseSeed + 50f, noiseTime * 0.7f);
            float turnSlowdown = 1f - 0.5f * Mathf.Clamp01(Mathf.Abs(turn) / (maxTurnRate + edgeTurnRate));
            targetSpeed = walkSpeed * (1f - speedVariation * speedNoise) * turnSlowdown;

            walkTimer -= Time.deltaTime;
            if (walkTimer <= 0f)
            {
                if (Random.value < pauseChance)
                    pauseTimer = Random.Range(pauseTime.x, pauseTime.y);
                else
                    walkTimer = Random.Range(walkTime.x, walkTime.y);
            }
        }

        // Ease in and out of walking rather than snapping.
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, walkSpeed * 2f * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(0f, heading, 0f);

        Vector3 p = transform.localPosition;
        p += transform.localRotation * Vector3.forward * currentSpeed * Time.deltaTime;
        p.x = Mathf.Clamp(p.x, -halfWidth, halfWidth);
        p.z = Mathf.Clamp(p.z, -halfDepth, halfDepth);

        // Little up-and-down bob while moving.
        if (currentSpeed > 0.001f)
        {
            bobPhase += Time.deltaTime * bobRate * (currentSpeed / walkSpeed);
            p.y = baseY + Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight;
        }
        else
        {
            p.y = Mathf.Lerp(p.y, baseY, Time.deltaTime * 10f);
        }
        transform.localPosition = p;

        if (animator) animator.SetFloat(speedParameter, currentSpeed);
    }

    // Shows the wander area as a yellow box in the Scene view,
    // and the edge-avoidance zone as an orange box inside it.
    void OnDrawGizmosSelected()
    {
        if (!transform.parent) return;
        Gizmos.matrix = transform.parent.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, 0.001f, halfDepth * 2f));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3((halfWidth - edgeMargin) * 2f, 0.001f,
                                                      (halfDepth - edgeMargin) * 2f));
    }
}
