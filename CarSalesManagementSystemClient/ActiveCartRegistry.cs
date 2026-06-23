using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CarSalesManagementSystemClient
{
    public static class ActiveCartRegistry
    {
        // Key: SessionId, Value: List of PartIds in that session's cart
        private static readonly ConcurrentDictionary<string, HashSet<int>> _activeCarts = new();

        public static void UpdateCart(string sessionId, IEnumerable<int> partIds)
        {
            _activeCarts[sessionId] = new HashSet<int>(partIds);
        }

        public static void ClearCart(string sessionId)
        {
            _activeCarts.TryRemove(sessionId, out _);
        }

        public static bool IsPartInAnyCart(int partId)
        {
            return _activeCarts.Values.Any(cart => cart.Contains(partId));
        }
    }
}
