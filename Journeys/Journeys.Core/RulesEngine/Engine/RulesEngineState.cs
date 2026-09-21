using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using Backend.Dto.Structures.Taxonomy;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using System.Collections.Concurrent;

namespace Journeys.Core.RulesEngine.Engine
{
    public class RulesEngineState : DynamicEntity
    {
        public bool CalculateOnly { get; set; }
        public IList<Campaign> Campaigns { get; set; }

        public LoyaltyAccount LoyaltyAccount { get; set; }
        
        private IJourneyStateManager _journeyStateManager;
        
        /// <summary>
        /// Journey state as a read-only view. Computed from JourneyStateManager.
        /// </summary>
        public LoyaltyAccountJourneyState JourneyState 
        { 
            get 
            {
                if (_journeyStateManager == null)
                    return new LoyaltyAccountJourneyState(LoyaltyAccountId, LoyaltyAccount?.Journeys);
                
                var currentState = _journeyStateManager.GetCurrentState();
                return new LoyaltyAccountJourneyState(LoyaltyAccountId, currentState);
            }
        }

        public JourneyNode ProcessingJourney { get; set; }
        
        /// <summary>
        /// Get the journey state manager for state mutations
        /// </summary>
        public IJourneyStateManager JourneyStateManager => _journeyStateManager;

        public IDynamicEntity? Globals
        {
            get { return this["globals"]; }
            set { this["globals"] = value; }
        }

        public IDynamicEntity? LoyaltyAccountNode
        {
            get { return this["loyaltyAccount"]; }
            set { this["loyaltyAccount"] = value; }
        }

        public IDynamicEntity? Event
        {
            get { return this["event"]; }
            set { this["event"] = value; }
        }

        #region Adapters...
        //public ILoyaltyAccountRuleStateAdapter RuleStateAdapter { get; private set; }
        #endregion

        public string EventModelId { get; set; }

        public string LoyaltyAccountId { get; set; }
        public string TenantId { get; set; }

        public string EventId { get; set; }
        public string EventType { get; set; }

        public Dictionary<string, List<TaxonomyDto>> Taxonomies { get; set; }

        public ILoyaltyAccountService LoyaltyAccountService { get; set; }

        public INotificationService? NotificationService { get; set; }

        public bool TreatNotificationSendThrowAsFalse { get; set; }

        public ConcurrentDictionary<string, HistoricalStateBase> LoadedState { get; set; } = new ConcurrentDictionary<string, HistoricalStateBase>();
        public ConcurrentDictionary<string, List<OutcomeResult>> EarnedOutcomes { get; set; } = new ConcurrentDictionary<string, List<OutcomeResult>>();

        //Represents the loaded Journeys that the user started from, this is only used at initial seed to begin engine processing and is not updated
        //throughout movement.
        public Dictionary<string, JourneyNode> LoadedJourneys { get; set; }

        public List<string> AppliedRuleSets = new List<string>();
        public List<string> CampaignsEvaluated = new List<string>();
        public string? CurrentCampaignId { get; set; }
        /// <summary>State key (CampaignId|RuleId) for the historical rule being evaluated; used by providers for LoadedState lookup.</summary>
        public string? CurrentHistoricalStateKey { get; set; }
        /// <summary>State keys modified during evaluation; persisted to TimeToLive container in ProcessRulesAsync finally.</summary>
        public List<string> ModifiedHistoricalStateKeys { get; set; } = new List<string>();
        /// <summary>Existing TimeToLive record for the current event per state key (when reprocessing same event). Used for idempotent delta updates.</summary>
        public Dictionary<string, HistoricalRuleEventTTL> ExistingEventTtlByStateKey { get; set; } = new Dictionary<string, HistoricalRuleEventTTL>();
        public Dictionary<string, string> Errors { get; set; }
        
