using System.Collections.Generic;
using UnityEngine;

// Same as Wanderer, but creatures also keep out of each other's way.
// Each one wanders around its own spot like a small creature: it always walks
// forward while its heading drifts smoothly left and right (driven by Perlin
// noise), curves back near the edge of its area, and stops now and then to
// look around. On top of that, it notices other AvoidingWanderers on the same
// plane and steers away from any that get within its personal space. If two
// do end up overlapping, they are gently pushed apart.
//
// Put this on the MODEL (the child inside your prefab), not the prefab root.
// It moves in the root's local space, where the paper runs from -0.5 to +0.5
// (when the prefab is built 1 unit wide and scaled by MarkerSpawner/TapToPlace).
// Avoidance distances are in METRES, because neighbours can have different
// roots and scales.
public class AvoidingWanderer : MonoBehaviour
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

    [Header("Avoiding others (in metres)")]
    [SerializeField] float avoidRadius = 0.08f;     // start steering away this close
    [SerializeField] float minDistance = 0.03f;     // never get closer than this
    [SerializeField] float avoidTurnRate = 250f;    // how hard it steers away from others
    [SerializeField] float samePlaneTolerance = 0.05f; // max height difference to count as "same plane"

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

    // Every active creature, so each one can find its neighbours.
    static readonly List<AvoidingWanderer> all = new List<AvoidingWanderer>();

    float heading;      // degrees around Y
    float noiseSeed;    // so each creature wanders differently
    float noiseTime;
    float walkTimer;
    float pauseTimer;
    float baseY;
    float bobPhase;
    float currentSpeed;

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

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

        // Which way to go to get away from nearby creatures, and how urgently (0..1).
        Vector3 away = AvoidDirection(out float crowding);

        if (pauseTimer > 0f)
        {
            // Standing still: occasionally glance around.
            pauseTimer -= Time.deltaTime;
            heading += Mathf.Sin(Time.time * 2f) * 30f * Time.deltaTime;
            // Someone's right on top of us: stop pausing and move off.
            if (crowding > 0.5f) pauseTimer = 0f;
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

            // Near another creature, blend in a turn away from it.
            float headingOnCollision = 0f;
            if (crowding > 0f)
            {
                float awayHeading = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(heading, awayHeading);
                // Only steer if we're heading towards them (they're in front of us).
                if (Mathf.Abs(diff) > 60f)
                {
                    turn += Mathf.Sign(diff) * avoidTurnRate * crowding;
                    headingOnCollision = Mathf.InverseLerp(60f, 180f, Mathf.Abs(diff));
                }
            }

            heading += turn * Time.deltaTime;

            // Speed drifts a little, slows on tight turns, and slows when about to bump into someone.
            float speedNoise = Mathf.PerlinNoise(noiseSeed + 50f, noiseTime * 0.7f);
            float turnSlowdown = 1f - 0.5f * Mathf.Clamp01(Mathf.Abs(turn) / (maxTurnRate + edgeTurnRate + avoidTurnRate));
            float crowdSlowdown = 1f - 0.7f * crowding * headingOnCollision;
            targetSpeed = walkSpeed * (1f - speedVariation * speedNoise) * turnSlowdown * crowdSlowdown;

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
        p = PushApart(p);
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

    Vector3 PlaneUp => transform.parent ? transform.parent.up : Vector3.up;

    // Is the other creature standing on the same plane as us?
    bool OnSamePlane(AvoidingWanderer other, Vector3 up)
    {
        float heightDiff = Vector3.Dot(other.transform.position - transform.position, up);
        return Mathf.Abs(heightDiff) <= samePlaneTolerance;
    }

    // Adds up "get away" pushes from every neighbour within avoidRadius,
    // stronger the closer they are. Returns the direction in the root's local
    // space (flat, y = 0) and how crowded we are (0 = nobody near, 1 = touching).
    Vector3 AvoidDirection(out float crowding)
    {
        crowding = 0f;
        Vector3 up = PlaneUp;
        Vector3 sum = Vector3.zero;

        foreach (var other in all)
        {
            if (other == this || !OnSamePlane(other, up)) continue;

            Vector3 offset = Vector3.ProjectOnPlane(transform.position - other.transform.position, up);
            float dist = offset.magnitude;
            if (dist >= avoidRadius) continue;

            float strength = 1f - Mathf.InverseLerp(minDistance, avoidRadius, dist);
            // Exactly on top of each other: pick any direction.
            Vector3 dir = dist > 0.0001f ? offset / dist : Vector3.ProjectOnPlane(Random.onUnitSphere, up).normalized;
            sum += dir * strength;
            crowding = Mathf.Max(crowding, strength);
        }

        if (crowding <= 0f) return Vector3.zero;
        Vector3 local = transform.parent ? transform.parent.InverseTransformDirection(sum) : sum;
        local.y = 0f;
        return local.normalized;
    }

    // If anyone is closer than minDistance, slide half the overlap away from
    // them (they do the other half). Takes and returns a local position.
    Vector3 PushApart(Vector3 localPos)
    {
        Transform root = transform.parent;
        Vector3 world = root ? root.TransformPoint(localPos) : localPos;
        Vector3 up = PlaneUp;
        Vector3 push = Vector3.zero;

        foreach (var other in all)
        {
            if (other == this || !OnSamePlane(other, up)) continue;

            Vector3 offset = Vector3.ProjectOnPlane(world - other.transform.position, up);
            float dist = offset.magnitude;
            if (dist >= minDistance || dist < 0.0001f) continue;
            push += offset / dist * (minDistance - dist) * 0.5f;
        }

        if (push == Vector3.zero) return localPos;
        world += push;
        Vector3 result = root ? root.InverseTransformPoint(world) : world;
        result.y = localPos.y;
        return result;
    }

    // Shows the wander area as a yellow box in the Scene view,
    // the edge-avoidance zone as an orange box inside it,
    // and personal space (avoidRadius / minDistance) as cyan / red circles.
    void OnDrawGizmosSelected()
    {
        if (transform.parent)
        {
            Gizmos.matrix = transform.parent.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, 0.001f, halfDepth * 2f));
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3((halfWidth - edgeMargin) * 2f, 0.001f,
                                                          (halfDepth - edgeMargin) * 2f));
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, avoidRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minDistance);
    }
}
