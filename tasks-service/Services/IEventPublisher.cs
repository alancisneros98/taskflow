namespace TasksService.Services;

public interface IEventPublisher
{
    Task PublishTaskCreated(int taskId, string title, int userId);
}