using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Journeys.Core.RulesEngine.Journey
{
    /// <summary>
    /// Manages journey state for loyalty accounts. Single source of truth for all journey state mutations.
    /// </summary>
    public class JourneyStateManager : IJourneyStateManager
    {
        private LoyaltyAccount _account;

        public JourneyStateManager(LoyaltyAccount account)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            InitializeFromAccount(account);
        }

        public void InitializeFromAccount(LoyaltyAccount account)
        {
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _account.Journeys ??= new List<LoyaltyAccountJourney>();
        }

        public void AddNode(string rootJourneyId, string nodeId)
        {
            if (string.IsNullOrEmpty(rootJourneyId))
                throw new ArgumentException("RootJourneyId cannot be null or empty", nameof(rootJourneyId));
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("NodeId cannot be null or empty", nameof(nodeId));

            var journey = GetOrCreateJourney(rootJourneyId);
            if (!journey.JourneyNodeIds.Contains(nodeId))
            {
                journey.JourneyNodeIds.Add(nodeId);
            }
        }

        public void RemoveNode(string rootJourneyId, string nodeId)
        {
            if (string.IsNullOrEmpty(rootJourneyId))
                throw new ArgumentException("RootJourneyId cannot be null or empty", nameof(rootJourneyId));
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("NodeId cannot be null or empty", nameof(nodeId));

            var journey = _account.Journeys?.FirstOrDefault(j => 
                j.RootJourneyNodeId.Equals(rootJourneyId, StringComparison.InvariantCultureIgnoreCase));

            if (journey != null)
            {
                journey.JourneyNodeIds?.Remove(nodeId);
                
                // Remove journey container if no nodes remain
                if (!journey.JourneyNodeIds.Any())
                {
                    _account.Journeys.Remove(journey);
                }
            }
        }

        public void TransitionNode(string rootJourneyId, string fromNodeId, string toNodeId)
        {
            if (string.IsNullOrEmpty(rootJourneyId))
                throw new ArgumentException("RootJourneyId cannot be null or empty", nameof(rootJourneyId));
            if (string.IsNullOrEmpty(fromNodeId))
                throw new ArgumentException("FromNodeId cannot be null or empty", nameof(fromNodeId));
            if (string.IsNullOrEmpty(toNodeId))
                throw new ArgumentException("ToNodeId cannot be null or empty", nameof(toNodeId));

            // Remove the source node
            RemoveNode(rootJourneyId, fromNodeId);

            // Add the target node
            AddNode(rootJourneyId, toNodeId);
        }

        public bool IsInNode(string rootJourneyId, string nodeId)
        {
            if (string.IsNullOrEmpty(rootJourneyId) || string.IsNullOrEmpty(nodeId))
                return false;

            var journey = _account.Journeys?.FirstOrDefault(j => 
                j.RootJourneyNodeId.Equals(rootJourneyId, StringComparison.InvariantCultureIgnoreCase));

            return journey?.JourneyNodeIds?.Contains(nodeId) ?? false;
        }

        public List<string> GetNodesForRoot(string rootJourneyId)
        {
            if (string.IsNullOrEmpty(rootJourneyId))
                return new List<string>();

            var journey = _account.Journeys?.FirstOrDefault(j => 
                j.RootJourneyNodeId.Equals(rootJourneyId, StringComparison.InvariantCultureIgnoreCase));

            return journey?.JourneyNodeIds?.ToList() ?? new List<string>();
        }

        public List<LoyaltyAccountJourney> GetCurrentState()
        {
            return _account.Journeys?.ToList() ?? new List<LoyaltyAccountJourney>();
        }

        public LoyaltyAccount GetAccount()
        {
            return _account;
        }

        private LoyaltyAccountJourney GetOrCreateJourney(string rootJourneyId)
        {
            var journey = _account.Journeys?.FirstOrDefault(j => 
                j.RootJourneyNodeId.Equals(rootJourneyId, StringComparison.InvariantCultureIgnoreCase));

            if (journey == null)
            {
                journey = new LoyaltyAccountJourney(rootJourneyId, new List<string>());
                _account.Journeys.Add(journey);
            }

            journey.JourneyNodeIds ??= new List<string>();
            return journey;
        }
    }
}




