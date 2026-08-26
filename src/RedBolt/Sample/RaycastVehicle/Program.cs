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
    private const float CameraDistance = 14.0f;
    private const uint StaticCollisionLayer = 2;
    private const int NumSceneryRocks = 1000;

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
        zone.SetAmbientColor(new Color(0.28f, 0.30f, 0.26f));
        zone.SetFogColor(new Color(0.62f, 0.72f, 0.82f));
        zone.SetFogStart(250.0f);
        zone.SetFogEnd(500.0f);
        zone.SetBoundingBox(new BoundingBox(-2000.0f, 2000.0f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.3f, -0.5f, 0.425f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(1.0f, 0.84f, 0.68f));
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
        terrain.SetMaterial(Resources.Get<Material>("ThirdParty/KenneyNature/grass.glb.d/Materials/grass_Lit.xml"));
        terrain.SetOccluder(true);

        var terrainBody = terrainNode.CreateComponent<RigidBody>();
        terrainBody.CollisionLayer = StaticCollisionLayer;
        terrainNode.CreateComponent<CollisionShape>().SetTerrain();

        var rockModel = Resources.Get<Model>("ThirdParty/KenneyNature/rock_smallA.glb.d/Models/Mesh rock_smallA.mdl");
        var rockGrassMaterial = Resources.Get<Material>("ThirdParty/KenneyNature/rock_smallA.glb.d/Materials/grass_Lit.xml");
        var dirtMaterial = Resources.Get<Material>("ThirdParty/KenneyNature/rock_smallA.glb.d/Materials/dirt_Lit.xml");
        for (var i = 0; i < NumSceneryRocks; ++i)
        {
            var position = new Vector3f(
                MathDefs.Random(2000.0f) - 1000.0f,
                0.0f,
                MathDefs.Random(2000.0f) - 1000.0f);
            if (MathF.Abs(position.X) < 10.0f && position.Z > -5.0f && position.Z < 30.0f)
                continue;
            var rockNode = _scene.CreateChild("SceneryRock");
            position.Y = terrain.GetHeight(position) - 0.1f;
            rockNode.SetPosition(position);
            rockNode.SetRotation(new Quaternion(Vector3f.Up, terrain.GetNormal(position)));
            rockNode.SetScale(1.5f + MathDefs.Random(1.5f));
            var rock = rockNode.CreateComponent<StaticModel>();
            rock.SetModel(rockModel);
            rock.SetMaterial(0, rockGrassMaterial);
            rock.SetMaterial(1, dirtMaterial);
        }

        var trackMaterial = Resources.Get<Material>(
            "ThirdParty/KenneyToyCar/track-striped-wide-straight.glb.d/Materials/colormap_Lit.xml");
        var trackNode = _scene.CreateChild("Track");
        trackNode.SetPosition(new Vector3f(0.0f, terrain.GetHeight(new Vector3f(0.0f, 0.0f, 10.0f)) + 0.1f, 0.0f));
        trackNode.SetScale(5.0f);
        var trackModel = trackNode.CreateComponent<StaticModel>();
        trackModel.SetModel(Resources.Get<Model>(
            "ThirdParty/KenneyToyCar/track-striped-wide-straight.glb.d/Models/track-striped-wide-straight.mdl"));
        trackModel.SetMaterial(trackMaterial);
        trackModel.SetCastShadows(true);

        var gateNode = _scene.CreateChild("FinishGate");
        gateNode.SetPosition(new Vector3f(0.0f, terrain.GetHeight(new Vector3f(0.0f, 0.0f, 20.0f)), 20.0f));
        gateNode.SetScale(5.0f);
        var gateModel = gateNode.CreateComponent<StaticModel>();
        gateModel.SetModel(Resources.Get<Model>(
            "ThirdParty/KenneyToyCar/gate-finish.glb.d/Models/gate-finish.mdl"));
        gateModel.SetMaterial(Resources.Get<Material>(
            "ThirdParty/KenneyToyCar/gate-finish.glb.d/Materials/colormap_Lit.xml"));
        gateModel.SetCastShadows(true);

        var treeModel = Resources.Get<Model>(
            "ThirdParty/KenneyNature/tree_default.glb.d/Models/Mesh tree_default.mdl");
        for (var i = 0; i < 10; ++i)
        {
            var treeNode = _scene.CreateChild("RouteTree");
            var position = new Vector3f(i % 2 == 0 ? -13.0f : 13.0f, 0.0f, i * 5.0f);
            position.Y = terrain.GetHeight(position);
            treeNode.SetPosition(position);
            treeNode.SetScale(3.0f);
            var tree = treeNode.CreateComponent<StaticModel>();
            tree.SetModel(treeModel);
            tree.SetMaterial(0, Resources.Get<Material>(
                "ThirdParty/KenneyNature/tree_default.glb.d/Materials/woodBark_Lit.xml"));
            tree.SetMaterial(1, Resources.Get<Material>(
                "ThirdParty/KenneyNature/tree_default.glb.d/Materials/leafsGreen_Lit.xml"));
            tree.SetCastShadows(true);
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
        var cameraStartPosition = vehicleNode.Position + Vector3f.Up * 2.5f;
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
