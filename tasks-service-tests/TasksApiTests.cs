using System.Net;
using System.Net.Http.Json;
using TasksService.Models;

namespace TasksService.Tests;

public class TasksApiTests : IClassFixture<TasksApiFactory>
{
    private readonly TasksApiFactory _factory;
    private static int _nextUserId = 1000;

    public TasksApiTests(TasksApiFactory factory) => _factory = factory;

    // Each test uses fresh user ids so tests never interfere with each other
    private static int NewUserId() => Interlocked.Increment(ref _nextUserId);

    // Simulates what the gateway does: inject the validated user id as a header
    private HttpClient ClientFor(int? userId)
    {
        var client = _factory.CreateClient();
        if (userId.HasValue)
            client.DefaultRequestHeaders.Add("x-user-id", userId.Value.ToString());
        return client;
    }

    private static async Task<TaskItem> CreateTask(HttpClient client, string title = "Test task")
    {
        var res = await client.PostAsJsonAsync("/tasks", new { title, description = "desc" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<TaskItem>())!;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var res = await ClientFor(null).GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Request_WithoutUserHeader_IsUnauthorized()
    {
        var res = await ClientFor(null).GetAsync("/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Create_AssignsTaskToAuthenticatedUser_IgnoringBodyUserId()
    {
        var userA = NewUserId();
        var res = await ClientFor(userA).PostAsJsonAsync("/tasks",
            new { title = "Mine", description = "x", userId = 999999 });

        var task = await res.Content.ReadFromJsonAsync<TaskItem>();
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(userA, task!.UserId);
    }

    [Fact]
    public async Task Create_WithEmptyTitle_ReturnsBadRequest()
    {
        var res = await ClientFor(NewUserId()).PostAsJsonAsync("/tasks",
            new { title = "  ", description = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Create_PublishesTaskCreatedEvent()
    {
        var userA = NewUserId();
        var task = await CreateTask(ClientFor(userA), "Event task");

        Assert.Contains(_factory.Publisher.Published,
            e => e.TaskId == task.Id && e.UserId == userA && e.Title == "Event task");
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnTasks()
    {
        var userA = NewUserId();
        var userB = NewUserId();
        await CreateTask(ClientFor(userA), "A's task");

        var res = await ClientFor(userB).GetAsync("/tasks");
        var tasks = await res.Content.ReadFromJsonAsync<List<TaskItem>>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Empty(tasks!);
    }

    [Fact]
    public async Task GetById_OfAnotherUsersTask_ReturnsNotFound()
    {
        var task = await CreateTask(ClientFor(NewUserId()));

        var res = await ClientFor(NewUserId()).GetAsync($"/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Update_OfAnotherUsersTask_ReturnsNotFound_AndDoesNotChangeIt()
    {
        var userA = NewUserId();
        var task = await CreateTask(ClientFor(userA), "Original");

        var res = await ClientFor(NewUserId()).PutAsJsonAsync($"/tasks/{task.Id}",
            new { title = "Hacked" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var after = await ClientFor(userA).GetFromJsonAsync<TaskItem>($"/tasks/{task.Id}");
        Assert.Equal("Original", after!.Title);
    }

    [Fact]
    public async Task Delete_OfAnotherUsersTask_ReturnsNotFound_AndTaskSurvives()
    {
        var userA = NewUserId();
        var task = await CreateTask(ClientFor(userA));

        var res = await ClientFor(NewUserId()).DeleteAsync($"/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var still = await ClientFor(userA).GetAsync($"/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);
    }

    [Fact]
    public async Task Owner_CanCompleteAndDeleteOwnTask()
    {
        var client = ClientFor(NewUserId());
        var task = await CreateTask(client);

        var put = await client.PutAsJsonAsync($"/tasks/{task.Id}", new { isCompleted = true });
        var updated = await put.Content.ReadFromJsonAsync<TaskItem>();
        Assert.True(updated!.IsCompleted);
        Assert.NotNull(updated.CompletedAt);

        var del = await client.DeleteAsync($"/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }
}