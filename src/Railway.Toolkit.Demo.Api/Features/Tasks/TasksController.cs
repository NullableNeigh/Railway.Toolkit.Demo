using Microsoft.AspNetCore.Mvc;
using Railway.Toolkit.Demo.Api.Domain;
using Railway.Toolkit.Demo.Api.Features.Tasks.BatchCreate;
using Railway.Toolkit.Demo.Api.Features.Tasks.Complete;
using Railway.Toolkit.Demo.Api.Features.Tasks.Create;
using Railway.Toolkit.Demo.Api.Features.Tasks.Edit;
using Railway.Toolkit.Demo.Api.Features.Tasks.Lookup;

namespace Railway.Toolkit.Demo.Api.Features.Tasks;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(
    CreateTaskHandler create, LookupTaskHandler lookup, EditTaskHandler edit,
    CompleteTaskHandler complete, BatchCreateTaskHandler batch) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TodoTask>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Create(TaskRequest request) => create.Handle(request).Match<IActionResult>(
        ok => CreatedAtAction(nameof(GetById), new { id = ok.Value.Id }, ok.Value),
        fail => ToFailure(fail.Error));

    [HttpGet("{id:int}")]
    [ProducesResponseType<TodoTask>(StatusCodes.Status200OK)]
    [ProducesResponseType<Error>(StatusCodes.Status404NotFound)]
    public IActionResult GetById(int id) => lookup.Handle(id).Match<IActionResult>(
        ok => Ok(ok.Value), fail => ToFailure(fail.Error));

    [HttpGet]
    [ProducesResponseType<TodoTask[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult GetAll([FromQuery] TodoStatus? status) => Ok(lookup.HandleAll(status));

    [HttpPut("{id:int}")]
    [ProducesResponseType<TodoTask>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Error>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Edit(int id, TaskRequest request) => edit.Handle(id, request).Match<IActionResult>(
        ok => Ok(ok.Value), fail => ToFailure(fail.Error));

    [HttpPost("{id:int}/complete")]
    [ProducesResponseType<TodoTask>(StatusCodes.Status200OK)]
    [ProducesResponseType<Error>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<Error>(StatusCodes.Status409Conflict)]
    public IActionResult Complete(int id) => complete.Handle(id).Match<IActionResult>(
        ok => Ok(ok.Value), fail => ToFailure(fail.Error));

    [HttpPost("batch")]
    [ProducesResponseType<BatchCreateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<Error>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult BatchCreate([FromBody] TaskRequest?[] requests) =>
        batch.Handle(requests).Match<IActionResult>(ok => Ok(ok.Value), fail => ToFailure(fail.Error));

    private IActionResult ToFailure(Error error) => error.Code switch
    {
        "Task.NotFound" => NotFound(error),
        "Task.AlreadyCompleted" => Conflict(error),
        var code when code.StartsWith("Validation.", StringComparison.Ordinal) => UnprocessableEntity(error),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
