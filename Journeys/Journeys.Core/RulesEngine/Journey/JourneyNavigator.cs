using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Journey
{
    /// <summary>
    /// Evaluates navigation criteria and returns navigation results. Does not mutate state.
    /// </summary>
    public class JourneyNavigator : IJourneyNavigator
    {
        private readonly IJourneyStateManager _stateManager;

        public JourneyNavigator(IJourneyStateManager stateManager)
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        }

        public async Task<List<NavigationResult>> EvaluateNavigationAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token)
        {
            var results = new List<NavigationResult>();

            // 1. Evaluate Exit (if currently in this node)
            if (_stateManager.IsInNode(node.RootNodeId ?? node.Id, node.Id))
            {
                var exitResult = await EvaluateExitAsync(node, state, token);
                if (exitResult.ShouldNavigate)
                {
                    results.Add(exitResult);
                }
            }

            // 2. Evaluate Entry (if not in node, or Entry allows multiple)
            if (!_stateManager.IsInNode(node.RootNodeId ?? node.Id, node.Id))
            {
                var entryResult = await EvaluateEntryAsync(node, state, token);
                if (entryResult.ShouldNavigate)
                {
                    results.Add(entryResult);
                }
            }

            // 3. Evaluate Transition for children (all siblings that pass)
            if (node.Children != null && node.Children.Any())
            {
                var transitionResults = await EvaluateTransitionAsync(node, node.Children, state, token);
                results.AddRange(transitionResults);
            }

            return results;
        }

        public async Task<NavigationResult> EvaluateExitAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token)
        {
            var result = new NavigationResult
            {
                ShouldNavigate = false,
                TargetNode = node,
                SourceNode = node,
                NavigationType = NavigationType.Exit
            };

            if (node.NavigationCriteria?.TryGetValue(NavigationType.Exit, out var exitCriteria) ?? false)
            {
                var navResponse = await exitCriteria.ShouldNavigateAsync(state, token);
                if (navResponse.shouldNavigate)
                {
                    result.ShouldNavigate = true;
                    // Calculate navigation outcomes (but don't mutate state here)
                    result.NavigationOutcomes = await CalculateNavigationOutcomesAsync(
                        navResponse.nav, node, state, token);
                }
            }

            return result;
        }

        public async Task<NavigationResult> EvaluateEntryAsync(
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token)
        {
            var result = new NavigationResult
            {
                ShouldNavigate = false,
                TargetNode = node,
                NavigationType = NavigationType.Entry
            };

            if (node.NavigationCriteria?.TryGetValue(NavigationType.Entry, out var entryCriteria) ?? false)
            {
                var navResponse = await entryCriteria.ShouldNavigateAsync(state, token);
                if (navResponse.shouldNavigate)
                {
                    result.ShouldNavigate = true;
                    // Calculate navigation outcomes (but don't mutate state here)
                    result.NavigationOutcomes = await CalculateNavigationOutcomesAsync(
                        navResponse.nav, node, state, token);
                }
            }

            return result;
        }

        public async Task<List<NavigationResult>> EvaluateTransitionAsync(
            JourneyNode parentNode,
            List<JourneyNode> children,
            RulesEngineState state,
            CancellationToken token)
        {
            var results = new List<NavigationResult>();

            if (parentNode.RootNodeId == null)
                throw new InvalidOperationException("RootNodeId must be set before evaluating transitions");

            foreach (var child in children)
            {
                if (child.NavigationCriteria?.TryGetValue(NavigationType.Transition, out var transitionCriteria) ?? false)
                {
                    var navResponse = await transitionCriteria.ShouldNavigateAsync(state, token);
                    if (navResponse.shouldNavigate)
                    {
                        var result = new NavigationResult
                        {
                            ShouldNavigate = true,
                            TargetNode = child,
                            SourceNode = parentNode,
                            NavigationType = NavigationType.Transition
                        };

                        // Calculate navigation outcomes (but don't mutate state here)
                        result.NavigationOutcomes = await CalculateNavigationOutcomesAsync(
                            navResponse.nav, child, state, token);

                        results.Add(result);
                    }
                }

                // Recursively evaluate children of this child for deeper transitions
                if (child.Children != null && child.Children.Any())
                {
                    var childTransitionResults = await EvaluateTransitionAsync(child, child.Children, state, token);
                    var curNodeResult = childTransitionResults?.FirstOrDefault(x => x.SourceNode.Id == parentNode.Id);
                    if (curNodeResult == null && childTransitionResults?.FirstOrDefault(x => x.SourceNode.Id == child.Id) != null)
                    {
                        //Make sure the current node is included for removal, if the dhild node is in the result (source)
                        // This is to handle deep lineage (more than 1 node) tranistions
                        curNodeResult = childTransitionResults.First(x => x.SourceNode.Id == child.Id).Clone();
                        curNodeResult.SourceNode = parentNode;
                        childTransitionResults.Add(curNodeResult);
                    }
                    results.AddRange(childTransitionResults);
                }
            }

            return results;
        }

        public async Task<List<NavigationResult>> EvaluateSiblingTransitionsAsync(
            JourneyNode currentNode,
            JourneyNode parentNode,
            RulesEngineState state,
            CancellationToken token)
        {
            var results = new List<NavigationResult>();

            if (parentNode?.Children == null || !parentNode.Children.Any())
                return results;

            // Get all siblings (exclude current node)
            var siblings = parentNode.Children.Where(c => c.Id != currentNode.Id).ToList();

            foreach (var sibling in siblings)
            {
                if (sibling.NavigationCriteria?.TryGetValue(NavigationType.Transition, out var transitionCriteria) ?? false)
                {
                    var navResponse = await transitionCriteria.ShouldNavigateAsync(state, token);
                    if (navResponse.shouldNavigate)
                    {
                        var result = new NavigationResult
                        {
                            ShouldNavigate = true,
                            TargetNode = sibling,
                            SourceNode = currentNode, // Current node is the source, not parent
                            NavigationType = NavigationType.Transition
                        };

                        // Calculate navigation outcomes (but don't mutate state here)
                        result.NavigationOutcomes = await CalculateNavigationOutcomesAsync(
                            navResponse.nav, sibling, state, token);

                        results.Add(result);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Calculate navigation outcomes without mutating state.
        /// This extracts the outcome calculation logic from NavigateAndAwardNavigationOutcomesAsync
        /// </summary>
        private async Task<List<OutcomeResult>> CalculateNavigationOutcomesAsync(
            INavigationCriteria criteria,
            JourneyNode node,
            RulesEngineState state,
            CancellationToken token)
        {
            var outcomes = new List<OutcomeResult>();

            if (criteria is SimpleNavigationCriteria simpleCriteria && simpleCriteria.Outcomes != null)
            {
                foreach (var outcome in simpleCriteria.Outcomes)
                {
                    var result = await outcome.CalculateOutcomeAsync(state, token);
                    if (result != null)
                    {
                        outcomes.Add(result);
                    }
                }
            }

            return outcomes;
        }
    }
}




