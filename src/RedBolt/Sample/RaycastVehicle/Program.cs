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
using Widgets = RedBolt.SystemUI.Widgets;

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

    private Scene? _scene;
    private Node? _cameraNode;
    private Vehicle2? _vehicle;
    private Viewport? _viewport;
    private bool _drawDebug;
    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

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
        camera.SetFarClip(350.0f);
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.30f, 0.34f, 0.42f));
        zone.SetFogColor(new Color(0.12f, 0.15f, 0.21f));
        zone.SetFogStart(180.0f);
        zone.SetFogEnd(320.0f);
        zone.SetBoundingBox(new BoundingBox(-500.0f, 500.0f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.45f, -1.0f, 0.55f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.92f, 0.96f, 1.0f));
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(12.0f, 50.0f, 150.0f, 0.0f, 0.8f));
        light.SetSpecularIntensity(0.55f);

        // One continuous collision slab supports the entire closed course.
        CreateStaticBox("Ground", new Vector3f(0.0f, -0.5f, 5.0f), new Vector3f(130.0f, 1.0f, 180.0f),
            "Materials/TechnicalDemo/SquareFloorGrid.xml", true);

        // Outer wall and central island define a wide, fully driveable rectangular loop.
        CreateStaticBox("OuterLeft", new Vector3f(-60.0f, 0.65f, 5.0f), new Vector3f(0.6f, 1.3f, 160.0f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("OuterRight", new Vector3f(60.0f, 0.65f, 5.0f), new Vector3f(0.6f, 1.3f, 160.0f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("OuterSouth", new Vector3f(0.0f, 0.65f, -75.0f), new Vector3f(120.0f, 1.3f, 0.6f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("OuterNorth", new Vector3f(0.0f, 0.65f, 85.0f), new Vector3f(120.0f, 1.3f, 0.6f),
            "Materials/TechnicalDemo/Dark.xml", true);

        CreateStaticBox("InnerLeft", new Vector3f(-26.0f, 0.65f, 5.0f), new Vector3f(0.6f, 1.3f, 90.0f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("InnerRight", new Vector3f(26.0f, 0.65f, 5.0f), new Vector3f(0.6f, 1.3f, 90.0f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("InnerSouth", new Vector3f(0.0f, 0.65f, -40.0f), new Vector3f(52.0f, 1.3f, 0.6f),
            "Materials/TechnicalDemo/Dark.xml", true);
        CreateStaticBox("InnerNorth", new Vector3f(0.0f, 0.65f, 50.0f), new Vector3f(52.0f, 1.3f, 0.6f),
            "Materials/TechnicalDemo/Dark.xml", true);

        // Slalom markers exercise steering on the first straight without blocking the lane.
        for (var i = 0; i < 7; ++i)
        {
            var x = i % 2 == 0 ? -46.0f : -39.0f;
            CreateStaticBox("SlalomMarker", new Vector3f(x, 0.45f, -27.0f + i * 10.0f),
                new Vector3f(1.0f, 0.9f, 1.0f), "Materials/TechnicalDemo/Accent.xml", true);
        }

        // Each ramp rises in the clockwise driving direction and may be bypassed on either side.
        CreateStaticBox("WestRamp", new Vector3f(-43.0f, 0.45f, 42.0f), new Vector3f(7.0f, 0.7f, 11.0f),
            "Materials/TechnicalDemo/Grid.xml", true, new Quaternion(-7.0f, 0.0f, 0.0f));
        CreateStaticBox("NorthRamp", new Vector3f(0.0f, 0.45f, 67.5f), new Vector3f(7.0f, 0.7f, 11.0f),
            "Materials/TechnicalDemo/Grid.xml", true, new Quaternion(-7.0f, 90.0f, 0.0f));
        CreateStaticBox("EastRamp", new Vector3f(43.0f, 0.45f, -30.0f), new Vector3f(7.0f, 0.7f, 11.0f),
            "Materials/TechnicalDemo/Grid.xml", true, new Quaternion(7.0f, 0.0f, 0.0f));
        CreateStaticBox("SouthRamp", new Vector3f(0.0f, 0.45f, -57.5f), new Vector3f(7.0f, 0.7f, 11.0f),
            "Materials/TechnicalDemo/Grid.xml", true, new Quaternion(7.0f, 90.0f, 0.0f));

        CreateStaticBox("BrakeMarker", new Vector3f(40.0f, 0.35f, 28.0f), new Vector3f(3.0f, 0.7f, 3.0f),
            "Materials/TechnicalDemo/Accent.xml", true);
        CreateStaticBox("BrakeMarker", new Vector3f(46.0f, 0.35f, 10.0f), new Vector3f(3.0f, 0.7f, 3.0f),
            "Materials/TechnicalDemo/Accent.xml", true);
        CreateStaticBox("BrakeMarker", new Vector3f(40.0f, 0.35f, -8.0f), new Vector3f(3.0f, 0.7f, 3.0f),
            "Materials/TechnicalDemo/Accent.xml", true);

        CreateGantry(new Vector3f(-43.0f, 0.0f, -55.0f), true);
        CreateGantry(new Vector3f(0.0f, 0.0f, 67.5f), false);
        CreateGantry(new Vector3f(43.0f, 0.0f, 42.0f), true);
        CreateGantry(new Vector3f(0.0f, 0.0f, -57.5f), false);
    }

    private void CreateGantry(Vector3f center, bool spansX)
    {
        var offset = spansX ? new Vector3f(15.0f, 0.0f, 0.0f) : new Vector3f(0.0f, 0.0f, 15.0f);
        var postScale = new Vector3f(0.8f, 6.0f, 0.8f);
        var beamScale = spansX ? new Vector3f(30.8f, 0.45f, 0.8f) : new Vector3f(0.8f, 0.45f, 30.8f);
        CreateStaticBox("Gantry", center - offset + new Vector3f(0.0f, 3.0f, 0.0f), postScale,
            "Materials/TechnicalDemo/Accent.xml", false);
        CreateStaticBox("Gantry", center + offset + new Vector3f(0.0f, 3.0f, 0.0f), postScale,
            "Materials/TechnicalDemo/Accent.xml", false);
        CreateStaticBox("Gantry", center + new Vector3f(0.0f, 5.6f, 0.0f), beamScale,
            "Materials/TechnicalDemo/Dark.xml", false);
    }

    private void CreateStaticBox(string name, Vector3f position, Vector3f scale, string material,
        bool collision, Quaternion? rotation = null)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetScale(scale);
        if (rotation.HasValue)
            node.SetRotation(rotation.Value);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(Resources.Get<Material>(material));
        model.SetCastShadows(true);
        if (!collision)
            return;
        node.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = node.CreateComponent<RigidBody>();
        body.CollisionLayer = StaticCollisionLayer;
    }

    private void CreateVehicle()
    {
        var vehicleNode = _scene!.CreateChild("Vehicle");
        vehicleNode.SetPosition(new Vector3f(-43.0f, 2.5f, -64.0f));
        _vehicle = vehicleNode.CreateComponent<Vehicle2>();
        var moveAndOrbit = vehicleNode.CreateComponent<MoveAndOrbitController>();
        moveAndOrbit.LoadInputMap("Input/MoveAndOrbit.inputmap");
        _vehicle.SetInputMap(moveAndOrbit.GetInputMap());
        _vehicle.Init();
    }

    private void CreateInstructions()
    {
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_move.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_f.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png")
        ];
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
        _scene.GetComponent<PhysicsWorld>()?.DrawDebugGeometry(debug, true);
        var vehicle = vehicleNode.GetComponent<RedBolt.PhysicsJolt.RaycastVehicle>();
        if (vehicle is null)
            return;
        for (var i = 0; i < vehicle.NumWheels; ++i)
            vehicle.GetWheel(i)?.DrawDebugGeometry(debug, false);
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Raycast Vehicle"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Drive"u8);
            DrawPrompt(_controlPrompts[4], "Rotate camera / drag touch"u8);
            DrawPrompt(_controlPrompts[5], "Brake"u8);
            DrawPrompt(_controlPrompts[6], _drawDebug ? "Hide physics debug"u8 : "Show physics debug"u8);
        }
        ImGui.End();
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, PromptIconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
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
