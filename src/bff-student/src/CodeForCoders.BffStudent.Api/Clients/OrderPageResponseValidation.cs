using System.Text.Json;

namespace CodeForCoders.BffStudent.Api.Clients;

public static class OrderPageResponseValidation
{
    public static bool IsValid(JsonElement body)
    {
        if (!body.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array
            || !data.EnumerateArray().All(item => item.ValueKind == JsonValueKind.Object && OrderResponseValidation.IsValid(item, false))
            || !body.TryGetProperty("pagination", out var pagination) || pagination.ValueKind != JsonValueKind.Object)
            return false;
        return Integer(pagination, "page", out var page) && page >= 1
            && Integer(pagination, "size", out var size) && size is >= 1 and <= 50
            && Integer(pagination, "total", out var total) && total >= 0
            && Integer(pagination, "totalPages", out var totalPages) && totalPages == (int)Math.Ceiling((double)total / size)
            && data.GetArrayLength() <= size && data.GetArrayLength() <= total;
    }

    private static bool Integer(JsonElement body, string name, out int number)
    {
        number = 0;
        return body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out number);
    }
}
