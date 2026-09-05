// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.AdvancedNetworking;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Network;
using RedBolt.PhysicsJolt;
using RedBolt.Replica;
using RedBolt.Resource;
using RedBolt.Sample.CharacterControls;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class AdvancedNetworkingProgram
{
    private static int Main()
    {
        try
        {
            using var application = new AdvancedNetworking();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Animates and orients a replicated Jolt character.</summary>
[Reflected]
public partial class AdvancedNetworkingPlayer : NetworkBehavior
{
    private const uint ImportantViewMask = 0x1;
    private const uint UnimportantViewMask = 0x2;

    public AdvancedNetworkingPlayer()
        : base(NetworkCallbackFlags.Update)
    {
    }

    public override void InitializeOnServer()
    {
        InitializeCommon();
        if (_animatedModel is not null)
            _animatedModel.ViewMask = UnimportantViewMask;
    }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination) =>
        destination.WriteColor(GetPlayerLight()!.Color);

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        InitializeCommon();
        SetPlayerColor(source.ReadColor());
        if (_animatedModel is not null)
            _animatedModel.ViewMask = isOwned ? UnimportantViewMask : ImportantViewMask;
    }

    public void SetPlayerColor(Color color)
    {
        GetPlayerLight()?.SetColor(color);
        _animatedModel ??= Node?.GetComponent<AnimatedModel>();
        if (_animatedModel?.GetMaterial() is not { } sourceMaterial)
            return;

        _playerMaterial = sourceMaterial.Clone();
        _playerMaterial.SetShaderParameter("MatDiffColor", color.ToVector4());
        _animatedModel.SetMaterial(0, _playerMaterial);
        _animatedModel.SetMaterial(1, _playerMaterial);
    }

    public override void Update(float replicaTimeStep, float inputTimeStep)
    {
        if (NetworkObject?.IsReplicatedClient != true)
            UpdateAnimations(inputTimeStep);
    }

    private void InitializeCommon()
    {
        _animatedModel = Node!.GetComponent<AnimatedModel>();
        _animationController = Node.GetComponent<AnimationController>();
        _networkController = NetworkObject?.GetNetworkBehavior<PredictedKinematicController>();
        _kinematicController = Node.FindComponent<KinematicCharacterController>(
            ComponentSearchFlag.SelfOrParentRecursive | ComponentSearchFlag.Derived);
    }

    private void UpdateAnimations(float timeStep)
    {
        if (_animationController is null || _networkController is null || _kinematicController is null)
            return;

        if (_interactionTime > 0.0f)
        {
            _interactionTime -= timeStep;
            return;
        }

        var velocity = _networkController.Velocity;
        var walkDirection = new Vector3f(velocity.X, 0.0f, velocity.Z).NormalizedOrDefault();
        if (_currentAnimation != AnimationKind.Jump && velocity.Y > 5.0f)
        {
            _animationController.PlayExclusive(JumpAnimation, 0, false, 0.1f);
            _animationController.SetTime(JumpAnimation, 0.0f);
            _currentAnimation = AnimationKind.Jump;
        }

        if (walkDirection != Vector3f.Zero)
        {
            var targetRotation = new Quaternion(Vector3f.Forward, walkDirection);
            Node!.SetWorldRotation(Node.GetWorldRotation().Slerp(targetRotation,
                System.Math.Clamp(timeStep * 8.0f, 0.0f, 1.0f)));
        }

        if (_kinematicController.OnGround)
        {
            _currentAnimation = walkDirection == Vector3f.Zero ? AnimationKind.Idle : AnimationKind.Walk;
            _animationController.PlayExclusive(
                _currentAnimation == AnimationKind.Walk ? WalkAnimation : IdleAnimation,
                0, true, 0.1f);
            if (_currentAnimation == AnimationKind.Walk)
            {
                var animation = GetSubsystem<ResourceCache>()!.Get<Animation>(WalkAnimation);
                var planarSpeed = new Vector2f(velocity.X, velocity.Z).Length;
                _animationController.SetSpeed(WalkAnimation,
                    RootMotionAnimation.GetSpeedScale(animation, planarSpeed));
            }
        }
    }

    private Light? GetPlayerLight() => Node?.Parent?.FindComponent<Light>();

    public void PlayInteraction()
    {
        if (_animationController is null)
            return;
        var animation = GetSubsystem<ResourceCache>()!.Get<Animation>(InteractionAnimation);
        _interactionTime = animation.Length;
        _animationController.PlayExclusive(InteractionAnimation, 0, false, 0.1f);
        _animationController.SetTime(InteractionAnimation, 0.0f);
        _currentAnimation = AnimationKind.Interact;
    }

    private enum AnimationKind { Idle, Walk, Jump, Interact }

    private const string IdleAnimation = "ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Idle_Loop.ani";
    private const string WalkAnimation = "ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Jog_Fwd_Loop.ani";
    private const string JumpAnimation = "ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Jump_Loop.ani";
    private const string InteractionAnimation = "ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Interact.ani";
    private AnimatedModel? _animatedModel;
    private AnimationController? _animationController;
    private Material? _playerMaterial;
    private PredictedKinematicController? _networkController;
    private KinematicCharacterController? _kinematicController;
    private AnimationKind _currentAnimation;
    private float _interactionTime;
}

internal sealed class AdvancedNetworkingConnection : DataChannelConnection
{
    public AdvancedNetworkingConnection()
    {
        ReplicationPeer = new ReplicatedPeer(this);
        Statistics = new NetworkStatisticsCounter(this);
    }

    public ReplicatedPeer ReplicationPeer { get; }
    public NetworkStatisticsCounter Statistics { get; }
    public Node? ControlledObject { get; set; }

    public override void Dispose()
    {
        ReplicationPeer.Dispose();
        base.Dispose();
    }
}

internal sealed record ServerRaycastInfo(
    AdvancedNetworkingConnection Connection,
    Vector3d Target,
    NetworkTime ReplicaTime,
    NetworkTime InputTime);

/// <summary>Jolt KCC prediction and lag-compensated raycast networking sample.</summary>
public sealed class AdvancedNetworking : Application
{
    private const ushort ServerPort = 2345;
    private const uint ImportantViewMask = 0x1;
    private const uint UnimportantViewMask = 0x2;
    private const float CameraDistance = 5.0f;
    private const float CameraOffset = 2.0f;
    private const float WalkVelocity = 3.35f;
    private const float HitDistance = 100.0f;
    private const int AddressBufferSize = 256;
    private static readonly NetworkMessageId RaycastMessage =
        (NetworkMessageId)((uint)NetworkMessageId.User + 100u);
    private static readonly NetworkMessageId RayHitMessage =
        (NetworkMessageId)((uint)NetworkMessageId.User + 101u);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AdvancedNetworking";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Advanced Networking (Jolt KCC)";
    }

    protected override void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
        parser.AddFlag(["--start-server"], "Start the authoritative server on launch.",
            () => _startServerOnLaunch = true);
        parser.AddOption(["--connect"], "address", "Connect to a server on launch.",
            address => _connectOnLaunch = address);
        parser.AddFlag(["--auto-move"], "Move the client in a circle.", () => _autoMovement = true);
        parser.AddFlag(["--auto-click"], "Continuously test lag-compensated raycasts.", () => _autoClick = true);
        parser.AddFlag(["--auto-aim"], "Aim raycasts at the closest remote player's hand.", () => _autoAim = true);
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context);
        Context.RegisterAssembly(typeof(AdvancedNetworkingPlayer).Assembly);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _controlPrompts = LoadControlPrompts();
        _inputMap = SampleInputMaps.Create(movement: true);
        SampleInputMaps.MapLook(_inputMap);
        SampleInputMaps.MapButton(_inputMap, "Jump", Scancode.Space, ControllerButton.A);
        _inputMap.MapMouseButton("Fire", MouseButton.Left);
        _inputMap.MapControllerButton("Fire", ControllerButton.X);
        _onScreenControls = new OnScreenControls(_inputMap,
            (OnScreenControl.Jump, "Jump", "JUMP"),
            (OnScreenControl.Fire, "Fire", "FIRE"));

        _addressBuffer = Marshal.AllocHGlobal(AddressBufferSize);
        SetAddressBuffer(string.IsNullOrWhiteSpace(_connectOnLaunch) ? "localhost" : _connectOnLaunch);
        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInterface());
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        if (_startServerOnLaunch)
            StartServer();
        else if (!string.IsNullOrWhiteSpace(_connectOnLaunch))
            Connect();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _inputMap!.EvaluatePressed("Exit"))
        {
            Exit();
            return;
        }

        ProcessServerRaycasts();
        UpdateControl(timeStep);
        _statisticsTime += timeStep;
        if (_statisticsTime >= 1.0f)
        {
            _statisticsTime %= 1.0f;
            UpdateStatistics();
        }
    }

    protected override void Stop()
    {
        _onScreenControls?.Dispose();
        _onScreenControls = null;
        _inputMap?.Dispose();
        _inputMap = null;
        StopNetworking();
        _server?.Dispose();
        _server = null;
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _hitMarkers = null;
        _scene?.Dispose();
        _scene = null;
        if (_addressBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_addressBuffer);
            _addressBuffer = IntPtr.Zero;
        }
        base.Stop();
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();
        _scene.CreateComponent<ReplicationManager>();

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.5f, 0.5f, 0.5f));
        zone.SetFogColor(new Color(0.4f, 0.5f, 0.8f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, -0.5f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.2f, 0.2f, 0.2f));
        light.SetCastShadows(true);

        _hitMarkers = _scene.CreateChild("Hit Markers");
        var boxModel = Resources.Get<Model>("Models/Box.mdl");
        var floorMaterial = Resources.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml");
        var obstacleMaterial = Resources.Get<Material>("Materials/TechnicalDemo/Dark.xml");
        var accentMaterial = Resources.Get<Material>("Materials/TechnicalDemo/Accent.xml");
        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var floorModel = floorNode.CreateComponent<StaticModel>();
        floorModel.SetModel(boxModel);
        floorModel.SetMaterial(floorMaterial);
        floorModel.ViewMask = ImportantViewMask;
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        floorNode.CreateComponent<RigidBody>().Friction = 1.0f;

        var random = new Random(0);
        for (var i = 0; i < 140; ++i)
        {
            var scale = new Vector3f(
                RandomRange(random, 0.75f, 2.5f),
                RandomRange(random, 0.4f, 1.2f),
                RandomRange(random, 0.75f, 2.5f));
            var obstacle = _scene.CreateChild("Box");
            float x;
            float z;
            do
            {
                x = RandomRange(random, -45.0f, 45.0f);
                z = RandomRange(random, -45.0f, 45.0f);
            } while (x * x + z * z < 100.0f);
            obstacle.SetPosition(new Vector3f(x, scale.Y * 0.5f, z));
            obstacle.SetRotation(new Quaternion(0.0f, RandomRange(random, 0.0f, 360.0f), 0.0f));
            obstacle.SetScale(scale);
            var model = obstacle.CreateComponent<StaticModel>();
            model.SetModel(boxModel);
            model.SetMaterial(i % 12 == 0 ? accentMaterial : obstacleMaterial);
            model.ViewMask = ImportantViewMask;
            model.SetCastShadows(true);
            obstacle.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
            obstacle.CreateComponent<RigidBody>().Friction = 1.0f;
        }

        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    private void DrawInterface()
    {
        _onScreenControls?.Draw(OnScreenControl.Jump | OnScreenControl.Fire, true);
        var connected = _clientConnection?.IsConnected == true;
        var listening = _server?.IsListening == true;
        var hasControls = GetClientObject() is not null;
        var windowHeight = hasControls ? 585.0f : connected || listening ? 410.0f : 445.0f;
        ImGui.SetNextWindowPos(new Vector2f(18.0f, 18.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(420.0f, windowHeight), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 12.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(14.0f, 14.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 6.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColCheckMark, AccentText);
        var open = true;
        if (ImGui.Begin("Advanced Networking (Jolt KCC)"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
            ImGui.TextUnformatted("NETWORK LAB"u8);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
            ImGui.TextUnformatted("Prediction, replication and temporal raycasts"u8);
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2f(1.0f, 7.0f));

            BeginPanel("##Session"u8, connected || listening ? 76.0f : 112.0f);
            DrawSectionLabel("SESSION"u8);
            if (!connected && !listening)
            {
                ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
                ImGui.TextUnformatted("Server address"u8);
                ImGui.PopStyleColor();
                ImGui.SetNextItemWidth(-1.0f);
                ImGui.InputText("##ServerAddress"u8, _addressBuffer, AddressBufferSize);
                if (ImGui.Button("Connect"u8)) Connect();
#if !REDBOLT_PLATFORM_BROWSER
                    ImGui.SameLine();
                    if (ImGui.Button("Start server"u8)) StartServer();
#endif
            }
            else if (ImGui.Button("Stop networking"u8))
                StopNetworking();
            EndPanel();

            ImGui.Dummy(new Vector2f(1.0f, 8.0f));
            BeginPanel("##Simulation"u8, 112.0f);
            DrawSectionLabel("SIMULATION"u8);
            ImGui.Checkbox("Automatic movement"u8, ref _autoMovement);
            ImGui.Checkbox("Aim at closest hand"u8, ref _autoAim);
            ImGui.Checkbox("Automatic raycasts"u8, ref _autoClick);
            EndPanel();

            ImGui.Dummy(new Vector2f(1.0f, 8.0f));
            BeginPanel("##Telemetry"u8, 100.0f);
            var mode = connected ? "Client connected" : listening
                ? $"Server running, clients: {_serverConnections.Count}" : "Offline";
            DrawSectionLabel("TELEMETRY"u8);
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, connected || listening ? StatusOnline : StatusOffline);
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes(mode));
            ImGui.PopStyleColor();
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes(
                $"Packets in/out: {_statistics.PacketsInPerSec} / {_statistics.PacketsOutPerSec}"));
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes(
                $"Bytes in/out: {_statistics.BytesInPerSec} / {_statistics.BytesOutPerSec}"));
            EndPanel();

            if (hasControls)
            {
                ImGui.Dummy(new Vector2f(1.0f, 8.0f));
                BeginPanel("##Controls"u8, 157.0f);
                DrawSectionLabel("CONTROLS"u8);
                if (Input.ActiveInputDevice == InputDeviceType.KeyboardMouse)
                {
                    DrawMovementPrompt("Move"u8);
                    DrawPrompt(_controlPrompts![4], "Jump"u8);
                    DrawPrompt(_controlPrompts[5], "Orbit camera"u8);
                    DrawPrompt(_controlPrompts[6], "Temporal raycast"u8);
                }
                else
                {
                    DrawPrompt(Input.ActiveInputDevice == InputDeviceType.Touch
                            ? SampleInputMap.GetMovementPrompt()
                            : SampleInputMap.GetControllerPrompt(ControllerButton.LeftStick),
                        Input.ActiveInputDevice == InputDeviceType.Touch ? "Drag left side to move"u8 : "Move"u8);
                    DrawPrompt(SampleInputMap.GetControllerPrompt(ControllerButton.A), "Jump"u8);
                    DrawPrompt(Input.ActiveInputDevice == InputDeviceType.Touch
                            ? Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/touch_swipe_move.png")
                            : SampleInputMap.GetControllerPrompt(ControllerButton.RightStick),
                        Input.ActiveInputDevice == InputDeviceType.Touch ? "Swipe right side to orbit camera"u8 : "Orbit camera"u8);
                    DrawPrompt(SampleInputMap.GetControllerPrompt(ControllerButton.X), "Temporal raycast"u8);
                }
                EndPanel();
            }
        }
        ImGui.End();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(4);
    }

    private static void BeginPanel(ReadOnlySpan<byte> id, float height)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildRounding, 9.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(12.0f, 10.0f));
        ImGui.BeginChild(id, new Vector2f(ImGui.GetContentRegionAvail().X, height),
            ImGuiChildFlags.ImGuiChildFlagsBorders);
    }

    private static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    private static void DrawSectionLabel(ReadOnlySpan<byte> label)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentText);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
    }

    private Texture2D[] LoadControlPrompts() =>
    [
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png")
    ];

    private void DrawMovementPrompt(ReadOnlySpan<byte> label)
    {
        for (var i = 0; i < 3; ++i)
        {
            ImGui.Image(_controlPrompts![i], PromptIconSize);
            ImGui.SameLine();
        }
        DrawPrompt(_controlPrompts![3], label);
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, PromptIconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private void StartServer()
    {
        StopNetworking();
        _server = new DataChannelServer();
        _server.SetConnectionFactory(static () => new AdvancedNetworkingConnection());
        _server.Connected += HandleServerConnected;
        _server.Disconnected += HandleServerDisconnected;
        _scene!.GetComponent<ReplicationManager>()!.StartServer();
        if (!_server.Listen(new URL($"ws://0.0.0.0:{ServerPort}")))
        {
            _server.Dispose();
            _server = null;
            _scene.GetComponent<ReplicationManager>()!.StartStandalone();
        }
    }

    private void Connect()
    {
        StopNetworking();
        var address = (Marshal.PtrToStringUTF8(_addressBuffer) ?? string.Empty).Trim();
        if (address.Length == 0)
            address = "localhost";
        _clientConnection = new AdvancedNetworkingConnection();
        _clientConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        _clientConnection.MessageReceived += HandleClientMessage;
        if (!_clientConnection.Connect(new URL(address.Contains("://", StringComparison.Ordinal)
                ? address
                : $"ws://{address}:{ServerPort}")))
        {
            _clientConnection.Dispose();
            _clientConnection = null;
        }
    }

    private void StopNetworking()
    {
        if (_clientConnection is not null)
        {
            _clientConnection.MessageReceived -= HandleClientMessage;
            _clientConnection.Disconnect();
            _clientConnection.Dispose();
            _clientConnection = null;
        }

        if (_server is not null)
        {
            _server.Connected -= HandleServerConnected;
            _server.Disconnected -= HandleServerDisconnected;
            foreach (var connection in _serverConnections.Keys.ToArray())
            {
                connection.MessageReceived -= HandleServerMessage;
                connection.ControlledObject?.Remove();
                connection.ControlledObject = null;
                connection.Disconnect();
                connection.Dispose();
            }
            _serverConnections.Clear();
            _serverRaycasts.Clear();
            _server.Stop();
            _server.Dispose();
            _server = null;
            _scene?.GetComponent<ReplicationManager>()?.StartStandalone();
        }
    }

    private void HandleServerConnected(NetworkServer server, NetworkConnection connection)
    {
        var advancedConnection = (AdvancedNetworkingConnection)connection;
        advancedConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        advancedConnection.MessageReceived += HandleServerMessage;
        advancedConnection.ControlledObject = CreateControllableObject(advancedConnection.ReplicationPeer);
        _serverConnections.Add(advancedConnection, advancedConnection.ControlledObject);
    }

    private void HandleServerDisconnected(NetworkServer server, NetworkConnection connection)
    {
        var advancedConnection = (AdvancedNetworkingConnection)connection;
        advancedConnection.MessageReceived -= HandleServerMessage;
        advancedConnection.ControlledObject?.Remove();
        advancedConnection.ControlledObject = null;
        _serverConnections.Remove(advancedConnection);
        _serverRaycasts.RemoveAll(item => ReferenceEquals(item.Connection, advancedConnection));
        advancedConnection.Dispose();
    }

    private Node CreateControllableObject(ReplicatedPeer owner)
    {
        var prefab = Resources.Get<PrefabResource>("Prefabs/AdvancedNetworkingPlayer.prefab")!;
        var playerNode = _scene!.InstantiatePrefab(prefab.GetNodePrefab(), new Vector3f(
            (float)Random.Shared.NextDouble() * 10.0f - 5.0f, 5.0f,
            (float)Random.Shared.NextDouble() * 10.0f - 5.0f), Quaternion.IDENTITY)!;
        var networkObject = playerNode.CreateComponent<BehaviorNetworkObject>();
        networkObject.SetClientPrefab(prefab);
        networkObject.SetOwner(owner);
        playerNode.FindComponent<AdvancedNetworkingPlayer>()?.SetPlayerColor(NextPlayerColor());
        return playerNode;
    }

    private Color NextPlayerColor()
    {
        var color = new Color();
        color.FromHSV((_nextPlayerColor++ * 0.618034f) % 1.0f, 0.72f, 1.0f);
        return color;
    }

    private void UpdateControl(float timeStep)
    {
        var keyboardMouse = Input.ActiveInputDevice == InputDeviceType.KeyboardMouse;
        var cameraMoving = keyboardMouse && Input.GetMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse;
        Input.SetMouseMode(cameraMoving ? MouseMode.Relative : MouseMode.Free);
        Input.SetMouseVisible(!cameraMoving);
        if (cameraMoving)
        {
            var mouseMove = Input.GetMouseMove();
            _yaw += 0.1f * mouseMove.X;
            _pitch = System.Math.Clamp(_pitch + 0.1f * mouseMove.Y, 1.0f, 90.0f);
        }
        else if (Input.ActiveInputDevice == InputDeviceType.Joystick)
        {
            _yaw += (_inputMap!.Evaluate("TurnRight") - _inputMap.Evaluate("TurnLeft")) * 90.0f * timeStep;
            _pitch = System.Math.Clamp(_pitch
                + (_inputMap.Evaluate("LookDown") - _inputMap.Evaluate("LookUp")) * 90.0f * timeStep,
                1.0f, 90.0f);
        }
        else if (Input.ActiveInputDevice == InputDeviceType.Touch)
        {
            for (var i = 0u; i < Input.GetNumTouches(); ++i)
            {
                var touch = Input.GetTouch(i)!.Value;
                if (touch.Position.X < Graphics.Width / 2 || _onScreenControls!.IsTouchCaptured(touch.TouchID))
                    continue;
                _yaw += 0.1f * touch.Delta.X;
                _pitch = System.Math.Clamp(_pitch + 0.1f * touch.Delta.Y, 1.0f, 90.0f);
            }
        }
        _cameraNode!.SetRotation(new Quaternion(_pitch, _yaw, 0.0f));

        var clientObject = GetClientObject();
        if (clientObject is null)
        {
            UpdateServerCamera(timeStep);
            return;
        }

        _autoMovementTime += timeStep;
        if (_autoMovement && _autoMovementTime >= 1.0f)
        {
            _autoMovementTime %= 1.0f;
            _autoMovementPhase = (_autoMovementPhase + 1) % 4;
        }
        var rotation = new Quaternion(0.0f, _yaw, 0.0f);
        var direction = Vector3f.Zero;
        if (!ImGui.GetIO().WantCaptureKeyboard)
        {
            var forwardAmount = _inputMap!.Evaluate("Forward") - _inputMap.Evaluate("Back");
            var rightAmount = _inputMap.Evaluate("Right") - _inputMap.Evaluate("Left");
            direction = rotation * (Vector3f.Forward * forwardAmount + Vector3f.Right * rightAmount);
            if (_autoMovement)
            {
                direction = rotation * (_autoMovementPhase switch
                {
                    0 => Vector3f.Right,
                    1 => Vector3f.Back,
                    2 => Vector3f.Left,
                    _ => Vector3f.Forward
                });
            }
            if (direction.LengthSquared > 1.0f)
                direction.Normalize();
        }
        var controller = clientObject.GetComponent<PredictedKinematicController>();
        controller?.SetWalkVelocity(direction * WalkVelocity);
        if ((Input.ActiveInputDevice == InputDeviceType.Touch
                || !ImGui.GetIO().WantCaptureKeyboard) && _inputMap!.Evaluate("Jump") != 0.0f)
            controller?.SetJump();

        _cameraNode.SetPosition(clientObject.GetPosition()
            + _cameraNode.GetRotation() * Vector3f.Back * CameraDistance + Vector3f.Up * CameraOffset);

        _autoClickTime += timeStep;
        var automaticClick = _autoClick && _autoClickTime >= 0.25f;
        if (automaticClick)
            _autoClickTime %= 0.25f;
        var touchInput = Input.ActiveInputDevice == InputDeviceType.Touch;
        var firePressed = _onScreenControls!.IsPressed(OnScreenControl.Fire);
        if (((touchInput || !ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard)
                && firePressed) || automaticClick)
        {
            var viewport = Renderer.GetViewport();
            var mousePosition = Input.GetMousePosition();
            if (viewport is not null)
                RequestClientRaycast(clientObject, viewport.GetScreenRay(mousePosition.X, mousePosition.Y));
        }
    }

    private void UpdateServerCamera(float timeStep)
    {
        if (_server?.IsListening != true || ImGui.GetIO().WantCaptureKeyboard)
            return;
        var direction = Vector3f.Zero;
        if (_inputMap!.Evaluate("Forward") != 0.0f) direction += Vector3f.Forward;
        if (_inputMap.Evaluate("Back") != 0.0f) direction += Vector3f.Back;
        if (_inputMap.Evaluate("Left") != 0.0f) direction += Vector3f.Left;
        if (_inputMap.Evaluate("Right") != 0.0f) direction += Vector3f.Right;
        _cameraNode!.SetPosition(_cameraNode.GetPosition()
            + _cameraNode.GetRotation() * direction.NormalizedOrDefault() * (20.0f * timeStep));
    }

    private Node? GetClientObject() =>
        _scene?.GetComponent<ReplicationManager>()?.ClientReplica?.OwnedNetworkObject?.Node;

    private void RequestClientRaycast(Node clientObject, Ray screenRay)
    {
        var clientReplica = _scene!.GetComponent<ReplicationManager>()?.ClientReplica;
        if (_clientConnection?.IsConnected != true || clientReplica is null)
            return;
        clientObject.FindComponent<AdvancedNetworkingPlayer>()?.PlayInteraction();
        var playerPosition = _scene.ToAbsoluteWorldPosition(clientObject.GetWorldPosition());
        var aimPosition = GetAimPosition(playerPosition, screenRay);
        var origin = playerPosition + Vector3d.Up * CameraOffset;
        var castRay = new Ray(_scene.ToRelativeWorldPosition(origin), ToVector3f(aimPosition - origin));
        if (RaycastImportantGeometries(castRay) is { } predictedHit)
            AddHitMarker(_scene.ToAbsoluteWorldPosition(predictedHit), false);

        using var data = new VectorBuffer();
        data.WritePackedVector3(origin, VectorBinaryEncoding.Double);
        data.WritePackedVector3(aimPosition, VectorBinaryEncoding.Double);
        WriteNetworkTime(data, clientReplica.ReplicaTime);
        WriteNetworkTime(data, clientReplica.InputTime);
        using var message = new MemoryBuffer(data);
        _clientConnection.SendMessage(RaycastMessage, message, PacketType.UnreliableUnordered);
    }

    private Vector3d GetAimPosition(Vector3d playerPosition, Ray screenRay)
    {
        if (_autoAim)
        {
            AnimatedModel? closest = null;
            var closestDistance = float.PositiveInfinity;
            foreach (var model in _scene!.FindComponents<AnimatedModel>())
            {
                if (model.ViewMask == UnimportantViewMask)
                    continue;
                var distance = model.Node!.GetWorldPosition().DistanceToPoint(
                    _scene.ToRelativeWorldPosition(playerPosition));
                if (distance < closestDistance)
                {
                    closest = model;
                    closestDistance = distance;
                }
            }
            if (closest?.GetSkeleton().GetBone("index_02_r")?.Node is { } hand)
                return _scene.ToAbsoluteWorldPosition(hand.GetWorldPosition());
        }

        var defaultAim = screenRay.Origin + screenRay.Direction * HitDistance;
        return _scene!.ToAbsoluteWorldPosition(RaycastImportantGeometries(screenRay) ?? defaultAim);
    }

    private Vector3f? RaycastImportantGeometries(Ray ray)
    {
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle,
            HitDistance, DrawableFlags.Geometry, ImportantViewMask);
        _scene!.GetComponent<Octree>()!.RaycastSingle(query);
        return query.Result.Count != 0 ? query.Result[0].Position : null;
    }

    private void HandleServerMessage(NetworkConnection connection, NetworkMessageId messageId,
        MemoryBuffer message, ref bool handled)
    {
        if (handled || messageId != RaycastMessage)
            return;
        var advancedConnection = (AdvancedNetworkingConnection)connection;
        _ = message.ReadPackedVector3(VectorBinaryEncoding.Double);
        var target = message.ReadPackedVector3(VectorBinaryEncoding.Double);
        _serverRaycasts.Add(new ServerRaycastInfo(advancedConnection, target,
            ReadNetworkTime(message), ReadNetworkTime(message)));
        handled = true;
    }

    private void ProcessServerRaycasts()
    {
        var manager = _scene?.GetComponent<ReplicationManager>();
        var replicator = manager?.ServerReplicator;
        if (manager is null || replicator is null)
            return;
        var serverTime = replicator.ServerTime;
        for (var i = _serverRaycasts.Count - 1; i >= 0; --i)
        {
            var item = _serverRaycasts[i];
            if (!item.Connection.IsConnected)
            {
                _serverRaycasts.RemoveAt(i);
                continue;
            }
            if (serverTime - item.InputTime < 0.0)
                continue;
            ProcessSingleRaycast(item, manager, replicator);
            _serverRaycasts.RemoveAt(i);
        }
    }

    private void ProcessSingleRaycast(ServerRaycastInfo item,
        ReplicationManager manager, ServerReplicator replicator)
    {
        var clientObject = replicator.GetNetworkObjectOwnedByConnection(item.Connection.ReplicationPeer);
        var replicatedTransform = clientObject?.Node?.FindComponent<ReplicatedTransform>();
        if (clientObject is null || replicatedTransform is null)
            return;
        clientObject.Node?.FindComponent<AdvancedNetworkingPlayer>()?.PlayInteraction();
        var origin = _scene!.ToRelativeWorldPosition(
            replicatedTransform.SampleTemporalPosition(item.InputTime).Value) + Vector3f.Up * CameraOffset;
        var target = _scene.ToRelativeWorldPosition(item.Target);
        using var query = new RayOctreeQuery(new Ray(origin, target - origin),
            RayQueryLevel.RayTriangle, float.PositiveInfinity, DrawableFlags.Geometry, ImportantViewMask);
        _scene.GetComponent<Octree>()!.RaycastSingle(query);
        foreach (var networkObject in manager.NetworkObjects)
        {
            if (ReferenceEquals(networkObject, clientObject) || networkObject is not BehaviorNetworkObject behavior)
                continue;
            behavior.GetNetworkBehavior<TrackedAnimatedModel>()?.ProcessTemporalRayQuery(
                item.ReplicaTime, query, query.Result);
        }
        query.Result.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));

        using var data = new VectorBuffer();
        data.WritePackedVector3(_scene.ToAbsoluteWorldPosition(origin), VectorBinaryEncoding.Double);
        data.WriteBool(query.Result.Count != 0);
        if (query.Result.Count != 0)
            data.WritePackedVector3(_scene.ToAbsoluteWorldPosition(query.Result[0].Position),
                VectorBinaryEncoding.Double);
        using var message = new MemoryBuffer(data);
        item.Connection.SendMessage(RayHitMessage, message, PacketType.UnreliableUnordered);
    }

    private void HandleClientMessage(NetworkConnection connection, NetworkMessageId messageId,
        MemoryBuffer message, ref bool handled)
    {
        if (handled || messageId != RayHitMessage)
            return;
        _ = message.ReadPackedVector3(VectorBinaryEncoding.Double);
        if (message.ReadBool())
            AddHitMarker(message.ReadPackedVector3(VectorBinaryEncoding.Double), true);
        handled = true;
    }

    private void AddHitMarker(Vector3d position, bool confirmed)
    {
        if (_hitMarkers is null)
            return;
        if (_hitMarkers.GetNumChildren() >= 200)
            _hitMarkers.GetChild(0)?.Remove();
        var marker = _hitMarkers.CreateChild(confirmed ? "Confirmed Hit" : "Predicted Hit");
        marker.SetPosition(_scene!.ToRelativeWorldPosition(position));
        marker.SetScale(confirmed ? 0.15f : 0.2f);
        var model = marker.CreateComponent<StaticModel>();
        model.ViewMask = UnimportantViewMask;
        model.SetModel(Resources.Get<Model>(confirmed ? "Models/Box.mdl" : "Models/Sphere.mdl"));
        model.SetMaterial(Resources.Get<Material>(confirmed
            ? "Materials/Constant/GlowingGreen.xml" : "Materials/Constant/GlowingRed.xml"));
    }

    private void UpdateStatistics()
    {
        _statistics = default;
        if (_clientConnection?.IsConnected == true)
        {
            _statistics = _clientConnection.Statistics.Snapshot;
            return;
        }
        foreach (var connection in _serverConnections.Keys)
            _statistics += connection.Statistics.Snapshot;
    }

    private void SetAddressBuffer(string address)
    {
        var bytes = Encoding.UTF8.GetBytes(address);
        var length = System.Math.Min(bytes.Length, AddressBufferSize - 1);
        Marshal.Copy(bytes, 0, _addressBuffer, length);
        Marshal.WriteByte(_addressBuffer, length, 0);
    }

    private static void WriteNetworkTime(Serializer message, NetworkTime time)
    {
        message.WriteInt64(time.Frame.Value);
        message.WriteFloat(time.Fraction);
    }

    private static NetworkTime ReadNetworkTime(Deserializer message) =>
        new(new NetworkFrame(message.ReadInt64()), message.ReadFloat());

    private static Vector3f ToVector3f(Vector3d value) =>
        new((float)value.X, (float)value.Y, (float)value.Z);

    private static float RandomRange(Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);

    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _hitMarkers;
    private DataChannelServer? _server;
    private AdvancedNetworkingConnection? _clientConnection;
    private readonly Dictionary<AdvancedNetworkingConnection, Node?> _serverConnections = [];
    private readonly List<ServerRaycastInfo> _serverRaycasts = [];
    private IntPtr _addressBuffer;
    private float _yaw;
    private float _pitch = 30.0f;
    private float _autoMovementTime;
    private int _autoMovementPhase;
    private float _autoClickTime;
    private float _statisticsTime;
    private NetworkStatisticsSnapshot _statistics;
    private bool _autoMovement;
    private bool _autoAim;
    private bool _autoClick;
    private int _nextPlayerColor;
    private bool _startServerOnLaunch;
    private string _connectOnLaunch = string.Empty;
    private Texture2D[]? _controlPrompts;
    private OnScreenControls? _onScreenControls;
    private InputMap? _inputMap;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
    private static readonly Vector4f HudBackground = new(0.055f, 0.065f, 0.085f, 0.96f);
    private static readonly Vector4f PanelBackground = new(0.075f, 0.09f, 0.125f, 0.94f);
    private static readonly Vector4f PanelBorder = new(0.22f, 0.3f, 0.44f, 0.8f);
    private static readonly Vector4f PrimaryText = new(0.94f, 0.97f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.58f, 0.66f, 0.76f, 1.0f);
    private static readonly Vector4f AccentText = new(0.35f, 0.85f, 1.0f, 1.0f);
    private static readonly Vector4f StatusOnline = new(0.35f, 0.9f, 0.55f, 1.0f);
    private static readonly Vector4f StatusOffline = new(0.95f, 0.55f, 0.35f, 1.0f);
}
