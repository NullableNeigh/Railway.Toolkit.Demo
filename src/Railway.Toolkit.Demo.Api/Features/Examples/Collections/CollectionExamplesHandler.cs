using System.Globalization;

namespace Railway.Toolkit.Demo.Api.Features.Examples.Collections;

public sealed class CollectionExamplesHandler
{
    public Result<IReadOnlyList<int>> Traverse(IReadOnlyList<string?> items) =>
        items.Select((value, index) => (value, index)).Traverse(Parse);

    public Result<IReadOnlyList<int>> TraverseAll(IReadOnlyList<string?> items) =>
        items.Select((value, index) => (value, index)).TraverseAll(Parse);

    private static Result<int> Parse((string? Value, int Index) item)
    {
        if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            return Result.Ok(number);
        }

        return Result.Fail<int>(new Error
        {
            Code = "Example.InvalidInteger",
            Message = $"Item {item.Index} must be a 32-bit integer",
            Details = new Dictionary<string, string[]>
            {
                [$"Items[{item.Index}]"] = ["must be a 32-bit integer"]
            }
        });
    }
}