        /// <summary>
        /// Apply a navigation result to the journey state.
        /// This is the centralized way to mutate journey state.
        /// </summary>
        public void ApplyNavigationResult(Interfaces.Services.NavigationResult result, string rootJourneyId)
        {
            if (result == null || !result.ShouldNavigate || result.TargetNode == null)
                return;

            var rootId = rootJourneyId ?? result.TargetNode.RootNodeId ?? result.TargetNode.Id;
            
            switch (result.NavigationType)
            {
                case Journey.Enums.NavigationType.Entry:
                    _journeyStateManager.AddNode(rootId, result.TargetNode.Id);
                    break;
                    
                case Journey.Enums.NavigationType.Exit:
                    _journeyStateManager.RemoveNode(rootId, result.TargetNode.Id);
                    break;
                    
                case Journey.Enums.NavigationType.Transition:
                    if (result.SourceNode != null)
                    {
                        _journeyStateManager.TransitionNode(rootId, result.SourceNode.Id, result.TargetNode.Id);
                    }
                    else
                    {
                        // Fallback: if source not provided, just add the target
                        _journeyStateManager.AddNode(rootId, result.TargetNode.Id);
                    }
                    break;
            }

            // Update ProcessingJourney if we have campaigns
            if (Campaigns != null)
            {
                var thisCampaign = Campaigns.FirstOrDefault(x => x.Journey?.RootNodeId == rootId);
                if (thisCampaign?.Journey != null)
                {
                    LoadedJourneys ??= new Dictionary<string, JourneyNode>();
                    if (LoadedJourneys.Count == 0)
                    {
                        LoadedJourneys.Add(rootId, thisCampaign.Journey);
                    }
                    ProcessingJourney = thisCampaign.Journey;
                }
            }
        }

        //public RulesEngineState(ILoyaltyAccountRuleStateAdapter ruleStateAdapter, IList<Campaign> campaigns, bool calculateOnly, LoyaltyAccount acct = null)
        //{
        //    CalculateOnly = calculateOnly;
        //    Campaigns = campaigns;
        //    RuleStateAdapter = ruleStateAdapter;
        //    TenantId = LoyaltyAccountId = EventModelId = string.Empty;
        //    LoyaltyAccount = acct ?? new LoyaltyAccount(null, null, null, null, null, null, null, null, null, null);
        //    _journeyStateManager = new Journey.JourneyStateManager(LoyaltyAccount);
        //}

        //public RulesEngineState(ILoyaltyAccountRuleStateAdapter ruleStateAdapter, RulesServiceRequest request, bool calculateOnly, ILoyaltyAccountService loyaltyAccountService, string eventId = null, string eventType = null)
        //{
        //    CalculateOnly = calculateOnly;
        //    RuleStateAdapter = ruleStateAdapter;
        //    Campaigns = request.Campaigns;
        //    EventModelId = request.PayloadModelId;
        //    LoyaltyAccountId = request.LoyaltyAccount.Id;
        //    TenantId = request.LoyaltyAccount.TenantId;
        //    LoyaltyAccount = request.LoyaltyAccount;
        //    ImportDynamicModels(request.Globals, request.LoyaltyAccount, request.Payload);
        //    LoyaltyAccountService = loyaltyAccountService;
        //    _journeyStateManager = new Journey.JourneyStateManager(LoyaltyAccount);

        //    EventId = eventId;
        //    EventType = eventType;
        //}

        public RulesEngineState(IList<Campaign> campaigns, bool calculateOnly, LoyaltyAccount acct = null)
        {
            CalculateOnly = calculateOnly;
            Campaigns = campaigns;
            //RuleStateAdapter = ruleStateAdapter;
            TenantId = LoyaltyAccountId = EventModelId = string.Empty;
            LoyaltyAccount = acct ?? new LoyaltyAccount(null, null, null, null, null, null, null, null, null, null);
            _journeyStateManager = new Journey.JourneyStateManager(LoyaltyAccount);
        }

        public RulesEngineState(RulesServiceRequest request, bool calculateOnly, ILoyaltyAccountService loyaltyAccountService, string eventId = null, string eventType = null)
        {
            CalculateOnly = calculateOnly;
            //RuleStateAdapter = ruleStateAdapter;
            Campaigns = request.Campaigns;
            EventModelId = request.PayloadModelId;
            LoyaltyAccountId = request.LoyaltyAccount.Id;
            TenantId = request.LoyaltyAccount.TenantId;
            LoyaltyAccount = request.LoyaltyAccount;
            ImportDynamicModels(request.Globals, request.LoyaltyAccount, request.Payload);
            LoyaltyAccountService = loyaltyAccountService;
            NotificationService = request.NotificationService;
            TreatNotificationSendThrowAsFalse = request.TreatNotificationSendThrowAsFalse;
            _journeyStateManager = new Journey.JourneyStateManager(LoyaltyAccount);

            EventId = eventId;
            EventType = eventType;
        }

