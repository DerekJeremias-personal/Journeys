using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Utility.StreamUtilities
{
    public class CSVUtility : IDisposable
    {
        private readonly int _bufferSize = 4096;
        private readonly Stream _readStream;

        public CSVUtility(Stream readStream, int? bufferSize = null)
        {
            _readStream = readStream;
            if (bufferSize.HasValue)
            {
                _bufferSize = bufferSize.Value;
            }
        }

        public async IAsyncEnumerable<Result<string[]?>> Read(
            int xrefIdx,
            CancellationToken token)
        {
            int bytesRead;
            var buffer = new byte[_bufferSize];
            var leftoverData = new StringBuilder();
            long lineNumber = 0;
            while ((bytesRead = await _readStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                // Combine leftover data with the current buffer
                leftoverData.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                // Process complete lines
                var data = leftoverData.ToString();
                var lines = data.Split('\n', StringSplitOptions.None);

                // Keep the last incomplete line in leftoverData
                leftoverData.Clear();
                if (!data.EndsWith(Environment.NewLine))
                {
                    leftoverData.Append(lines[^1]);
                    lines = lines[..^1];
                }

                foreach (var line in lines)
                {
                    var currentLine = lineNumber;
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    Result<string[]?> result;
                    try
                    {
                        using (var parser = new TextFieldParser(new StringReader(line)))
                        {
                            parser.TextFieldType = FieldType.Delimited;
                            parser.SetDelimiters(",");
                            var fields = parser.ReadFields();
                            result = Result<string[]?>.Success(fields);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Return an error response in case of deserialization failure
                        result = Result<string[]?>.Error($"Error encountered on line {currentLine}: {ex.Message}", line);
                    }
                    yield return result;
                }

                if (token.IsCancellationRequested)
                {
                    yield break;
                }
            }
        }

        #region Dispose...
        public void Dispose()
        {
            _readStream?.Dispose();
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                _readStream?.Dispose();
            }
        }

        ~CSVUtility()
        {
            Dispose(false);
        }
        #endregion
    }
}
