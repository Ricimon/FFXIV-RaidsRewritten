using System.Collections.Generic;
using RaidsRewritten.Services;

namespace RaidsRewritten.Structures;

public class BoneCategories
{
    public IReadOnlyList<BoneCategory> Categories => _categories;

    private readonly List<BoneCategory> _categories = [];

    public BoneCategories(ResourceProvider resourceProvider)
    {
        var boneCategoryFile = resourceProvider.GetResourceDocument<BoneCategoryFile>("Data.BoneCategories.json");

        foreach(var (id, entry) in boneCategoryFile.Categories)
        {
            //var name = Localize.Get($"bone_categories.{id}", id);
            var name = "Unknown Bone";
            var category = new BoneCategory(id, name, entry.Type, entry.Bones);
            _categories.Add(category);
        }
    }

    public record class BoneCategory(string Id, string Name, BoneCategoryTypes Type, List<string> Bones);

    private class BoneCategoryFile
    {
        public Dictionary<string, BoneCategoryFileEntry> Categories { get; set; } = [];

        public record class BoneCategoryFileEntry(BoneCategoryTypes Type, List<string> Bones);
    }

    public enum BoneCategoryTypes
    {
        Filter
    }
}
