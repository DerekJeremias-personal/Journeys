using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Utility;
using Journeys.DTO.Models.RulesEngine;
using Microsoft.Azure.Amqp.Encoding;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.RulesEngine.Journey
{
    public class JourneyNode : JourneyBase
    {
        private readonly JsonProperty<Dictionary<NavigationType, INavigationCriteria>> _navigation 
            = new JsonProperty<Dictionary<NavigationType, INavigationCriteria>>();


        [JsonConstructor]
        public JourneyNode(
            string name,
            List<RuleSet>? rules = null,
            string? id = null,
            string? rootNodeId = null,
            JsonElement? navigation = null,
            List<JourneyNode>? children = null) : base(id)
        {
            Id = id ?? Guid.NewGuid().ToString();
            Name = name;
            RootNodeId = rootNodeId;
            Children = children;
            Rules = rules;
            _navigation.Serialized = navigation;
        }

        public string Name { get; set; }

        public List<RuleSet>? Rules { get; set; }

        public string? RootNodeId { get; set; }

        public JsonElement? Navigation
        {
            get => _navigation.Serialized;
            set => _navigation.Serialized = value;
        }

        [JsonIgnore]
        public Dictionary<NavigationType, INavigationCriteria>? NavigationCriteria
        {
            get => _navigation.Deserialized;
            set => _navigation.Deserialized = value;
        }

        public List<JourneyNode>? Children { get; set; } = new List<JourneyNode>();

        /// <summary>
        /// Sets <see cref="RootNodeId"/> only when null or whitespace: the tree root defaults its own value to
        /// <see cref="ModelBase.Id"/>; descendants copy the root's canonical id. Existing non-empty
        /// <see cref="RootNodeId"/> values are left unchanged.
        /// </summary>
        public void FillMissingRootNodeIds()
        {
            if (string.IsNullOrWhiteSpace(RootNodeId))
                RootNodeId = Id;
            var canonicalRootNodeId = RootNodeId;
            if (string.IsNullOrWhiteSpace(canonicalRootNodeId) || Children == null)
                return;
            foreach (var child in Children)
                child.FillMissingRootNodeIdsOnSubtree(canonicalRootNodeId);
        }

        private void FillMissingRootNodeIdsOnSubtree(string canonicalRootNodeId)
        {
            if (string.IsNullOrWhiteSpace(RootNodeId))
                RootNodeId = canonicalRootNodeId;
            if (Children == null)
                return;
            foreach (var child in Children)
                child.FillMissingRootNodeIdsOnSubtree(canonicalRootNodeId);
        }

        public async Task<RulesEngineState> ProcessAsync(RulesEngineState state, CancellationToken token)
        {
            // Get navigator and state manager from state
            var navigator = new JourneyNavigator(state.JourneyStateManager);
            var rootJourneyId = RootNodeId ?? Id;

            // 1. Evaluate all navigation possibilities for this node
            var navigationResults = await navigator.EvaluateNavigationAsync(this, state, token);

            // 1a. If we're currently in this node, also evaluate transitions to siblings
            if (state.JourneyStateManager.IsInNode(rootJourneyId, this.Id))
            {
                var parentNode = FindParentNode(state, this);
                if (parentNode != null)
                {
                    var siblingTransitions = await navigator.EvaluateSiblingTransitionsAsync(this, parentNode, state, token);
                    navigationResults.AddRange(siblingTransitions);
                }
            }

            // 2. Apply navigation results and award navigation outcomes
            foreach (var result in navigationResults)
            {
                if (result.NavigationOutcomes?.Any() ?? false)
                {
                    state.TryAddUpdateEarnedOutcomes(
                        result.NavigationOutcomes,
                        result.NavigationType.ToString());
                }

                // Apply state changes
                state.ApplyNavigationResult(result, rootJourneyId);
            }

            // 3. Check if we exited this node
            var exited = navigationResults.Any(r => 
                r.NavigationType == NavigationType.Exit && 
                r.TargetNode?.Id == this.Id);
            
            if (exited)
            {
                // If we exited, don't process rules or children
                return state;
            }

            // 4. Check if we transitioned away from this node
            var transitionedAway = navigationResults.Any(r => 
                r.NavigationType == NavigationType.Transition && 
                r.SourceNode?.Id == this.Id);

            if (transitionedAway)
            {
                // Process the child node(s) we transitioned to
                var transitionResults = navigationResults
                    .Where(r => r.NavigationType == NavigationType.Transition && r.SourceNode?.Id == this.Id)
                    .ToList();

                foreach (var transitionResult in transitionResults)
                {
                    if (transitionResult.TargetNode != null)
                    {
                        // Recursively process the child node we transitioned to
                        await transitionResult.TargetNode.ProcessAsync(state, token);
                    }
                }

                // Don't process rules for this node since we transitioned away
                return state;
            }

            var journeyToProcess = this;

            {
                //Make sure they are in their matching journey node
                var nodes = state.JourneyStateManager.GetNodesForRoot(this.RootNodeId);
                if (nodes?.Count > 0)
                {
                    var rightnode = this.NavigateTo(nodes.First());
                    if (rightnode != null && rightnode.Rules?.Count > 0)
                    {
                        journeyToProcess = rightnode;
                    }
                }
            }

            // 5. If we're still in this node (or entered it), process rules
            if (journeyToProcess.Rules != null && state.JourneyStateManager.IsInNode(rootJourneyId, journeyToProcess.Id))
            {
                foreach (var ruleSet in journeyToProcess.Rules)
                {
                    if (ruleSet != null && ruleSet.RuleTree != null && await ruleSet.RuleTree.Evaluate(state, token))
                    {
                        state.AppliedRuleSets.Add(ruleSet.Id);
                        if (ruleSet.Outcomes != null)
                        {
                            foreach (var o in ruleSet.Outcomes)
                            {
                                var outcome = await o.CalculateOutcomeAsync(state, token);
                                if (outcome != null)
                                {
                                    outcome.CampaignId = state.CurrentCampaignId;
                                    outcome.RuleSetId = ruleSet.Id;
                                    outcome.IsAwarded = false;
                                    state.TryAddUpdateEarnedOutcomes(outcome, Id);
                                }
                            }
                        }
                    }
                }
            }

            // 6. Process Entry navigation for children (additive, doesn't remove current)
            if (Children != null && Children.Any())
            {
                foreach (var child in Children)
                {
                    var entryResult = await navigator.EvaluateEntryAsync(child, state, token);
                    if (entryResult.ShouldNavigate)
                    {
                        // Award navigation outcomes
                        if (entryResult.NavigationOutcomes?.Any() ?? false)
                        {
                            state.TryAddUpdateEarnedOutcomes(
                                entryResult.NavigationOutcomes,
                                NavigationType.Entry.ToString());
                        }

                        // Apply Entry (additive)
                        state.ApplyNavigationResult(entryResult, rootJourneyId);

                        // Process the child node
                        await child.ProcessAsync(state, token);
                    }
                }
            }

            return state;
        }

        public List<T> FlattenToRulesOfType<T>() where T : RuleBase
        {
            var result = new List<T>();

            if (Rules != null)
            {
                foreach (var ruleSet in Rules)
                {
                    if (ruleSet == null)
                        continue;
                    var fromEarn = ruleSet.FlattenToRulesOfType<T>();
                    if (fromEarn != null && fromEarn.Count > 0)
                        result.AddRange(fromEarn);
                }
            }

            if (NavigationCriteria != null)
            {
                foreach (var criteria in NavigationCriteria.Values)
                {
                    if (criteria is not SimpleNavigationCriteria simple || simple.NavConstraint == null)
                        continue;
                    var fromNav = FlattenConstraintTreeToType<T>(simple.NavConstraint);
                    if (fromNav.Count > 0)
                        result.AddRange(fromNav);
                }
            }

            if (Children != null)
            {
                foreach (var child in Children)
                {
                    if (child == null)
                        continue;
                    var fromChild = child.FlattenToRulesOfType<T>();
                    if (fromChild != null && fromChild.Count > 0)
                        result.AddRange(fromChild);
                }
            }

            return result;
        }

        private static List<T> FlattenConstraintTreeToType<T>(RuleBase constraint) where T : RuleBase
        {
            var result = new List<T>();
            if (constraint is T match)
                result.Add(match);
            if (constraint is CompositeRuleBase composite)
                result.AddRange(composite.FlattenChildrenToRulesOfType<T>());
            return result;
        }

        public JourneyNode? NavigateTo(string targetId)
        {
            if (Id == targetId)
                return this;

            if (Children != null)
                foreach (var c in Children)
                {
                    var result = c.NavigateTo(targetId);
                    if (result != null)
                        return result;
                }

            return null;
        }

        /// <summary>
        /// Find the parent node of this node in the journey tree from the campaign
        /// </summary>
        private JourneyNode? FindParentNode(RulesEngineState state, JourneyNode currentNode)
        {
            if (state.Campaigns == null)
                return null;

            // Find the root journey for this node
            var rootJourneyId = currentNode.RootNodeId ?? currentNode.Id;
            var campaign = state.Campaigns.FirstOrDefault(c => c.Journey?.RootNodeId == rootJourneyId);
            if (campaign?.Journey == null)
                return null;

            // Search for parent by checking if any node has this node as a child
            return FindParentRecursive(campaign.Journey, currentNode);
        }

        private JourneyNode? FindParentRecursive(JourneyNode node, JourneyNode targetNode)
        {
            // Check if this node has the target as a child
            if (node.Children != null && node.Children.Any(c => c.Id == targetNode.Id))
                return node;

            // Recursively search children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var parent = FindParentRecursive(child, targetNode);
                    if (parent != null)
                        return parent;
                }
            }

            return null;
        }
    }

}
