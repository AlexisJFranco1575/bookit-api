using Bookit.Application.Agenda;
using Bookit.Domain.Scheduling;

namespace Bookit.Application.Tests;

internal sealed class FakeAgendaReader : IAgendaReader
{
    public List<AgendaRow> Rows { get; } = [];

    /// <summary>The window the service asked for, so tests can check the local-day-to-UTC conversion.</summary>
    public TimeRange? LastWindow { get; private set; }

    public Task<IReadOnlyList<AgendaRow>> ListAsync(
        Guid businessId,
        TimeRange window,
        CancellationToken cancellationToken)
    {
        LastWindow = window;

        IReadOnlyList<AgendaRow> list = Rows
            .Where(row => row.StartsAt >= window.Start && row.StartsAt < window.End)
            .OrderBy(row => row.StartsAt)
            .ToList();

        return Task.FromResult(list);
    }
}