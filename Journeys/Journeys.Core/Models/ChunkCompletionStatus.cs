namespace Journeys.Core.Models;

public class ChunkCompletionStatus
{
    public int TotalChunks { get; set; }
    public int CompletedChunks { get; set; }
    public int PendingChunks { get; set; }
    public int ProcessingChunks { get; set; }
    public int FailedChunks { get; set; }
    public bool AreAllComplete => TotalChunks > 0 && CompletedChunks == TotalChunks;
}


