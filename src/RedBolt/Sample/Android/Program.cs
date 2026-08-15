namespace RedBolt.Sample.Android;

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        new("advanced-networking", "Advanced Networking", static () => new global::RedBolt.Sample.AdvancedNetworking.AdvancedNetworking()),
        new("character-demo", "Character Demo", static () => new global::RedBolt.Sample.CharacterDemo.CharacterDemo()),
        new("kinematic-character", "Kinematic Character", static () => new global::RedBolt.Sample.KinematicCharacter.KinematicCharacterDemo()),
        new("physics", "Physics", static () => new global::RedBolt.Sample.Physics.PhysicsSample()),
        new("raycast-vehicle", "Raycast Vehicle", static () => new global::RedBolt.Sample.RaycastVehicle.RaycastVehicleDemo()),
        new("scene-replication", "Scene Replication", static () => new global::RedBolt.Sample.SceneReplication.SceneReplication()),
    ];
}
