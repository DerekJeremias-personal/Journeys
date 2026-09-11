using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services
{
    /// <summary>
    /// Manages journey state for loyalty accounts. Single source of truth for all journey state mutations.
    /// </summary>
    public interface IJourneyStateManager
    {
        /// <summary>
        /// Initialize state from a LoyaltyAccount
        /// </summary>
        void InitializeFromAccount(LoyaltyAccount account);

        /// <summary>
        /// Add a node to the journey state (for Entry navigation)
        /// </summary>
        void AddNode(string rootJourneyId, string nodeId);

        /// <summary>
        /// Remove a node from the journey state (for Exit navigation)
        /// </summary>
        void RemoveNode(string rootJourneyId, string nodeId);

        /// <summary>
        /// Transition from one node to another (removes fromNode, adds toNode)
        /// </summary>
        void TransitionNode(string rootJourneyId, string fromNodeId, string toNodeId);

        /// <summary>
        /// Check if account is in a specific node
        /// </summary>
        bool IsInNode(string rootJourneyId, string nodeId);

        /// <summary>
        /// Get all node IDs for a root journey
        /// </summary>
        List<string> GetNodesForRoot(string rootJourneyId);

        /// <summary>
        /// Get current state as LoyaltyAccountJourney list (for persistence)
        /// </summary>
        List<LoyaltyAccountJourney> GetCurrentState();

        /// <summary>
        /// Get the LoyaltyAccount being managed
        /// </summary>
        LoyaltyAccount GetAccount();
    }
}




