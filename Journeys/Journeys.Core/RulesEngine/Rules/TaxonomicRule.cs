using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using Backend.Dto.Structures.Model;
using Backend.Dto.Utilities;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Rules
{
    /// <summary>
    /// Need to path to the Taxonomic XId property(ies) (string)
    /// for each string xid value, look up the taxonomy element(s) via Lookup pattern
    /// Lookup returns the target id, pk1 and pk2 values to read from the Taxonomy container
    /// The taxonomy record(s) will have the hierarchy mapping, including paths to (hierarchy) values
    ///  ...do we need to pulls the hierarchy values from the incoming data? we have the category
    ///  Use category(ies) and id against the rule set (of categories and/or ids)
    /// Each inbound taxonomy item/element will resolve to true or false
    /// In the case of being a predicate in or for an outcome, the matching set is either included or excluded from the outcome
    /// In the case of a rule usage, the matching set (And'ed / Or'ed) return true
    /// 
    /// </summary>
    public class TaxonomicRule : SimpleRule<string>
    {
        public override string Kind => RuleKindDiscriminators.TaxonomicRule;

        public TaxonomicRule() { }
        public TaxonomicRule(string leftPropertyPath, StringEvalType comparison, ConstantValueProvider constant)
            : base(new PathValueProvider(leftPropertyPath), constant, new StringEvaluation(comparison)) { }

        public TaxonomicRule(string leftPropertyPath, StringEvalType comparison, PathValueProvider rightProperty)
            : base(new PathValueProvider(leftPropertyPath), rightProperty, new StringEvaluation(comparison)) { }

        ///Included Tree Nodes ... i.e.categories
        ///
        public List<string> IncludedTreeNodes { get; set; }

        public List<string> IncludedIds { get; set; }


        public List<string> ExcludedTreeNodes { get; set; }

        public List<string> ExcludedIds{ get; set; }

        public string TaxonomyType { get; set; }

        public string TaxonomyId { get; set; }

        public string KeySymbolPath { get; set; }


        public override async Task<bool> Evaluate(RulesEngineState obj, CancellationToken token)
        {
            if (Evaluator == null)
            {
                throw new NullReferenceException("The IEvaluatable property must be set prior to evaluating the rule");
            }

            if (LeftProvider == null)
            {
                throw new NullReferenceException("Left value providers must be set prior to evaluating the rule");
            }

            var lstTaxKeys = await GetTaxonomiesKeys(obj, token);
            if (lstTaxKeys == null) return false;

            //Load/find taxonomy records for the provided event data and known model
            var taxonomies = obj.GetTaxonomies(TaxonomyId, lstTaxKeys);
            if (taxonomies == null) return false;

            var excludedIds = ExcludedIds ?? new List<string>();
            var excludedTreeNodes = ExcludedTreeNodes ?? new List<string>();
            var includedIds = IncludedIds ?? new List<string>();
            var includedTreeNodes = IncludedTreeNodes ?? new List<string>();

            foreach (var txDto in taxonomies)
            {
                if (txDto == null) continue;

                //First, IDs trump TreeNodes (categories), so do them first

                //Excludes trump includes, so do them first
                if (excludedIds.Contains(txDto.Id)) return false;

                if (includedIds.Contains(txDto.Id)) return true;

                //Now check TreeNodes
                foreach (var exc in excludedTreeNodes)
                {
                    if (txDto.Category.Contains(exc)) return false;
                }
                foreach (var inc in includedTreeNodes)
                {
                    if (txDto.Category.Contains(inc)) return true;
                }
            }
            return false;

            //if (((StringEvaluation)Evaluator).Comparison == StringEvalType.InCollection)
            //{
            //    var leftValue = LeftProvider.GetValue<IDynamicEntity>(obj, token);
            //    var rightValue = RightProvider.GetValue<DynamicList>(obj, token);
            //    await Task.WhenAll(leftValue, rightValue);

            //    if (leftValue.Result == null || rightValue.Result == null)
            //    {
            //        throw new ArgumentNullException("Both left and right values must be non-null for Contains comparison");
            //    }

            //    return Evaluator.Evaluate(leftValue.Result, rightValue.Result);
            //}
            //else
            //{
            //    var leftValue = LeftProvider.GetValue<string>(obj, token);
            //    var rightValue = RightProvider.GetValue<string>(obj, token);

            //    await Task.WhenAll(leftValue, rightValue);

            //    return Evaluator.Evaluate(leftValue.Result, rightValue.Result);
            //}
        }

        public async Task<List<string>> GetTaxonomiesKeys(RulesEngineState obj, CancellationToken token)
        {
            var lstTaxKeys = new List<string>();
            bool isNull = (obj.Event is DynamicValue<object> o && o.Value == null);
            if (isNull) return lstTaxKeys;

            var leftValue = await LeftProvider.GetValue<object>(obj, token);
            if (leftValue is string)
            {
                lstTaxKeys.Add(leftValue.ToString());
            }
            else if (leftValue is DynamicEntity)
            {
                var value = DynamicHelper.ReadNodeValue((IDynamicEntity)leftValue, KeySymbolPath);
                if (value == null)
                {
                    throw new Exception($"{KeySymbolPath} not found");
                }
                if (!lstTaxKeys.Contains(value))
                    lstTaxKeys.Add(value.ToString().Trim());
            }
            else if (leftValue is IEnumerable<string> stringEnumerable)
            {
                // obj is any enumerable collection of strings
                foreach (var strTax in stringEnumerable.ToList())
                {
                    if (!lstTaxKeys.Contains(strTax))
                        lstTaxKeys.Add(strTax.ToString().Trim());
                }
            }
            else if (leftValue is IEnumerable<DynamicEntity> lstEntities)
            {
                // obj is any enumerable collection of strings
                foreach (var item in lstEntities)
                {
                    var value = DynamicHelper.ReadNodeValue(item, KeySymbolPath);
                    if (value == null)
                    {
                        throw new Exception($"{KeySymbolPath} not found");
                    }
                    if (!lstTaxKeys.Contains(value))
                        lstTaxKeys.Add(value.ToString().Trim());
                }
            }
            else if (leftValue is DynamicList dynLst)
            {
                // obj is any enumerable collection of strings
                foreach (var item in dynLst)
                {
                    var value = DynamicHelper.ReadNodeValue(item, KeySymbolPath);
                    if (value == null)
                    {
                        throw new Exception($"{KeySymbolPath} not found");
                    }
                    if (!lstTaxKeys.Contains(value))
                        lstTaxKeys.Add(value.ToString().Trim());
                }
            }

            return lstTaxKeys;
        }

        private async Task NormalizeTaxonomyElementsAsync(string tenantId,
            (ModelDto eventModel, ModelDto wrapperModel) modelDetails,
            LoyaltyAccount loyaltyAccountEntity,
            IDynamicEntity data,
            CancellationToken? token = null)
        {
            //Get taxonomy attributes from event model
            var lstTaxes = await TaxonomyHelper.ExtractTaxonomyElements(modelDetails.eventModel);
            if (lstTaxes?.Count == 0) return;

            //Use taxonomy attribute definitions to find elements in data ...by symbol
            var dicTaxes = new Dictionary<string, List<string>>();
            foreach (var tax in lstTaxes)
            {
                //if (tax.TaxonomyEntity == null ||
                //    string.IsNullOrEmpty(tax.TaxonomyEntity.TaxonomyType) ||
                //    string.IsNullOrEmpty(tax.TaxonomyEntity.TaxonomyId) ||
                //    string.IsNullOrEmpty(tax.TaxonomyEntity.ElementSymbol) ||
                //    string.IsNullOrEmpty(tax.TaxonomyEntity.ModelType) ||
                //    string.IsNullOrEmpty(tax.TaxonomyEntity.ModelId)) continue;

                ////var taxdefs = await _taxonomyDataAdapter.GetTaxonomyAsync(tenantId, tax.TaxonomyEntity.TaxonomyType, )

                ////path to tax.TaxonomyEntity.ElementSymbol
                //data.GetTaxonomyKeys("items.sku");


                //if (!dicTaxes.ContainsKey(tax.TaxonomyEntity.TaxonomyId))
                //{

                //}
                //else
                //{
                //    dicTaxes[tax.TaxonomyEntity.TaxonomyId].Add(tax.TaxonomyEntity.TaxonomyId);
                //}
            }

            //var taxres = await _taxonomyDataAdapter.GetManyTaxonomiesByXidAsync(tenantId, modelDetails.eventModel.ID, )


            //With collection of symbols, call taxonomy adapter for corresponding taxonomy elements

            // Stash retrieved taxonomy elements in campaign model

            //Put records into state for use later
        }

    }
}
