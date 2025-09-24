using System;
using System.Collections.Generic;
using System.Numerics;
using MessagePack;
using RaidsRewritten.Structures.Files.Converters;

namespace RaidsRewritten.Structures.Files;

[Serializable]
[MessagePackObject(keyAsPropertyName: true)]
public class PoseFile : JsonDocumentBase
{
    public string TypeName { get; set; } = "Brio Pose";

    public Bone ModelDifference { get; set; } = Transform.Identity;
    public Bone ModelAbsoluteValues { get; set; } = Transform.Identity;

    public Dictionary<string, Bone> Bones { get; set; } = [];
    public Dictionary<string, Bone> MainHand { get; set; } = [];
    public Dictionary<string, Bone> OffHand { get; set; } = [];

    public Vector3 Position { get; set; }  // legacy & for better support for other pose tools
    public Quaternion Rotation { get; set; } // legacy & for better support for other pose tools
    public Vector3 Scale { get; set; } // legacy & for better support for other pose tools

    [MessagePackObject(keyAsPropertyName: true)]
    public class Bone
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }

        public static implicit operator Transform(Bone bone)
        {
            return new Transform()
            {
                Position = bone.Position,
                Rotation = bone.Rotation,
                Scale = bone.Scale
            };
        }

        public static implicit operator Bone(Transform bone)
        {
            return new Bone()
            {
                Position = bone.Position,
                Rotation = bone.Rotation,
                Scale = bone.Scale
            };
        }
    }

    public void SanitizeBoneNames()
    {
        var newBones = new Dictionary<string, Bone>();
        foreach(var bone in Bones)
        {
            newBones[AnamnesisBoneNameConverter.AnamnesisToGame(bone.Key)] = bone.Value;
        }
        Bones = newBones;
    }
}
