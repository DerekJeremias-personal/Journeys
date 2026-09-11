using System.IO;

namespace Journeys.Core.Utilities;

/// <summary>
/// Centralized utility for generating consistent chunk file names across the application.
/// This ensures all components use the same naming convention for chunk files and result files.
/// </summary>
public static class ChunkNamingUtility
{
    /// <summary>
    /// Builds the chunk file name for a given batch file and chunk index.
    /// Format: {batchFileName}_chunk_{chunkIndex:D4}{extension}
    /// </summary>
    /// <param name="batchFileName">The original batch file name</param>
    /// <param name="chunkIndex">The zero-based chunk index</param>
    /// <returns>The chunk file name</returns>
    public static string BuildChunkFileName(string batchFileName, int chunkIndex)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(batchFileName);
        var extension = Path.GetExtension(batchFileName);
        return $"{fileNameWithoutExtension}_chunk_{chunkIndex:D4}{extension}";
    }

    /// <summary>
    /// Builds the chunk result file name for a given batch file and chunk index.
    /// Format: {batchFileName}_chunk_{chunkIndex:D4}_results.ndjson
    /// </summary>
    /// <param name="batchFileName">The original batch file name</param>
    /// <param name="chunkIndex">The zero-based chunk index</param>
    /// <returns>The chunk result file name</returns>
    public static string BuildChunkResultFileName(string batchFileName, int chunkIndex, bool oldStyle = false)
    {
        //var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(batchFileName);
        return (oldStyle) ? $"{batchFileName}_chunk{chunkIndex:D3}_results.ndjson" : $"{batchFileName}_chunk_{chunkIndex:D4}_results.ndjson";
    }

    /// <summary>
    /// Builds the result file name for a regular (non-chunk) batch job.
    /// Format: {batchFileName}_results.ndjson
    /// </summary>
    /// <param name="batchFileName">The original batch file name</param>
    /// <returns>The result file name</returns>
    public static string BuildResultFileName(string batchFileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(batchFileName);
        return $"{fileNameWithoutExtension}_results.ndjson";
    }

    /// <summary>
    /// Determines if a file name represents a chunk file.
    /// </summary>
    /// <param name="fileName">The file name to check</param>
    /// <returns>True if the file is a chunk file</returns>
    public static bool IsChunkFile(string fileName)
    {
        return fileName.Contains("_chunk_") && fileName.Contains("_results.ndjson");
    }

    /// <summary>
    /// Extracts the chunk index from a chunk file name.
    /// </summary>
    /// <param name="chunkFileName">The chunk file name</param>
    /// <returns>The chunk index, or -1 if not found</returns>
    public static int ExtractChunkIndex(string chunkFileName)
    {
        var parts = chunkFileName.Split('_');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "chunk" && i + 1 < parts.Length)
            {
                if (int.TryParse(parts[i + 1], out int chunkIndex))
                {
                    return chunkIndex;
                }
            }
        }
        return -1;
    }
}