        public RulesEngineState ImportDynamicModels<TGlobal, TAccount>(TGlobal globals, TAccount account, object evt)
        {
            Globals = DynamicHelper.Import(globals);
            LoyaltyAccountNode = DynamicHelper.Import(account);
            Event = DynamicHelper.Import(evt);

            return this;
        }

        public RulesEngineState ImportDynamicEvent(object evt)
        {
            Event = DynamicHelper.Import(evt);

            return this;
        }

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            if (Globals == null)
            {
                errors.Add("Globals is null");
            }

            if (LoyaltyAccountNode == null)
            {
                errors.Add("LoyaltyAccountNode is null");
            }

            if (Event == null)
            {
                errors.Add("Event is null");
            }
            return errors.Count == 0;
        }

        /// <summary>
        /// Creates a clone of this state with Event replaced by the given row (target).
        /// Used when evaluating per-row constraints (e.g. AggregateValueProvider.Constraint) so that
        /// the constraint sees a state where "event" is the single row (e.g. one item from event.items).
        /// Copies Taxonomies and campaign/tenant context so rules like TaxonomicRule can resolve taxonomy lookups.
        /// </summary>
        public RulesEngineState NavigatePayload(IDynamicEntity target)
        {
            var clone = new RulesEngineState(Campaigns, CalculateOnly);
            clone.ImportDynamicModels(Globals, LoyaltyAccountNode, target);
            clone.LoyaltyAccountService = LoyaltyAccountService;
            clone.NotificationService = NotificationService;
            clone.TreatNotificationSendThrowAsFalse = TreatNotificationSendThrowAsFalse;
            clone.Taxonomies = Taxonomies;
            clone.TenantId = TenantId;
            clone.LoyaltyAccountId = LoyaltyAccountId;
            clone.CurrentCampaignId = CurrentCampaignId;
            clone.CurrentHistoricalStateKey = CurrentHistoricalStateKey;
            return clone;
        }

        public void TryAddUpdateEarnedOutcomes(OutcomeResult earnedOutcome, string id = null)
        {
            TryAddUpdateEarnedOutcomes(new List<OutcomeResult> { earnedOutcome }, id);
        }
        public void TryAddUpdateEarnedOutcomes(List<OutcomeResult> earnedOutcomes, string id = null)
        {
            if (!earnedOutcomes?.Any() ?? true) return;

            if (ProcessingJourney == null) throw new Exception("TryAddUpdateEarnedOutcomes: ProcessingJourney is not set for this earned outcome.");

            var key = id ?? this.ProcessingJourney.Id;
            this.EarnedOutcomes ??= new ConcurrentDictionary<string, List<OutcomeResult>>();

            earnedOutcomes.ForEach(x => x.IssuingJourneyNodeId = ProcessingJourney.RootNodeId);
            this.EarnedOutcomes.AddOrUpdate(key, earnedOutcomes, (s, o) => { o.AddRange(earnedOutcomes); return o; });
        }

        public List<TaxonomyDto> GetTaxonomies(string taxonomyId, List<string> keys)
        {
            var returnset = new List<TaxonomyDto>();
            var lstDtos = (Taxonomies?.ContainsKey(taxonomyId) ?? false) ? Taxonomies[taxonomyId] : new List<TaxonomyDto>();
            foreach (var key in keys)
            {
                var keyTx = lstDtos.FirstOrDefault(x => x.DataExternalIds.Contains(key));
                if (keyTx != null)
                {
                    returnset.Add(keyTx);
                }
            }

            return returnset;
        }

        private JourneyNode JourneyNodeByJourneyNodeId(JourneyNode node, string nodeId)
        {
            foreach (var n in node.Children)
            {
                if (n.Id == nodeId) return n;
                var cn = JourneyNodeByJourneyNodeId(n, nodeId);
                if (cn != null) return cn;
            }
            return (node.Id == nodeId) ? node : null;
        }

        private bool IsDescendentJourneyNode(JourneyNode node, string nodeId)
        {
            foreach (var n in node.Children)
            {
                if (n.Id == nodeId) return true;
                var isChild = IsDescendentJourneyNode(n, nodeId);
                if (isChild) return true;
            }
            return node.Id == nodeId;
        }

    }
}
