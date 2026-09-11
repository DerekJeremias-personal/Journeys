using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Core.Utility.StreamUtilities
{
    public class NDJsonUtility : IDisposable
    {
        private readonly int _bufferSize = 4096;
        private readonly Stream _readStream;

        public NDJsonUtility(Stream readStream, int? bufferSize = null)
        {
            _readStream = readStream;
            if (bufferSize.HasValue)
            {
                _bufferSize = bufferSize.Value;
            }
        }

        public async IAsyncEnumerable<KeyValuePair<string, Result<TElement>>> Read<TElement>(
            Func<TElement, string> xrefSelector,
            CancellationToken token)
            where TElement : class
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
                var lines = data.Split(Environment.NewLine, StringSplitOptions.None);

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

                    KeyValuePair<string, Result<TElement>> response;
                    try
                    {
                        var tmp = line.Replace("\"\"", "\"").Trim('\"');
                        if (tmp.Contains("\"\""))
                        {
                            tmp = tmp.Replace("\"\"", "\"").Trim('\"');
                        }
                        var element = JsonUtility.Deserialize<TElement>(tmp, null);
                        if (element == null)
                        {
                            continue;
                        }
                        var xref = xrefSelector(element);
                        response = new KeyValuePair<string, Result<TElement>>(xref, Result<TElement>.Success(element));
                    }
                    catch (Exception ex)
                    {
                        // Return an error response in case of deserialization failure
                        response = new KeyValuePair<string, Result<TElement>>(
                            $"Error Line #: {currentLine}",
                            Result<TElement>.Error($"Error encountered on line {currentLine}: {ex.Message}", line));
                    }
                    yield return response;
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

        ~NDJsonUtility()
        {
            Dispose(false);
        }
        #endregion
    }
}
