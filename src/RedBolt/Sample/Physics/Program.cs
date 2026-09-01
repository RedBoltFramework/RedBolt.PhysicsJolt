// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Physics;

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
using RedBolt.Sample.CharacterControls;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class PhysicsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new PhysicsSample();
            return application.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Physics example.
/// This sample demonstrates:
///     - Creating both static and moving physics objects to a scene
///     - Displaying the scene using the Renderer subsystem
/// </summary>
public class PhysicsSample : Application
{
    protected override void Start()
    {
        base.Start();
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(RedBolt.Core.Context.Instance);
        CreateScene();
        CreateInstructions();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseVisible(false);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.30f, 0.34f, 0.42f));
        zone.SetFogColor(new Color(0.12f, 0.15f, 0.21f));
        zone.SetFogStart(55.0f);
        zone.SetFogEnd(110.0f);
        zone.SetBoundingBox(new BoundingBox(-500.0f, 500.0f));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.45f, -1.0f, 0.55f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.92f, 0.96f, 1.0f));
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(60.0f, 1.0f, 60.0f));
        var floorObject = floorNode.CreateComponent<StaticModel>();
        floorObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
        floorObject.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml"));
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        floorNode.CreateComponent<RigidBody>();

        for (var y = 0; y < 8; ++y)
        {
            for (var x = -y; x <= y; ++x)
            {
                var boxNode = _scene.CreateChild("Box");
                boxNode.SetPosition(new Vector3f(x, -y + 8.0f, 0.0f));
                var boxObject = boxNode.CreateComponent<StaticModel>();
                boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
                boxObject.SetMaterial(cache.Get<Material>(
                    ((x + y) & 1) == 0 ? "Materials/TechnicalDemo/Accent.xml" : "Materials/TechnicalDemo/Dark.xml"));
                boxObject.SetCastShadows(true);
                boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
                var body = boxNode.CreateComponent<RigidBody>();
                body.Mass = 1.0f;
                body.Friction = 0.75f;
            }
        }

        CreateMarker(new Vector3f(-12.0f, 2.0f, 6.0f), new Vector3f(1.0f, 4.0f, 1.0f));
        CreateMarker(new Vector3f(12.0f, 2.0f, 6.0f), new Vector3f(1.0f, 4.0f, 1.0f));
        CreateMarker(new Vector3f(-12.0f, 0.5f, -8.0f), new Vector3f(1.0f, 1.0f, 1.0f));
        CreateMarker(new Vector3f(12.0f, 0.5f, -8.0f), new Vector3f(1.0f, 1.0f, 1.0f));

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>().SetSpeed(20.0f);
        var camera = _cameraNode.CreateComponent<Camera>();
        void UpdateShadowCascades() => light.SetShadowCascade(ShadowCascadeEstimator.Estimate(camera,
            new Vector2i(Graphics.Width, Graphics.Height), new RenderPipelineSettings(),
            new ShadowCascadeEstimateSettings(80.0f) { ShadowResolution = light.GetShadowResolution() }));
        UpdateShadowCascades();
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => UpdateShadowCascades());
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, -20.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 4.0f, 0.0f));
    }

    private void CreateMarker(Vector3f position, Vector3f scale)
    {
        var node = _scene!.CreateChild("CalibrationMarker");
        node.SetPosition(position);
        node.SetScale(scale);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/Accent.xml"));
        model.SetCastShadows(true);
    }

    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _inputMap = SampleInputMaps.Create();
        _inputMap.MapMouseButton("Throw", MouseButton.Left);
        _inputMap.MapControllerButton("Throw", ControllerButton.A);
        _onScreenControls = new OnScreenControls(_inputMap,
            (OnScreenControl.Fire, "Throw", "THROW"));

        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png")
        ];
    }

    private void SpawnObject()
    {
        var boxNode = _scene!.CreateChild("SmallBox");
        boxNode.SetPosition(_cameraNode!.GetPosition());
        boxNode.SetRotation(_cameraNode.GetRotation());
        var boxObject = boxNode.CreateComponent<StaticModel>();
        boxObject.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        boxObject.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/Accent.xml"));
        boxObject.SetCastShadows(true);
        boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = boxNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        body.Friction = 0.75f;
        body.LinearVelocity = _cameraNode.GetRotation() * new Vector3f(0.0f, 0.25f, 1.0f) * 10.0f;
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _inputMap!.EvaluatePressed("Exit"))
        {
            Exit();
            return;
        }

        if (_inputMap.EvaluatePressed("Throw"))
            SpawnObject();
    }

    protected override void Stop()
    {
        _onScreenControls?.Dispose();
        _onScreenControls = null;
        _inputMap?.Dispose();
        _inputMap = null;
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private void DrawInstructions()
    {
        _onScreenControls?.Draw(OnScreenControl.Fire, showJoystick: false);
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Physics"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Move"u8);
            DrawPrompt(_controlPrompts[4], "Look"u8);
            DrawPrompt(_controlPrompts[5], "Throw box"u8);
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

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics";
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Texture2D[]? _controlPrompts;
    private OnScreenControls? _onScreenControls;
    private InputMap? _inputMap;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
}
