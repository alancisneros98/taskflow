using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TasksService.Data;
using TasksService.DTOs;
using TasksService.Models;
using TasksService.Services;

namespace TasksService.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TasksController> _logger;
    private readonly RabbitMQPublisher _publisher;

    public TasksController(AppDbContext db, ILogger<TasksController> logger, RabbitMQPublisher publisher)
    {
        _db = db;
        _logger = logger;
        _publisher = publisher;
    }

    // El gateway valida el JWT y coloca el id del usuario en este header.
    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        return Request.Headers.TryGetValue("x-user-id", out var value)
            && int.TryParse(value.FirstOrDefault(), out userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var tasks = await _db.Tasks
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return NotFound(new { error = "Tarea no encontrada" });
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "El título es requerido" });

        var task = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            UserId = userId
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        await _publisher.PublishTaskCreated(task.Id, task.Title, task.UserId);

        _logger.LogInformation("Tarea creada: {TaskId} por usuario {UserId}", task.Id, task.UserId);

        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return NotFound(new { error = "Tarea no encontrada" });

        if (dto.Title is not null) task.Title = dto.Title;
        if (dto.Description is not null) task.Description = dto.Description;
        if (dto.IsCompleted.HasValue)
        {
            task.IsCompleted = dto.IsCompleted.Value;
            task.CompletedAt = dto.IsCompleted.Value ? DateTime.UtcNow : null;
        }

        await _db.SaveChangesAsync();
        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null) return NotFound(new { error = "Tarea no encontrada" });

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}