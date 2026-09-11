# Testing Commands for Chunking Enhancement

## 1. Run Unit Tests
```bash
dotnet test --filter "ChunkingIntegrationTests"
```

## 2. Test with Large File
1. Place your large order file in the test data directory
2. Update the test configuration in `appsettings.Testing.json`
3. Run the manual test:
```bash
dotnet test --filter "ManualChunkingTest"
```

## 3. Monitor Logs
Look for these log messages:
- "Creating chunks for file {FileName} with size {FileSize}"
- "Created {ChunkCount} chunk jobs for file {FileName}"
- "Processing {JobCount} batch jobs in parallel"
- "Flushed {WriteCounter} results to file"

## 4. Check Results
- Verify that multiple chunk jobs were created
- Check that result files are written frequently
- Confirm that processing completed successfully

## 5. Performance Testing
Test with different configurations:
```json
{
  "BatchProcessing": {
    "MaxChunkSizeBytes": 1048576,    // 1MB
    "MaxConcurrency": 4,             // Adjust based on your system
    "WriteFrequency": 10              // Write every 10 items
  }
}
```

## 6. Troubleshooting
- Check logs for chunk creation messages
- Verify file sizes are being calculated correctly
- Monitor memory usage during processing
- Check that result files are being written incrementally 