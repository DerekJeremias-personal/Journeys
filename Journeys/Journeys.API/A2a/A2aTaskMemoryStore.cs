using System.Collections.Concurrent;

namespace Journeys.API.A2a;

internal sealed class A2aTaskMemoryStore
{
    private readonly ConcurrentDictionary<string, TaskDto> _tasks = new(StringComparer.Ordinal);

    public void Put(TaskDto task) => _tasks[task.Id] = task;

    public bool TryGet(string id, out TaskDto? task) => _tasks.TryGetValue(id, out task);
}
