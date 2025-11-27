// Source - https://stackoverflow.com/a/20639697
// Posted by Brian Rogers, modified by community. See post 'Timeline' for change history
// Retrieved 2025-11-27, License - CC BY-SA 3.0

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

class LongNameContractResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        // Let the base class create all the JsonProperties 
        // using the short names
        IList<JsonProperty> list = base.CreateProperties(type, memberSerialization);

        // Now inspect each property and replace the 
        // short name with the real property name
        foreach (JsonProperty prop in list)
        {
            prop.PropertyName = prop.UnderlyingName;
        }

        return list;
    }
}
