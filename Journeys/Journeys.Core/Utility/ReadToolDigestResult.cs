namespace Journeys.Core.Utility;

public sealed record ReadToolDigestResult(string Json, bool Transformed, int OriginalChars, int DigestChars);
