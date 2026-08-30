// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.KinematicCharacter;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Sample.CharacterControls;
using RedBolt.Scene;

internal static class KinematicCharacterProgram
{
    private static int Main()
    {
        try
        {
            using var application = new KinematicCharacterDemo();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>
/// Moving character example.
/// This sample demonstrates:
///     - Controlling a humanoid character through physics
///     - Driving animations using the AnimationController component
///     - Manual control of a bone scene node
///     - Implementing 1st and 3rd person cameras, using raycasts to avoid the 3rd person camera clipping into scenery
///     - Defining attributes of a custom component so that it can be saved and loaded
///     - Using touch inputs/gyroscope for iOS/Android (implemented through an external file)
/// </summary>
public class KinematicCharacterDemo : Application
{
    private const float CameraMinDist = 1.0f;
    private const float CameraInitialDist = 5.0f;
    private const float CameraMaxDist = 20.0f;

    /// <summary>Construct.</summary>
    public KinematicCharacterDemo()
    {
        _firstPerson = false;
        _drawDebug = false;
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context);
        _dPadAdapter = new DirectionalPadAdapter();
        _dPadAdapter.SetEnabled(true);

        // Create static scene content
        CreateScene();

        // Create the controllable character
        CreateCharacter(Vector3f.Zero);

        // Create the UI content
        CreateInstructions();

        // Subscribe to necessary events
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Create static scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();

        // Create scene subsystem components
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();

        // Create camera and define viewport. We will be doing load / save, so it's convenient to create the camera outside the scene,
        // so that it won't be destroyed and recreated, and we don't have to redefine the viewport on load
        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        // Create static scene content. First create a zone for ambient lighting and fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.5f, 0.5f, 0.5f));
        zone.SetFogColor(new Color(0.4f, 0.5f, 0.8f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));

        // Create a directional light with cascaded shadow mapping
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.3f, -0.5f, 0.425f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        void UpdateShadowCascades() => light.SetShadowCascade(ShadowCascadeEstimator.Estimate(camera,
            new Vector2i(Graphics.Width, Graphics.Height), new RenderPipelineSettings(),
            new ShadowCascadeEstimateSettings(200.0f) { ShadowResolution = light.GetShadowResolution() }));
        UpdateShadowCascades();
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => UpdateShadowCascades());
        light.SetSpecularIntensity(0.5f);

        // Create the floor object
        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(200.0f, 1.0f, 200.0f));
        var floorObject = floorNode.CreateComponent<StaticModel>();
        floorObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
        floorObject.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml"));

        var floorBody = floorNode.CreateComponent<RigidBody>();
        // Use collision layer bit 2 to mark world scenery. This is what we will raycast against to prevent camera from going
        // inside geometry
        floorBody.CollisionLayer = 2;
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);

        // Create sliding door
        {
            var doorPrefab = cache.Get<PrefabResource>("Prefabs/SlidingDoor.prefab");
            var objectNode = _scene.CreateChild("SlidingDoor");
            objectNode.SetPosition(new Vector3f(-3.0f, 0.0f, -3.0f));
            var prefabReference = objectNode.CreateComponent<PrefabReference>();
            prefabReference.SetPrefab(doorPrefab);
            prefabReference.Inline(PrefabInlineFlag.None);
        }

