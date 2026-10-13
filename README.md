# AR Image Markers in Unity 6

Build an iPhone app that recognises a printed image and places a 3D model on top of it, sized to the printout and following it as you move.

**In this repo**

| File | What it is |
| --- | --- |
| [`Assets/Scripts/MarkerSpawner.cs`](Assets/Scripts/MarkerSpawner.cs) | The script that spawns a prefab on each marker |
| [`Markers/ar_marker.png`](Markers/ar_marker.png) | A ready-made black-and-white marker to print |

**Before you start you need:**

- Unity 6 installed through Unity Hub, with the **iOS Build Support** module
- A Mac with Xcode, an Apple ID, and an iPhone with a USB cable
- A printed marker image and a 3D model (FBX or OBJ)

In AR, 1 Unity unit = 1 metre. Keep that in mind whenever you size anything.

## Contents

1. [Create the project](#part-1-create-the-project)
2. [Sort out the scene](#part-2-sort-out-the-scene)
3. [Add marker images](#part-3-add-marker-images)
4. [Prefabs and the spawner script](#part-4-prefabs-and-the-spawner-script)
5. [Test in the editor with XR Simulation](#part-5-test-in-the-editor-with-xr-simulation)
6. [Build and run on an iPhone](#part-6-build-and-run-on-an-iphone)
7. [Troubleshooting](#troubleshooting)

## Part 1: Create the project

Start from the **AR Mobile** template. It installs AR Foundation, ARKit and ARCore, sets up URP for AR, and fills in the mobile player settings for you.

1. Open **Unity Hub** → **New project**.
2. Choose the **AR Mobile** template, name the project, and click **Create project**.
3. When the editor opens, check **Window → Package Manager**. AR Foundation, ARKit XR Plugin and ARCore XR Plugin should all be the same version (6.5.0 in template v2.1.3).

If you ever upgrade, upgrade those three packages together. Mismatched versions cause confusing build errors.

## Part 2: Sort out the scene

The sample scene is built for a tap-to-place demo. Keep the AR parts and switch off the demo parts.

| Object | What it does | What to do |
| --- | --- | --- |
| AR Session | Starts, pauses and resets AR tracking | Keep, don't change |
| Directional Light | Lights your 3D models | Keep |
| EventSystem | Sends screen touches to UI buttons | Keep |
| UI | The demo's prompts and buttons | Disable |
| XR Origin (AR Rig) | The AR camera and all the tracking managers | Keep, and edit as below |

**On XR Origin (AR Rig):**

1. Untick the **AR Plane Manager** component. This also removes the dotted pattern on tables and floors.
2. Disable the **Object Spawner** child (the demo's shape spawner).
3. Leave **Camera Offset → Main Camera** exactly as it is. Changing its AR components gives you a black screen.

Disable rather than delete, so you can switch the demo parts back on later. Changes to XR Origin show as prefab overrides, which is fine.

## Part 3: Add marker images

A marker tracks well when it is high contrast, full of sharp corners, not symmetrical and not repetitive. Plain logos, smooth gradients and grid patterns track badly. You can use [`Markers/ar_marker.png`](Markers/ar_marker.png) from this repo.

1. Drag your marker image (PNG or JPG) into the **Project** window.
2. **Assets → Create → XR → Reference Image Library**. Name it `MarkerLibrary`.
3. Select the library and click **Add Image**. Drag your marker texture into the new slot.
4. Give it a **Name**, e.g. `Everest`. Your prefab will need exactly the same name later.
5. Tick **Specify Size** and enter the printed width in metres, e.g. `0.15` for 15 cm.
6. Select **XR Origin (AR Rig)** → **Add Component** → **AR Tracked Image Manager**.
7. Drag `MarkerLibrary` into its **Serialized Library** field. Leave **Tracked Image Prefab** empty.

**Printing:** print at the exact width you entered, on matte paper, and keep it flat. Measure the printout rather than trusting the printer settings.

## Part 4: Prefabs and the spawner script

Each marker gets its own prefab, and a short script spawns the right one when its marker is seen.

**Build a prefab**

1. Drag your model file into the Project window. If it imports at a strange size, select it, change **Scale Factor** on the **Model** tab, and click **Apply**.
2. In the Hierarchy, **right-click → Create Empty**. Name it exactly like the marker, e.g. `Everest`. Keep its position 0,0,0, rotation 0, scale 1.
3. Drag the model onto that empty so it becomes a **child**.
4. Move the child so it is centred and its base sits at Y = 0, on top of the marker rather than sunk into it.
5. Add a **3D Object → Cube** as a ruler (it is 1 m). Scale the **child** model until it is about **1 cube wide**, then delete the cube.
6. Drag the root `Everest` object into a `Prefabs` folder, then delete it from the scene.

If the model is pink, convert its materials to URP: **Edit → Rendering → Materials → Convert Selected Built-in Materials to URP**. If it lies on its side, rotate the **child** -90° on X.

**Add the script**

Copy [`Assets/Scripts/MarkerSpawner.cs`](Assets/Scripts/MarkerSpawner.cs) into your project's `Assets` folder (the file name must match the class name):

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MarkerSpawner : MonoBehaviour
{
    [SerializeField] ARTrackedImageManager manager;
    [SerializeField] GameObject[] prefabs;      // names must match reference image names
    [SerializeField] bool scaleToImageWidth = true;

    void OnEnable()  => manager.trackablesChanged.AddListener(OnChanged);
    void OnDisable() => manager.trackablesChanged.RemoveListener(OnChanged);

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var img in args.added)
        {
            var prefab = System.Array.Find(prefabs, p => p.name == img.referenceImage.name);
            if (!prefab)
            {
                Debug.LogWarning($"No prefab named '{img.referenceImage.name}'");
                continue;
            }
            var go = Instantiate(prefab, img.transform);
            if (scaleToImageWidth) go.transform.localScale = Vector3.one * img.size.x;
        }

        foreach (var img in args.updated)
        {
            bool visible = img.trackingState == TrackingState.Tracking;
            foreach (Transform child in img.transform) child.gameObject.SetActive(visible);
        }
    }
}
```

1. Drag `MarkerSpawner` onto **XR Origin (AR Rig)**.
2. Drag **XR Origin (AR Rig)** itself into the **Manager** slot.
3. Add your prefabs to the **Prefabs** list.
4. Leave **Scale To Image Width** ticked if your prefabs are built 1 unit wide.

**What the script does:** when a marker is first seen, it finds the prefab with the same name, places it on the marker, and scales it to the printed width. When the marker leaves view, it hides the model.

## Part 5: Test in the editor with XR Simulation

XR Simulation runs your AR app inside Unity, using a virtual room as the camera feed, so you can test without building to a phone. (The Xcode iPhone Simulator cannot run AR, because it has no camera.)

**Turn it on**

1. **Edit → Project Settings → XR Plug-in Management**.
2. On the **desktop (Standalone) tab**, tick **XR Simulation**. Leave ARKit ticked on the iOS tab.

**Add your marker to a simulated room**

1. **Window → XR → AR Foundation → XR Environment**. A toolbar appears in the Scene view. Accept the offer to install the sample environments.
2. Pick an environment, **duplicate** it so you are not editing Unity's copy, then click **Edit**.
3. Add an empty GameObject on a table or wall and add a **Simulated Tracked Image** component.
4. Set its **Image** to the same marker texture used in `MarkerLibrary`, and its size to match (e.g. 0.15 m).
5. Save, and select your edited environment in the toolbar.

**Press Play**

| Control | Action |
| --- | --- |
| Hold right mouse button + move mouse | Look around |
| W / A / S / D | Move forward, left, back, right |
| Q / E | Move down / up |

Walk up to the marker and your model should appear on it. Simulation detects markers quickly and perfectly, so always confirm on a real phone too.

## Part 6: Build and run on an iPhone

Unity builds an Xcode project, and Xcode installs it on your phone.

**In Unity**

1. **File → Build Profiles** → select **iOS** → **Switch Platform**.
2. Make sure your scene is in the **Scene List**.
3. **Project Settings → XR Plug-in Management → iOS tab**: check **Apple ARKit** is ticked.
4. **Player → Other Settings**: set a unique **Bundle Identifier** (e.g. `com.yourname.everestar`) and check **Camera Usage Description** is not empty.
5. Click **Build**, choose an empty folder such as `Builds/iOS`, and wait.

**In Xcode**

1. Open `Unity-iPhone.xcodeproj` from the build folder.
2. Select the **Unity-iPhone** target → **Signing & Capabilities**. Tick **Automatically manage signing** and choose your **Team** (add your Apple ID under **Xcode → Settings → Accounts** if needed).
3. Plug in the iPhone, select it at the top of the window, and press **Run**.

**On the iPhone (first time only)**

1. Turn on **Settings → Privacy & Security → Developer Mode**, then restart.
2. If you see "Untrusted Developer": **Settings → General → VPN & Device Management** → tap your Apple ID → **Trust**.
3. Allow camera access when asked.

With a free Apple ID the app stops working after **7 days**; run it from Xcode again to reinstall. When Unity asks on later builds, choose **Append** to keep your Xcode signing settings.

## Troubleshooting

| Problem | Likely cause | Fix |
| --- | --- | --- |
| Nothing appears on the marker | Prefab name doesn't match the image name | Check the Console for the "No prefab named" warning and rename to match exactly |
| Two models on each marker | Tracked Image Prefab field is filled in | Clear that field on AR Tracked Image Manager |
| Model far too big or small | Model not built 1 unit wide, or wrong Specify Size | Resize the child against a 1 m cube; check the library width |
| Model freezes where the marker was | Marker out of view | Expected; the script hides it when tracking is lost |
| Dotted pattern on tables and floors | AR Plane Manager still on | Untick AR Plane Manager on XR Origin |
| Pink model | Materials not URP | Convert materials to URP |
| Black camera screen | Main Camera's AR components changed | Restore the template's Main Camera |
| App crashes on launch | Camera Usage Description empty | Fill it in under Player → Other Settings |
| Marker tracks badly | Glossy, curled or low-detail print | Matte paper, flat, high-contrast image |
