// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SceneReplication;

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

internal static class SceneReplicationProgram
{
    private static int Main()
    {
        try
        {
            using var application = new SceneReplication();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public struct PlayerControls
{
    public float Yaw;
    public uint Buttons;
}

/// <summary>Replicates the ball light color and returns owner controls to the authoritative server.</summary>
[Reflected]
public partial class SceneReplicationPlayer : NetworkBehavior
{
    public SceneReplicationPlayer()
        : base(NetworkCallbackFlags.UnreliableFeedback)
    {
    }

    public PlayerControls Controls { get; set; }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination) =>
        destination.WriteColor(GetComponent<Light>()!.Color);

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned) =>
        SetPlayerColor(source.ReadColor());

    public void SetPlayerColor(Color color)
    {
        GetComponent<Light>()!.SetColor(color);
        var model = GetComponent<StaticModel>()!;
        _playerMaterial = model.GetMaterial()!.Clone();
        _playerMaterial.SetShaderParameter("MatDiffColor", color.ToVector4());
        model.SetMaterial(_playerMaterial);
    }

    public override bool PrepareUnreliableFeedback(NetworkFrame frame) => true;

    public override void WriteUnreliableFeedback(NetworkFrame frame, Serializer destination)
    {
        destination.WriteFloat(Controls.Yaw);
        destination.WriteVLE(Controls.Buttons);
    }

    public override void ReadUnreliableFeedback(NetworkFrame feedbackFrame, Deserializer source)
    {
        if (_lastFeedbackFrame is { } lastFeedbackFrame && lastFeedbackFrame >= feedbackFrame)
            return;
        Controls = new PlayerControls { Yaw = source.ReadFloat(), Buttons = source.ReadVLE() };
        _lastFeedbackFrame = feedbackFrame;
    }

    private NetworkFrame? _lastFeedbackFrame;
    private Material? _playerMaterial;
}

internal sealed class ReplicatedDataChannelConnection : DataChannelConnection
{
    public ReplicatedDataChannelConnection()
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

/// <summary>Authoritative scene-replication sample using Jolt rigid-body physics.</summary>
public sealed class SceneReplication : Application
{
    private const ushort ServerPort = 2345;
    private const uint ControlForward = 1;
    private const uint ControlBack = 2;
    private const uint ControlLeft = 4;
    private const uint ControlRight = 8;
    private const int AddressBufferSize = 256;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SceneReplication";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Scene Replication (Jolt)";
    }

