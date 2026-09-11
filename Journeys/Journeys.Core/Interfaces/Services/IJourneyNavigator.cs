using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;

namespace Journeys.Core.Interfaces.Services
{
    /// <summary>
    /// Result of navigation evaluation
    /// </summary>
    public class NavigationResult
    {
        public bool ShouldNavigate { get; set; }
        public JourneyNode? TargetNode { get; set; }
        public JourneyNode? SourceNode { get; set; } // For transitions/exits
        public List<OutcomeResult> NavigationOutcomes { get; set; } = new List<OutcomeResult>();
        public NavigationType NavigationType { get; set; }

        public NavigationResult Clone()
        {
            return new NavigationResult
            {
                ShouldNavigate = ShouldNavigate,
                TargetNode = TargetNode,
                SourceNode = SourceNode,
                NavigationType = NavigationType,
                NavigationOutcomes = NavigationOutcomes
            };
        }
    }

    /// <summary>
    /// Evaluates navigation criteria and returns navigation results. Does not mutate state.
    /// </summary>
    public interface IJourneyNavigator
    {
        /// <summary>
        /// Evaluate all navigation possibilities for a node (Exit, Entry, Transition for children)
        /// </summary>
        Task<List<NavigationResult>> EvaluateNavigationAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token);

        /// <summary>
        /// Evaluate Exit navigation for a node
        /// </summary>
        Task<NavigationResult> EvaluateExitAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token);

        /// <summary>
        /// Evaluate Entry navigation for a node
        /// </summary>
        Task<NavigationResult> EvaluateEntryAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token);

        /// <summary>
        /// Evaluate Transition navigation for all children of a parent node
        /// Returns all children that have passing transition criteria
        /// </summary>
        Task<List<NavigationResult>> EvaluateTransitionAsync(
            JourneyNode parentNode,
            List<JourneyNode> children,
            RulesEngineState state,
            CancellationToken token);

        /// <summary>
        /// Evaluate Transition navigation from current node to its siblings
        /// Returns all siblings that have passing transition criteria
        /// </summary>
        Task<List<NavigationResult>> EvaluateSiblingTransitionsAsync(
            JourneyNode currentNode,
            JourneyNode parentNode,
            RulesEngineState state,
            CancellationToken token);
    }
}

