# Portal Holes and Wandering Creatures

Two add-ons for the marker project in [README.md](README.md):

- **Portal hole**: the marker becomes a hole you can look down into, with a scene hidden below the paper.
- **Avoiding wanderer**: creatures that walk around the marker by themselves and keep out of each other's way.

Both assume you have already finished the main README, so you have a working `MarkerSpawner` and know how to build a prefab 1 unit wide.

| File | What it is |
| --- | --- |
| [`Scripts/PortalHole.cs`](Scripts/PortalHole.cs) | Builds the hole, its walls and floor at runtime |
| [`Scripts/DepthMask.shader`](Scripts/DepthMask.shader) | The invisible "occluder" shader the hole needs |
| [`Scripts/AvoidingWanderer.cs`](Scripts/AvoidingWanderer.cs) | Makes a model wander and avoid other wanderers |
| [`Scripts/Wanderer.cs`](Scripts/Wanderer.cs) | The simpler version that doesn't avoid others |

Copy the scripts and the shader into your project's `Assets` folder.

## Contents

1. [Portal hole](#part-1-portal-hole)
2. [Avoiding wanderer](#part-2-avoiding-wanderer)
3. [Putting creatures inside the hole](#part-3-putting-creatures-inside-the-hole)
4. [Troubleshooting](#troubleshooting)

## Part 1: Portal hole

### How it works

You can't really cut a hole in a table, so the script fakes one. It surrounds the hole with a large invisible frame lying flat on the paper. The frame uses the **DepthMask** shader. That shader draws nothing you can see, but it still blocks any 3D content behind it, so the camera feed shows through. The result is that your scene below the paper is only visible through the opening, as if the paper had a hole in it.

```
   camera
     \
      \    you see in here
 ======\===[  hole  ]=========   <- invisible frame at paper level (y = 0)
        \  |        |              hides everything under it
           |  your  |  <- walls
           | scene  |
           |________|  <- floor
```

When the game starts, the script builds the invisible frame, four walls and a floor. You only provide the prefab root, the materials and your scene.

### Step 1: Make the materials

| Material | How to make it |
| --- | --- |
| **Occluder** | **Create → Material**, then set **Shader** to **AR → DepthMask** |
| **Wall** | **Create → Material**, leave it on **Universal Render Pipeline/Lit**. A dark rock or earth colour works well |
| **Floor** (optional) | Another URP/Lit material. If you leave it empty, the floor uses the wall material |

The script can find the DepthMask shader by itself if you leave the occluder slot empty. Assign it anyway: Unity can leave out shaders that nothing refers to when it builds for the phone, and then the hole stops working on the device only.

### Step 2: Build the prefab

1. In the Hierarchy, **right-click → Create Empty**. Name it **exactly** like the marker image, e.g. `ar_marker_3`.
2. Keep its position at 0,0,0, rotation 0 and scale **1**. Don't scale the root, because `MarkerSpawner` sets its scale to the printed width.
3. **Add Component → Portal Hole**, and drag in your materials.
4. Set the sizes. One unit is the full width of the marker image:

| Setting | What it does | Start with |
| --- | --- | --- |
| Hole Width | Width of the opening (X) | `0.8` |
| Hole Depth | Length of the opening along the paper (Z) | `0.8` |
| Pit Depth | How deep the hole goes | `0.5` |
| Frame Size | How far the invisible frame reaches outwards | `2` |
| Build Walls | Turn off if your scene has its own walls | on |

`0.8` puts the edge of the hole just inside the black frame printed on the markers in [`Markers/`](Markers/). If you use `1`, the hole reaches the edge of the white margin instead.

5. Select the root. A **cyan wire box** in the Scene view shows the pit. The walls and frame only exist while the game is running, so use this box as your guide.

### Step 3: Add your scene

Add your cave, ocean, mountain or anything else as **children of the root**.

- Everything inside the pit must sit **below Y = 0** and inside the cyan box.
- Anything that sticks out past the sides of the box is hidden by the invisible frame. That's usually what you want.
- Keep content slightly inside the box edges. The walls are very thin and sit just outside the opening, so anything touching the edge pokes through them.
- Anything **above Y = 0** shows normally, as if it's standing on the paper. Use this for things climbing out of the hole.

### Step 4: Hook it up

1. Drag the root into your `Prefabs` folder, then delete it from the scene.
2. Add the prefab to the **Prefabs** list on `MarkerSpawner`.
3. Make sure the marker is in your **Reference Image Library**, with the same name, **Specify Size** ticked and the real printed width.

## Part 2: Avoiding wanderer

### How it works

Each creature always walks forwards and slowly drifts left and right, so it follows natural curvy paths rather than straight lines. Near the edge of its area it curves back towards the middle. Every so often it stops and looks around.

On top of that, it notices other `AvoidingWanderer`s standing on the same surface. If one gets inside its personal space, it turns away. If two still end up overlapping, they get pushed gently apart.

Use [`Wanderer.cs`](Scripts/Wanderer.cs) instead if you only have one creature and don't need avoidance. The settings are the same, minus the avoidance section.

### Step 1: Build the prefab

The script must go on the **model**, not the root, because it moves the model around inside the root.

```
ar_marker_4            <- root: empty, scale 1, named like the marker
 ├── Mouse             <- AvoidingWanderer goes HERE
 │    └── (mesh, bones...)
 ├── Mouse (1)         <- another one, also with AvoidingWanderer
 └── Mouse (2)
```

1. Create an empty root named like the marker, at position 0,0,0 and scale 1, as before.
2. Drag your model in as a **direct child** of the root. The script uses its parent as the "paper", so don't nest it any deeper.
3. Scale the model so it's a sensible size compared with the paper, which is 1 unit wide. Something like `0.1`–`0.2` wide works for small creatures.
4. Put its feet on **Y = 0**. The height it starts at is the height it walks at.
5. **Add Component → Avoiding Wanderer** on the model.
6. Duplicate the model (**Ctrl/Cmd + D**) to get more creatures. Spread them out so they don't start on top of each other.

Select a creature to see its gizmos in the Scene view:

| Gizmo | Meaning |
| --- | --- |
| Yellow box | The area it can walk in |
| Orange box | Where it starts turning back from the edge |
| Cyan circle | Personal space: it steers away from others inside this |
| Red circle | Closest it will ever get to another creature |

### Step 2: Tune the settings

**Area**: these are in prefab units, where the paper runs from -0.5 to 0.5.

| Setting | What it does | Default |
| --- | --- | --- |
| Half Width / Half Depth | How far from the centre it can go | `0.4` |
| Edge Margin | Starts curving away this far from the edge | `0.12` |

**Movement**

| Setting | What it does | Default |
| --- | --- | --- |
| Walk Speed | Paper widths per second | `0.15` |
| Speed Variation | How much its speed drifts (0 = steady) | `0.4` |
| Max Turn Rate | How tightly it can curve, in degrees per second | `90` |
| Wander Frequency | How often it changes which way it's curving | `0.3` |
| Edge Turn Rate | How hard it turns away from edges | `200` |

**Avoiding others**: these are in **metres**, not paper units, because creatures on different markers can be different sizes.

| Setting | What it does | Default |
| --- | --- | --- |
| Avoid Radius | Starts steering away when another creature is this close | `0.08` |
| Min Distance | Never gets closer than this | `0.03` |
| Avoid Turn Rate | How hard it turns away from others | `250` |
| Same Plane Tolerance | Ignores creatures more than this far above or below | `0.05` |

A good rule of thumb: set **Min Distance** to roughly the creature's real width once printed, and **Avoid Radius** to two or three times that. On a 15 cm marker, a creature that's `0.15` of the paper wide is about 2 cm across.

**Pauses**

| Setting | What it does | Default |
| --- | --- | --- |
| Walk Time | Walks for a random time between these, in seconds | `3`–`8` |
| Pause Chance | Chance it stops after each walk (0–1) | `0.5` |
| Pause Time | Stops for a random time between these, in seconds | `0.5`–`2` |

If another creature walks right up to one that's paused, it stops pausing and moves away.

**Walking bob**: a small up-and-down hop while moving. Set **Bob Height** to `0` if your model has its own walk animation.

### Step 3: Animation (optional)

If your model has walk and idle animations:

1. Give its **Animator Controller** a **Float** parameter called `Speed`.
2. Add a transition from **Idle → Walk** with the condition `Speed` **Greater** `0.01`, and another from **Walk → Idle** with `Speed` **Less** `0.01`. Untick **Has Exit Time** on both.
3. Drag the model's **Animator** into the script's **Animator** slot.

The script sends the current speed every frame, so the animation switches as the creature starts and stops.

### Creatures on different markers

Avoidance works between **any** `AvoidingWanderer`s on the same surface, not just ones in the same prefab. If two markers lie side by side on a table, their creatures avoid each other too. They still stay inside their own marker's area.

## Part 3: Putting creatures inside the hole

The two scripts work together. To get creatures walking around the bottom of a pit:

1. Build the portal prefab from Part 1.
2. Add the creature as a **direct child of the portal root**, with its feet on the pit floor, at **Y = -Pit Depth** (e.g. `-0.5`).
3. Add **Avoiding Wanderer** and make its area smaller than the hole so it can't walk into the walls. Use **Half Width** a little less than half of **Hole Width** minus the creature's size. For a `0.8` hole, start with `0.3`.
4. Turn **Walk Speed** down a bit. The pit is smaller than the whole paper.

## Troubleshooting

| Problem | Likely cause | Fix |
| --- | --- | --- |
| No hole, the scene just floats under the paper and shows everywhere | Occluder material missing or wrong shader | Assign a material using **AR → DepthMask** |
| Hole works in the editor but not on the phone | DepthMask shader left out of the build | Assign the occluder material in the Inspector rather than relying on the script to find it |
| Hole edge doesn't line up with the printed frame | Hole size doesn't match the print | Adjust **Hole Width/Depth**; `0.8` fits the markers in this repo |
| Scene shows through the invisible frame | A material uses a very early custom render queue | Put that material's queue back to its default |
| Changing PortalHole settings in Play mode does nothing | It builds everything once, at start | Stop, change the settings, press Play again |
| Content pokes through the pit walls | Content touching the hole edge | Move it slightly inside the cyan box |
| Creature walks off the marker | Script on the root, or model nested too deep | Put the script on a **direct child** of the root |
| Creatures walk through each other | Using `Wanderer` instead of `AvoidingWanderer`, or radius too small | Use `AvoidingWanderer`; increase **Avoid Radius** and **Min Distance** |
| Creatures stay far apart and never get close | Avoid Radius too big for the print size | Reduce it. Remember it's in metres |
| Creature floats or sinks while walking | Start height not on the surface | Put its feet at Y = 0 (or on the pit floor) before pressing Play |
| Creature slides without animating | Animator not assigned, or no `Speed` parameter | See Part 2, Step 3 |
