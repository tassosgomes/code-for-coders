using System.Text.Json;
using System.Text.Json.Nodes;
using CodeForCoders.BffAdmin.Application.Interfaces;

namespace CodeForCoders.BffAdmin.Api.Clients;

internal static class CourseDraftMapper
{
    public static CourseDetail ToPublic(CourseDetail course)
        => course with { Modules = course.Modules.Select(MapModule).ToList() };

    private static JsonElement MapModule(JsonElement module)
    {
        var node = JsonNode.Parse(module.GetRawText())!.AsObject();
        foreach (var lesson in node["lessons"]!.AsArray())
        {
            var item = lesson!.AsObject();
            if (item.ContainsKey("videoId"))
            {
                var videoId = item["videoId"]?.DeepClone();
                item.Remove("videoId");
                item["video"] = videoId is null ? null : new JsonObject { ["videoId"] = videoId };
            }
            if (item["description"] is null) item.Remove("description");
        }
        return JsonSerializer.SerializeToElement(node);
    }
}