    protected override void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
        parser.AddFlag(["--start-server"], "Start the replication server on launch.",
            () => _startServerOnLaunch = true);
        parser.AddOption(["--connect"], "address", "Connect to a replication server on launch.",
            address => _connectOnLaunch = address);
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context);
        Context.RegisterAssembly(typeof(SceneReplicationPlayer).Assembly);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _inputMap = SampleInputMaps.Create(movement: true);
        _onScreenControls = new OnScreenControls(_inputMap);
        _controlPrompts = LoadControlPrompts();
        _addressBuffer = Marshal.AllocHGlobal(AddressBufferSize);
        Marshal.WriteByte(_addressBuffer, 0, 0);
        if (!string.IsNullOrWhiteSpace(_connectOnLaunch))
        {
            var addressBytes = Encoding.UTF8.GetBytes(_connectOnLaunch);
            var length = System.Math.Min(addressBytes.Length, AddressBufferSize - 1);
            Marshal.Copy(addressBytes, 0, _addressBuffer, length);
            Marshal.WriteByte(_addressBuffer, length, 0);
        }

        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(
            _scene!.GetComponent<PhysicsWorld>()!, HandlePhysicsPreStep);
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
        MoveCamera();
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
        Disconnect();
        _server?.Dispose();
        _server = null;
        Renderer.SetNumViewports(0);
        _cameraNode = null;
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
        zone.SetAmbientColor(new Color(0.24f, 0.27f, 0.34f));
        zone.SetFogColor(new Color(0.055f, 0.065f, 0.085f));
        zone.SetFogStart(70.0f);
        zone.SetFogEnd(160.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.5f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.65f, 0.68f, 0.72f));
        light.SetSpecularIntensity(1.0f);
        light.SetCastShadows(true);

        var boxModel = Resources.Get<Model>("Models/Box.mdl");
        var floorMaterial = Resources.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml");
        var darkMaterial = Resources.Get<Material>("Materials/TechnicalDemo/Dark.xml");
        var accentMaterial = Resources.Get<Material>("Materials/TechnicalDemo/Accent.xml");

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var floorObject = floorNode.CreateComponent<StaticModel>();
        floorObject.SetModel(boxModel);
        floorObject.SetMaterial(floorMaterial);
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        floorNode.CreateComponent<RigidBody>().Friction = 1.0f;

        var random = new Random(1);
        for (var i = 0; i < 64; ++i)
        {
            var scale = new Vector3f(
                RandomRange(random, 1.2f, 4.0f),
                RandomRange(random, 0.5f, 1.8f),
                RandomRange(random, 1.2f, 4.0f));
            float x;
            float z;
            do
            {
                x = RandomRange(random, -44.0f, 44.0f);
                z = RandomRange(random, -44.0f, 44.0f);
            } while (x * x + z * z < 100.0f);

            var obstacle = _scene.CreateChild("TechnicalObstacle");
            obstacle.SetPosition(new Vector3f(x, scale.Y * 0.5f, z));
            obstacle.SetRotation(new Quaternion(0.0f, RandomRange(random, 0.0f, 360.0f), 0.0f));
            obstacle.SetScale(scale);
            var obstacleModel = obstacle.CreateComponent<StaticModel>();
            obstacleModel.SetModel(boxModel);
            obstacleModel.SetMaterial(i % 8 == 0 ? accentMaterial : darkMaterial);
            obstacleModel.SetCastShadows(true);
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
        var clientConnected = _clientConnection?.IsConnected == true;
        var serverRunning = _server?.IsListening == true;
        var hasControls = GetPlayerObject() is not null;
        _onScreenControls?.Draw(OnScreenControl.None, showJoystick: hasControls);
        var windowHeight = hasControls ? 430.0f : clientConnected || serverRunning ? 315.0f : 350.0f;
        ImGui.SetNextWindowPos(new Vector2f(18.0f, 18.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(420.0f, windowHeight), ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 12.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(14.0f, 14.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 6.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, HudBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        var open = true;
        if (ImGui.Begin("Scene Replication (Jolt)"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
            ImGui.TextUnformatted("REPLICATION LAB"u8);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
            ImGui.TextUnformatted("Authoritative Jolt rigid-body replication"u8);
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2f(1.0f, 7.0f));

            BeginPanel("##Session"u8, clientConnected || serverRunning ? 76.0f : 112.0f);
            DrawSectionLabel("SESSION"u8);
            if (!clientConnected && !serverRunning)
            {
                ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
                ImGui.TextUnformatted("Server address"u8);
                ImGui.PopStyleColor();
                ImGui.SetNextItemWidth(-1.0f);
                ImGui.InputText("##ServerAddress"u8, _addressBuffer, AddressBufferSize);
                if (ImGui.Button("Connect"u8))
                    Connect();
#if !REDBOLT_PLATFORM_BROWSER
                    ImGui.SameLine();
                    if (ImGui.Button("Start server"u8))
                        StartServer();
#endif
            }
            else if (ImGui.Button("Disconnect"u8))
                Disconnect();
            EndPanel();

            ImGui.Dummy(new Vector2f(1.0f, 8.0f));
            BeginPanel("##Telemetry"u8, 116.0f);
            DrawSectionLabel("TELEMETRY"u8);
            var mode = clientConnected ? "Client connected" : serverRunning ? "Server running" : "Offline";
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText,
                clientConnected || serverRunning ? StatusOnline : StatusOffline);
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes(mode));
            ImGui.PopStyleColor();
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Packets in/out: {_statistics.PacketsInPerSec} / {_statistics.PacketsOutPerSec}"));
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Bytes in/out: {_statistics.BytesInPerSec} / {_statistics.BytesOutPerSec}"));
            ImGui.TextUnformatted(Encoding.UTF8.GetBytes($"Connections: {_connectionCount}"));
            EndPanel();

            if (hasControls)
            {
                ImGui.Dummy(new Vector2f(1.0f, 8.0f));
                BeginPanel("##Controls"u8, 100.0f);
                DrawSectionLabel("CONTROLS"u8);
                DrawMovementPrompt("Roll ball"u8);
                DrawPrompt(_controlPrompts![4], "Orbit camera"u8);
                EndPanel();
            }
        }
        ImGui.End();
        ImGui.PopStyleColor(2);
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
        Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png")
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

    private void MoveCamera()
    {
        if (_cameraNode is null)
            return;
        var rotating = Input.GetMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse;
        Input.SetMouseMode(rotating ? MouseMode.Relative : MouseMode.Free);
        Input.SetMouseVisible(!rotating);
        if (rotating)
        {
            var mouseMove = Input.GetMouseMove();
            _yaw += 0.1f * mouseMove.X;
            _pitch = System.Math.Clamp(_pitch + 0.1f * mouseMove.Y, 1.0f, 90.0f);
        }
        _cameraNode.SetRotation(new Quaternion(_pitch, _yaw, 0.0f));
        if (GetPlayerObject() is { } playerNode)
            _cameraNode.SetPosition(playerNode.GetPosition() + _cameraNode.GetRotation() * Vector3f.Back * 5.0f);
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (_clientConnection?.IsConnected == true)
        {
            if (GetPlayerObject()?.GetComponent<SceneReplicationPlayer>() is { } player)
            {
                var controls = new PlayerControls { Yaw = _yaw };
                if (!ImGui.GetIO().WantCaptureKeyboard)
                {
                    if (_inputMap!.Evaluate("Forward") != 0.0f) controls.Buttons |= ControlForward;
                    if (_inputMap.Evaluate("Back") != 0.0f) controls.Buttons |= ControlBack;
                    if (_inputMap.Evaluate("Left") != 0.0f) controls.Buttons |= ControlLeft;
                    if (_inputMap.Evaluate("Right") != 0.0f) controls.Buttons |= ControlRight;
                }
                player.Controls = controls;
            }
            return;
        }

        if (_server?.IsListening != true)
            return;
        foreach (var connection in _serverConnections)
        {
            if (connection.ControlledObject is not { } playerNode)
                continue;
            var body = playerNode.GetComponent<RigidBody>()!;
            var controls = playerNode.GetComponent<SceneReplicationPlayer>()!.Controls;
            var rotation = new Quaternion(0.0f, controls.Yaw, 0.0f);
            const float moveTorque = 3.0f;
            if ((controls.Buttons & ControlForward) != 0) body.ApplyTorque(rotation * Vector3f.Right * moveTorque);
            if ((controls.Buttons & ControlBack) != 0) body.ApplyTorque(rotation * Vector3f.Left * moveTorque);
            if ((controls.Buttons & ControlLeft) != 0) body.ApplyTorque(rotation * Vector3f.Forward * moveTorque);
            if ((controls.Buttons & ControlRight) != 0) body.ApplyTorque(rotation * Vector3f.Back * moveTorque);
        }
    }

    private Node CreateControllableObject(ReplicatedPeer owner)
    {
        var prefab = Resources.Get<PrefabResource>("Prefabs/SceneReplicationPlayer.prefab")!;
        var position = new Vector3f(
            (float)Random.Shared.NextDouble() * 40.0f - 20.0f,
            5.0f,
            (float)Random.Shared.NextDouble() * 40.0f - 20.0f);
        var playerNode = _scene!.InstantiatePrefab(prefab.GetNodePrefab(), position, Quaternion.IDENTITY)!;
        playerNode.SetName("Ball");
        var networkObject = playerNode.CreateComponent<BehaviorNetworkObject>();
        networkObject.SetClientPrefab(prefab);
        networkObject.SetOwner(owner);
        playerNode.CreateComponent<CollisionShape>().SetSphere(1.0f);
        var body = playerNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        body.Friction = 1.0f;
        body.LinearDamping = 0.5f;
        body.AngularDamping = 0.5f;
        playerNode.GetComponent<SceneReplicationPlayer>()!.SetPlayerColor(NextPlayerColor());
        return playerNode;
    }

    private Color NextPlayerColor()
    {
        var color = new Color();
        color.FromHSV((_nextPlayerColor++ * 0.618034f) % 1.0f, 0.72f, 1.0f);
        return color;
    }

    private Node? GetPlayerObject() =>
        _scene?.GetComponent<ReplicationManager>()?.ClientReplica?.OwnedNetworkObject?.Node;

    private void Connect()
    {
        Disconnect();
        var address = (Marshal.PtrToStringUTF8(_addressBuffer) ?? string.Empty).Trim();
        if (address.Length == 0)
            address = "localhost";
        _clientConnection = new ReplicatedDataChannelConnection();
        _clientConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        _clientConnection.Connect(new URL(address.Contains("://", StringComparison.Ordinal)
            ? address
            : $"ws://{address}:{ServerPort}"));
    }

    private void StartServer()
    {
        if (_server is null)
        {
            _server = new DataChannelServer();
            _server.SetConnectionFactory(static () => new ReplicatedDataChannelConnection());
            _server.Connected += HandleServerConnected;
            _server.Disconnected += HandleServerDisconnected;
        }
        _scene!.GetComponent<ReplicationManager>()!.StartServer();
        _server.Listen(new URL($"ws://0.0.0.0:{ServerPort}"));
    }

    private void Disconnect()
    {
        if (_clientConnection is not null)
        {
            _clientConnection.Disconnect();
            _clientConnection.Dispose();
            _clientConnection = null;
        }
        if (_server?.IsListening == true)
        {
            foreach (var connection in _serverConnections.ToArray())
                connection.Disconnect();
            _serverConnections.Clear();
            _server.Stop();
            _scene?.GetComponent<ReplicationManager>()?.StartStandalone();
        }
    }

    private void HandleServerConnected(NetworkServer server, NetworkConnection connection)
    {
        var replicatedConnection = (ReplicatedDataChannelConnection)connection;
        replicatedConnection.ReplicationPeer.SetReplicationManager(_scene!.GetComponent<ReplicationManager>());
        replicatedConnection.ControlledObject = CreateControllableObject(replicatedConnection.ReplicationPeer);
        _serverConnections.Add(replicatedConnection);
    }

    private void HandleServerDisconnected(NetworkServer server, NetworkConnection connection)
    {
        var replicatedConnection = (ReplicatedDataChannelConnection)connection;
        replicatedConnection.ControlledObject?.Remove();
        replicatedConnection.ControlledObject = null;
        _serverConnections.Remove(replicatedConnection);
        replicatedConnection.Dispose();
    }

    private void UpdateStatistics()
    {
        _statistics = default;
        if (_clientConnection?.IsConnected == true)
        {
            _statistics = _clientConnection.Statistics.Snapshot;
            _connectionCount = 1;
            return;
        }
        _connectionCount = _serverConnections.Count;
        foreach (var connection in _serverConnections)
            _statistics += connection.Statistics.Snapshot;
    }

    private static float RandomRange(Random random, float minimum, float maximum) =>
        minimum + (float)random.NextDouble() * (maximum - minimum);

    private Scene? _scene;
    private Node? _cameraNode;
    private DataChannelServer? _server;
    private ReplicatedDataChannelConnection? _clientConnection;
    private readonly List<ReplicatedDataChannelConnection> _serverConnections = [];
    private IntPtr _addressBuffer;
    private float _yaw;
    private float _pitch = 30.0f;
    private float _statisticsTime;
    private NetworkStatisticsSnapshot _statistics;
    private int _connectionCount;
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
