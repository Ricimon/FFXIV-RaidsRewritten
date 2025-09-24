using System;
using MessagePack;

namespace RaidsRewritten.Structures.Files;

[Serializable]
[MessagePackObject(keyAsPropertyName: true)]
//[Union(0, typeof(SceneFile))]
//[Union(1, typeof(AnamnesisCharaFile))]
[Union(2, typeof(PoseFile))]
public abstract class JsonDocumentBase : IFileMetadata
{
    [Key(0)] public string? Author { get; set; }
    [Key(1)] public string? Description { get; set; }
    [Key(2)] public string? Version { get; set; }
    [Key(3)] public string? Base64Image { get; set; }
    [Key(4)] public TagCollection? Tags { get; set; }

    public virtual void GetAutoTags(ref TagCollection tags)
    {
        if(this.Author != null)
        {
            tags.Add(this.Author);
        }
    }
}
