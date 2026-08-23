// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.RaycastVehicle;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class RaycastVehicleProgram
{
    private static int Main()
    {
        try
        {
            using var application = new RaycastVehicleDemo();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Raycast vehicle sample ported from rbfx sample 46.</summary>
public sealed class RaycastVehicleDemo : Application
{
    private const float CameraDistance = 10.0f;
    private const uint StaticCollisionLayer = 2;
    private const int NumMushrooms = 1000;

    private Scene? _scene;
    private Node? _cameraNode;
    private Vehicle2? _vehicle;
    private Viewport? _viewport;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.RaycastVehicle";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Raycast Vehicle";
    }

    protected override void Start()
    {
        base.Start();
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context.Instance);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        CreateScene();
        CreateVehicle();
        CreateInstructions();
        SubscribeToEvents();

        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseVisible(false);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();

        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(500.0f);
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f));
        zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f));
        zone.SetFogStart(300.0f);
        zone.SetFogEnd(500.0f);
        zone.SetBoundingBox(new BoundingBox(-2000.0f, 2000.0f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.3f, -0.5f, 0.425f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));
        light.SetSpecularIntensity(0.5f);

        var terrainNode = _scene.CreateChild("Terrain");
        terrainNode.SetPosition(Vector3f.Zero);
        var terrain = terrainNode.CreateComponent<Terrain>();
        terrain.SetPatchSize(64);
        terrain.SetSpacing(new Vector3f(3.0f, 0.1f, 3.0f));
        terrain.SetSmoothing(true);
        terrain.SetHeightMap(Resources.Get<Image>("Textures/HeightMap.png"));
        terrain.SetMaterial(Resources.Get<Material>("Materials/Terrain.xml"));
        terrain.SetOccluder(true);

        var terrainBody = terrainNode.CreateComponent<RigidBody>();
        terrainBody.CollisionLayer = StaticCollisionLayer;
        terrainNode.CreateComponent<CollisionShape>().SetTerrain();

        var mushroomPrefab = Resources.Get<PrefabResource>("Prefabs/Mushroom.prefab");
        for (var i = 0; i < NumMushrooms; ++i)
        {
            var mushroomNode = _scene.CreateChild("Mushroom");
            var position = new Vector3f(
                MathDefs.Random(2000.0f) - 1000.0f,
                0.0f,
                MathDefs.Random(2000.0f) - 1000.0f);
            position.Y = terrain.GetHeight(position) - 0.1f;
            mushroomNode.SetPosition(position);
            mushroomNode.SetRotation(new Quaternion(Vector3f.Up, terrain.GetNormal(position)));
            mushroomNode.SetScale(3.0f);
            mushroomNode.CreateComponent<PrefabReference>().SetPrefab(mushroomPrefab);
        }
    }

    private void CreateVehicle()
    {
        var vehicleNode = _scene!.CreateChild("Vehicle");
        vehicleNode.SetPosition(new Vector3f(0.0f, 25.0f, 0.0f));
        _vehicle = vehicleNode.CreateComponent<Vehicle2>();
        var moveAndOrbit = vehicleNode.CreateComponent<MoveAndOrbitController>();
        moveAndOrbit.LoadInputMap("Input/MoveAndOrbit.inputmap");
        _vehicle.SetInputMap(moveAndOrbit.GetInputMap());
        _vehicle.Init();
    }

    private void CreateInstructions()
    {
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    private void SubscribeToEvents()
    {
        SubscribeToEvent<CoreEvents.PostUpdateEvent>(
            (ref CoreEvents.PostUpdateEvent _) => HandlePostUpdate());
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (_vehicle is not null && !ImGui.GetIO().WantCaptureKeyboard && Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;
    }

    private void HandlePostUpdate()
    {
        if (_vehicle?.TryGetNode() is not { } vehicleNode || _cameraNode is null || _scene is null)
            return;

        var direction = new Quaternion(vehicleNode.Rotation.YawAngle, Vector3f.Up);
        direction *= new Quaternion(_vehicle.GetYaw(), Vector3f.Up);
        direction *= new Quaternion(_vehicle.GetPitch(), Vector3f.Right);
        var cameraStartPosition = vehicleNode.Position;
        var cameraTargetPosition = cameraStartPosition - direction * new Vector3f(0.0f, 0.0f, CameraDistance);

        var cameraRay = new Ray(cameraStartPosition, cameraTargetPosition - cameraStartPosition);
        var cameraRayLength = (cameraTargetPosition - cameraStartPosition).Length;
        var world = _scene.GetComponent<PhysicsWorld>();
        if (world is not null && world.TryRaycastSingle(
                cameraRay, cameraRayLength, out var result, StaticCollisionLayer) && result.Body is not null)
        {
            cameraTargetPosition = cameraStartPosition + cameraRay.Direction * (result.Distance - 0.5f);
        }

        _cameraNode.SetPosition(cameraTargetPosition);
        _cameraNode.SetRotation(direction);
    }

    private void HandlePostRenderUpdate()
    {
        if (!_drawDebug || _vehicle?.TryGetNode() is not { } vehicleNode || _scene is null)
            return;

        var debug = _scene.GetOrCreateComponent<DebugRenderer>();
        vehicleNode.GetComponent<RigidBody>()?.DrawDebugGeometry(debug, false);
        var vehicle = vehicleNode.GetComponent<RedBolt.PhysicsJolt.RaycastVehicle>();
        if (vehicle is null)
            return;
        for (var i = 0; i < vehicle.NumWheels; ++i)
            vehicle.GetWheel(i)?.DrawDebugGeometry(debug, false);
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Raycast Vehicle"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted(
                "Use WASD keys to drive, F to brake, mouse/touch to rotate camera\nSpace to toggle physics debug geometry\nEscape to exit"u8);
        }
        ImGui.End();
    }

    protected override void Stop()
    {
        Cleanup();
        base.Stop();
    }

    public override void Dispose()
    {
        Cleanup();
        base.Dispose();
    }

    private void Cleanup()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _vehicle = null;
        _cameraNode = null;
        _viewport?.Dispose();
        _viewport = null;
        _scene?.Dispose();
        _scene = null;
    }
}
