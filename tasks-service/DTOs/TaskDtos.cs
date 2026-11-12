namespace TasksService.DTOs;

public record CreateTaskDto(
    string Title,
    string Description
);

public record UpdateTaskDto(
    string? Title,
    string? Description,
    bool? IsCompleted
);