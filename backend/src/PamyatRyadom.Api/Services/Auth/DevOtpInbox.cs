using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PamyatRyadom.Api.Services.Auth;

/// <summary>
/// Remembers the last login code sent to each address, so local development does not require
/// digging through container logs to sign in.
///
/// Development only, and structurally so: the store is filled by <see cref="ConsoleEmailSender"/>
/// (registered only when the environment is Development) and read by an endpoint that Program.cs
/// only maps in the same branch. In any other environment the sender is SmtpEmailSender and the
/// route does not exist — there is no configuration value, header or flag that can turn this on,
/// which is the whole point. A code that a running production API can be persuaded to hand back
/// is an account takeover for every user at once.
/// </summary>
public sealed class DevOtpInbox
{
    private static readonly Regex CodePattern = new(@"\b(\d{4,8})\b", RegexOptions.Compiled);

    // Bounded so a long-running dev session cannot grow it without limit.
    private const int MaxEntries = 200;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string destination, string body)
    {
        var match = CodePattern.Match(body);
        if (!match.Success)
        {
            return;
        }

        if (_entries.Count >= MaxEntries)
        {
            // Cheap eviction: drop the oldest rather than tracking an LRU for a dev aid.
            var oldest = _entries.OrderBy(pair => pair.Value.SentAt).FirstOrDefault();
            if (oldest.Key is not null)
            {
                _entries.TryRemove(oldest.Key, out _);
            }
        }

        _entries[destination.Trim()] = new Entry(match.Groups[1].Value, DateTimeOffset.UtcNow);
    }

    public string? Peek(string destination) =>
        _entries.TryGetValue(destination.Trim(), out var entry) ? entry.Code : null;

    private sealed record Entry(string Code, DateTimeOffset SentAt);
}