        // Create movable boxes. Let them fall from the sky at first
        const int NumBoxes = 100;
        for (var i = 0; i < NumBoxes; ++i)
        {
            var scale = MathDefs.Random(2.0f) + 0.5f;

            var objectNode = _scene.CreateChild("Box");
            objectNode.SetPosition(new Vector3f(
                MathDefs.Random(180.0f) - 90.0f,
                MathDefs.Random(10.0f) + 10.0f,
                MathDefs.Random(180.0f) - 90.0f));
            objectNode.SetRotation(new Quaternion(
                MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
            objectNode.SetScale(scale);
            var boxObject = objectNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/Dark.xml"));
            boxObject.SetCastShadows(true);

            var body = objectNode.CreateComponent<RigidBody>();
            body.CollisionLayer = 2;
            // Bigger boxes will be heavier and harder to move
            body.Mass = scale * 2.0f;
            objectNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        }
    }

    /// <summary>Create controllable character.</summary>
    private void CreateCharacter(Vector3f position)
    {
        var cache = Resources;

        var objectNode = _scene!.CreateChild("Character");
        objectNode.SetPosition(position);

        // Keep the model's native +Z forward direction aligned with the character node.
        var adjustNode = objectNode.CreateChild("AdjNode");

        // Create the rendering component + animation controller
        var model = adjustNode.CreateComponent<AnimatedModel>();
        model.SetModel(cache.Get<Model>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Models/Mannequin.mdl"));
        var characterMaterial = cache.Get<Material>("Materials/TechnicalDemo/Yellow.xml");
        model.SetMaterial(0, characterMaterial);
        model.SetMaterial(1, characterMaterial);
        model.SetCastShadows(true);
        adjustNode.CreateComponent<AnimationController>();

        // Set the head bone for manual control
        model.GetSkeleton().GetBone("Head")!.Animated = false;

        // Create the character logic component, which takes care of steering the fake rigidbody
        // Remember it so that we can set the controls. Use a ea::weak_ptr because the scene hierarchy already owns it
        // and keeps it alive as long as it's not removed from the hierarchy
        var character = objectNode.CreateComponent<KinematicCharacter>();
        _character = new WeakReference<KinematicCharacter>(character);
        var moveAndOrbit = objectNode.CreateComponent<MoveAndOrbitController>();
        moveAndOrbit.LoadInputMap("Input/MoveAndOrbit.inputmap");
        character.SetInputMap(moveAndOrbit.GetInputMap());

        var kinematicCharacter = objectNode.CreateComponent<KinematicCharacterController>();
        _kinematicCharacter = new WeakReference<KinematicCharacterController>(kinematicCharacter);
        kinematicCharacter.Diameter = 0.7f;
        kinematicCharacter.Height = 1.8f;
        kinematicCharacter.Offset = new Vector3f(0.0f, 0.9f, 0.0f);
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _controlPrompts = new CharacterControlPrompts(Resources);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // The text has multiple rows. Center them in relation to each other
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>Subscribe to necessary events.</summary>
    private void SubscribeToEvents()
    {
        // Subscribe to PostUpdate event for updating the camera position after physics simulation
        SubscribeToEvent<CoreEvents.PostUpdateEvent>((ref CoreEvents.PostUpdateEvent _) => HandlePostUpdate());

        // Unsubscribe the SceneUpdate event from base class as the camera node is being controlled in HandlePostUpdate() in this sample

        // Subscribe HandlePostRenderUpdate() function for processing the post-render update event, during which we request
        // debug geometry
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    /// <summary>Handle application update. Set controls to character.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        var rotating = Input.GetMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse;
        Input.SetMouseMode(rotating ? MouseMode.Relative : MouseMode.Free);
        Input.SetMouseVisible(!rotating);

        if (_character is not null && _character.TryGetTarget(out var character))
        {
            // Update controls using keys
            if (!ImGui.GetIO().WantCaptureKeyboard)
            {
                // Set rotation already here so that it's updated every rendering frame instead of every physics frame
                character.Node.SetRotation(new Quaternion(character.GetYaw(), Vector3f.Up));

                // Switch between 1st and 3rd person
                if (Input.GetKeyPress(Key.F))
                    _firstPerson = !_firstPerson;
            }
        }

        // Toggle debug geometry with space
        if (!ImGui.GetIO().WantCaptureKeyboard && Input.GetKeyPress(Key.M))
        {
            _drawDebug = !_drawDebug;
            if (_drawDebug && _scene is not null)
            {
                _scene.GetOrCreateComponent<DebugRenderer>();
            }
        }
    }

    /// <summary>Handle application post-update. Update camera position after character has moved.</summary>
    private void HandlePostUpdate()
    {
        if (_character is null || !_character.TryGetTarget(out var character))
            return;

        var characterNode = character.Node;

        // Get camera lookat dir from character yaw + pitch
        var rot = characterNode.Rotation;
        var dir = rot * new Quaternion(character.GetPitch(), Vector3f.Right);

        // Turn head to camera pitch, but limit to avoid unnatural animation
        var headNode = characterNode.GetChild("Head", true)!;
        var limitPitch = MathDefs.Clamp(character.GetPitch(), -45.0f, 45.0f);
        var headDir = rot * new Quaternion(limitPitch, new Vector3f(1.0f, 0.0f, 0.0f));
        // This could be expanded to look at an arbitrary target, now just look at a point in front
        var headWorldTarget = headNode.WorldPosition + headDir * new Vector3f(0.0f, 0.0f, -1.0f);
        headNode.LookAt(headWorldTarget, new Vector3f(0.0f, 1.0f, 0.0f));

        if (_firstPerson)
        {
            _cameraNode!.SetPosition(headNode.WorldPosition + rot * new Vector3f(0.0f, 0.15f, 0.2f));
            _cameraNode.SetRotation(dir);
        }
        else
        {
            // Third person camera: position behind the character
            var aimPoint = characterNode.Position + rot * new Vector3f(0.0f, 1.7f, 0.0f);

            // Collide camera ray with static physics objects (layer bitmask 2) to ensure we see the character properly
            var rayDir = dir * Vector3f.Back;
            var rayDistance = CameraInitialDist;
            var result = default(PhysicsRaycastResult);
            _scene!.GetComponent<PhysicsWorld>()!.RaycastSingle(ref result, new Ray(aimPoint, rayDir), rayDistance, 2);
            if (result.Body is not null)
                rayDistance = MathF.Min(rayDistance, result.Distance);
            rayDistance = MathDefs.Clamp(rayDistance, CameraMinDist, CameraMaxDist);

            _cameraNode!.SetPosition(aimPoint + rayDir * rayDistance);
            _cameraNode.SetRotation(dir);
        }
    }

    private void HandlePostRenderUpdate()
    {
        if (_drawDebug)
        {
            _scene!.GetComponent<PhysicsWorld>()!.DrawDebugGeometry(true);
            var debugRenderer = _scene.GetComponent<DebugRenderer>()!;

            var objectNode = _scene.GetChild("Player");
            if (objectNode is not null)
            {
                debugRenderer.AddSphere(new Sphere(objectNode.WorldPosition, 0.1f), Color.Yellow);
            }
        }
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        if (ImGui.Begin("Kinematic Character"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            _controlPrompts!.Draw(true, true);
        }
        ImGui.End();
        ImGui.PopStyleColor();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.KinematicCharacter";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Kinematic Character";
    }

    protected override void Stop()
    {
        Cleanup();
        base.Stop();
    }

    public override void Dispose()
    {
        Cleanup();
        _dPadAdapter?.SetEnabled(false);
        _dPadAdapter?.Dispose();
        _dPadAdapter = null;
        base.Dispose();
    }

    private void Cleanup()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _kinematicCharacter = null;
        _character = null;
        _cameraNode = null;
        _viewport?.Dispose();
        _viewport = null;
        _scene?.Dispose();
        _scene = null;
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Viewport? _viewport;
    /// <summary>The controllable character component.</summary>
    private WeakReference<KinematicCharacter>? _character;
    private WeakReference<KinematicCharacterController>? _kinematicCharacter;
    /// <summary>First person camera flag.</summary>
    private bool _firstPerson;

    private bool _drawDebug;
    private DirectionalPadAdapter? _dPadAdapter;
    private CharacterControlPrompts? _controlPrompts;
    private static readonly Vector4f HudBackground = new(0.055f, 0.065f, 0.085f, 0.96f);
}
