namespace Railway.Toolkit.Demo.Api.Domain;

public enum TodoStatus { Open, Completed }

public sealed record TodoTask(
    int Id, string Title, string? Description, DateOnly? DueDate, TodoStatus Status);
