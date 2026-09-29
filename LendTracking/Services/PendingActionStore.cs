using System.Collections.Concurrent;
using LendTracking.Model.Agent;

namespace LendTracking.Services
{
    /// <summary>
    /// Holds agent-proposed write actions until the user confirms or cancels them.
    /// In-memory only: fine for a single Render instance, entries expire after a short window.
    /// </summary>
    public class PendingActionStore
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<string, PendingAction> _actions = new();

        public string Add(int userId, string action, string payloadJson)
        {
            var token = Guid.NewGuid().ToString("N");
            _actions[token] = new PendingAction
            {
                Action = action,
                PayloadJson = payloadJson,
                UserId = userId
            };
            return token;
        }

        public PendingAction? Take(string token, int userId)
        {
            if (!_actions.TryRemove(token, out var pending)) return null;
            if (pending.UserId != userId) return null;
            if (DateTime.UtcNow - pending.CreatedAt > Ttl) return null;

            return pending;
        }

        public bool Discard(string token, int userId)
        {
            if (!_actions.TryGetValue(token, out var pending) || pending.UserId != userId) return false;
            return _actions.TryRemove(token, out _);
        }
    }
}
