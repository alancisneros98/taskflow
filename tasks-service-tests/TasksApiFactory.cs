using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TasksService.Data;
using TasksService.Services;

namespace TasksService.Tests;

// A "fake" (test double): records calls instead of talking to RabbitMQ
public class FakeEventPublisher : IEventPublisher
{
    public List<(int TaskId, string Title, int UserId)> Published { get; } = new();

    public Task PublishTaskCreated(int taskId, string title, int userId)
    {
        lock (Published) Published.Add((taskId, title, userId));
        return Task.CompletedTask;
    }
}

// Boots the real app in memory, replacing PostgreSQL and RabbitMQ
public class TasksApiFactory : WebApplicationFactory<Program>
{
    public FakeEventPublisher Publisher { get; } = new();
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql registration (both the options and, in newer
            // EF versions, the options configuration) to avoid two providers
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration") &&
                 d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Publisher);
        });
    }
}