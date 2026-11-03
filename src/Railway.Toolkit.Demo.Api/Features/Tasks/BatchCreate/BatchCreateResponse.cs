using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Tasks.BatchCreate;

public sealed record BatchTaskResult(int Index, TodoTask? Task, Error? Error);
public sealed record BatchCreateResponse(
    int Total, int Succeeded, int Failed, IReadOnlyList<BatchTaskResult> Results);
