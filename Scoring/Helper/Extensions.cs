﻿using Microsoft.Extensions.Logging;
using Citolab.QTI.ScoringEngine.ResponseProcessing;
using Citolab.QTI.ScoringEngine.Const;
using Citolab.QTI.ScoringEngine.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Char;
using Citolab.QTI.ScoringEngine.Interfaces;
using Citolab.QTI.ScoringEngine.OutcomeProcessing;

namespace Citolab.QTI.ScoringEngine.Helper
{

    public static class Extensions
    {
        public static IEnumerable<XElement> FindElementsByName(this XDocument doc, string name)
        {
            return doc.Descendants().Where(d => d.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static IEnumerable<XElement> FindElementsByLastPartOfName(this XDocument doc, string name)
        {
            return doc.Descendants().Where(d => d.Name.LocalName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        }
        public static IEnumerable<XElement> FindElementsByLastPartOfName(this XElement el, string name)
        {
            return el.Descendants().Where(d => d.Name.LocalName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        }
        public static IEnumerable<XElement> FindElementsByName(this XElement el, string name)
        {
            return el.Descendants().Where(d => d.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static XElement FindElementByName(this XDocument doc, string name)
        {
            return doc.FindElementsByName(name).FirstOrDefault();
        }

        public static OutcomeVariable CreateVariable(this OutcomeDeclaration outcomeDeclaration)
        {
            return new OutcomeVariable
            {
                Identifier = outcomeDeclaration.Identifier,
                BaseType = outcomeDeclaration.BaseType,
                Cardinality = outcomeDeclaration.Cardinality,
                Value = outcomeDeclaration.DefaultValue
            };
        }

        public static string Identifier(this XElement element) =>
            element.GetAttributeValue("identifier");

        private static IList<BaseValue> GetBaseValues(this XElement qtiElement)
        {
            var baseValues = qtiElement.FindElementsByName("baseValue")
                .Select(childElement =>
                {
                    return new BaseValue()
                    {
                        BaseType = childElement.GetAttributeValue("baseType").ToBaseType(),
                        Value = childElement.Value.RemoveXData(),
                        Identifier = childElement.Identifier()
                    };
                }).ToList();
            return baseValues;
        }

        private static IList<BaseValue> GetOutcomeVariables(this XElement qtiElement, Dictionary<string, OutcomeVariable> outcomeVariables)
        {
            return qtiElement.GetOutcomeVariables(outcomeVariables, null);
        }
        private static IList<BaseValue> GetOutcomeVariables(this XElement qtiElement, Dictionary<string, OutcomeVariable> outcomeVariables, AssessmentItem assessmentItem)
        {
            var variables = qtiElement.FindElementsByName("variable")
              .Select(childElement =>
              {
                  var identifier = childElement.Identifier();
                  if (outcomeVariables.ContainsKey(identifier))
                  {
                      return outcomeVariables[identifier].ToBaseValue();
                  }
                  else
                  {
                      if (assessmentItem != null && assessmentItem.OutcomeDeclarations.ContainsKey(identifier))
                      {
                          return assessmentItem.OutcomeDeclarations[identifier].ToVariable().ToBaseValue();
                      }
                      return null;
                  }
              })
              .Where(v => v != null)
              .ToList();
            return variables;
        }

        public static string RemoveXData(this string value)
        {
            if (value.Contains("<![CDATA["))
            {
                return value.Replace("<![CDATA[", "").Replace("]]>", "");
            }
            return value;
        }

        private static BaseValue ToBaseValue(this OutcomeVariable outcomeVariable)
        {
            return new BaseValue { BaseType = outcomeVariable.BaseType, Value = outcomeVariable.Value.ToString(), Identifier = outcomeVariable.Identifier };
        }

        private static IList<BaseValue> GetResponseVariables(this XElement qtiElement, ResponseProcessorContext context)
        {
            var variables = qtiElement.FindElementsByName("variable")
              .Select(childElement =>
              {
                  var identifier = childElement.Identifier();
                  if (context.ItemResult.ResponseVariables.ContainsKey(identifier))
                  {
                      var responseVariable = context.ItemResult.ResponseVariables[identifier];
                      return new BaseValue { BaseType = responseVariable.BaseType, Value = responseVariable.Value, Identifier = identifier };
                  }
                  return null;
              })
              .Where(v => v != null)
              .ToList();
            return variables;
        }


        public static IList<BaseValue> GetValues(this XElement qtiElement, OutcomeProcessorContext context)
        {
            var itemOutcomes = context.AssessmentResult.ItemResults
               .Select(i => i.Value)
               .SelectMany(i =>
               {
                   return i.OutcomeVariables.Select(o =>
                   {
                       // find the variable to apply weight
                       var weightedValue = o.Value.Value;
                       var itemVariableElement = qtiElement.FindElementsByElementAndAttributeValue("variable", "identifier", $"{i.Identifier}.{o.Key}").FirstOrDefault();
                       if (itemVariableElement != null)
                       {
                           var weightIdentifier = itemVariableElement.GetAttributeValue("weightIdentifier");
                           if (!string.IsNullOrEmpty(weightIdentifier))
                           {
                               var itemRef = context.AssessmentTest.AssessmentItemRefs[i.Identifier];
                               if (itemRef.Weights.ContainsKey(weightIdentifier))
                               {
                                   if (float.TryParse(o.Value.Value.ToString(), out var floatValue))
                                   {
                                       weightedValue = floatValue * itemRef.Weights[weightIdentifier];
                                   }
                               }
                               else
                               {
                                   context.LogError($"cannot find weight identifier: {weightIdentifier} for item: {i.Identifier}");
                               }
                           }
                       }                     
                       var outcome = new OutcomeVariable
                       {
                           BaseType = o.Value.BaseType,
                           Cardinality = o.Value.Cardinality,
                           Value = weightedValue,
                           Identifier = o.Value.Identifier
                       };
                       return new
                       {
                           Name = i.Identifier,
                           Identifier = o.Key,
                           Variable = outcome
                       };
                   });
               })
               .ToDictionary(o => $"{o.Name}.{o.Identifier}", o => o.Variable);

            var testOutcomes = context.AssessmentResult.TestResults
                .Select(i => i.Value)
                .SelectMany(i => i.OutcomeVariables.Select(o => o.Value));

            var allOutcomes = itemOutcomes.Concat(testOutcomes
                    .ToDictionary(t => t.Identifier, t => t))
                    .ToDictionary(x => x.Key, x => x.Value);

            var baseValues = qtiElement.GetBaseValues();
            var variables = qtiElement.GetOutcomeVariables(allOutcomes);

            var testVariables = qtiElement.FindElementsByName("testVariables")
                .Select(testVariableElement =>
                {
                    var qtiValues = context.AssessmentTest.AssessmentItemRefs.Values.Where(assessmentItemRef =>
                {
                    var excludedCategoriesString = testVariableElement.GetAttributeValue("excludeCategory");
                    var excludedCategories = !string.IsNullOrWhiteSpace(excludedCategoriesString) ?
                        excludedCategoriesString.Split(" ") : null;

                    var includeCategoriesString = testVariableElement.GetAttributeValue("includeCategory");
                    var includeCategories = !string.IsNullOrWhiteSpace(includeCategoriesString) ?
                    includeCategoriesString.Split(" ") : null;
                    if (excludedCategories?.Length > 0)
                    {
                        foreach (var excludedCategory in excludedCategories)
                        {
                            if (assessmentItemRef.Categories.Contains(excludedCategory))
                            {
                                return false;
                            }
                        }
                    }
                    if (includeCategories?.Length > 0)
                    {
                        foreach (var includeCategory in includeCategories)
                        {
                            if (assessmentItemRef.Categories.Contains(includeCategory))
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                    return true; // if not included or excluded categories are defined return all
                }).Select(assesmentItemRef =>
                {
                    var weightIdentifier = testVariableElement.GetAttributeValue("weightIdentifier");
                    var variableIdentifier = testVariableElement.GetAttributeValue("variableIdentifier");
                    var itemRefIdentifier = $"{assesmentItemRef.Identifier}.{variableIdentifier}";
                    if (allOutcomes.ContainsKey(itemRefIdentifier))
                    {
                        var outcome = allOutcomes[itemRefIdentifier];
                        if (!string.IsNullOrWhiteSpace(weightIdentifier))
                        {
                            if (assesmentItemRef.Weights != null && assesmentItemRef.Weights.ContainsKey(weightIdentifier))
                            {
                                return new OutcomeVariable
                                {
                                    BaseType = outcome.BaseType,
                                    Cardinality = outcome.Cardinality,
                                    Identifier = outcome.Identifier,
                                    Value = float.Parse(outcome.Value.ToString()) * assesmentItemRef.Weights[weightIdentifier]
                                };
                            }
                            else
                            {
                                context.LogWarning($"Cannot find weight with identifier: {weightIdentifier} from item: {itemRefIdentifier}");

                            }
                        }
                        return outcome;
                    }
                    else
                    {
                        context.LogError($"Cannot find assessmentItemRef outcomeVariable: {itemRefIdentifier}");
                        return null;
                    }
                    // return GetItemReferenceVariables(assesmentItemRef.Identifier, variableIdentifier, "testVariable", context);
                }).Where(v => v != null);
                    return new BaseValue
                    {
                        Identifier = "testIdentifier",
                        BaseType = BaseType.Float,
                        Value = qtiValues
                        .Where(v => float.TryParse(v.Value.ToString(), out var s))
                        .Sum(v => float.Parse(v.Value.ToString()) * 1).ToString()
                    };
                });
            return baseValues
                .Concat(variables)
                .Concat(testVariables)
                .ToList();
        }

        public static IList<BaseValue> GetValues(this XElement qtiElement, ResponseProcessorContext context)
        {
            var baseValues = qtiElement.GetBaseValues();
            var outcomeVariables = context.ItemResult?.OutcomeVariables;
            var variables = qtiElement.GetOutcomeVariables(outcomeVariables, context.AssessmentItem).Concat(qtiElement.GetResponseVariables(context));
            var correct = qtiElement.FindElementsByName("correct")
            .Select(childElement =>
            {
                var identifier = childElement.Identifier();
                if (context.AssessmentItem.ResponseDeclarations.ContainsKey(identifier))
                {

                    var dec = context.AssessmentItem.ResponseDeclarations[identifier];
                    if (string.IsNullOrWhiteSpace(dec.CorrectResponse))
                    {
                        context.LogError($"Correct: {identifier} references to a response without correctResponse");
                        return null;
                    }
                    return new BaseValue { BaseType = dec.BaseType, Value = dec.CorrectResponse };
                }
                else
                {
                    context.LogError($"Cannot reference to response declaration for correct {identifier}");
                    return null;
                }
            })
            .Where(v => v != null)
            .ToList();

            var allValues = baseValues.Concat(variables).Concat(correct).ToList();
            return allValues;
        }

        public static string GetAttributeValue(this XElement el, string name)
        {
            return el.GetAttribute(name)?.Value ?? string.Empty;
        }
        public static XAttribute GetAttribute(this XElement el, string name)
        {
            return el.Attributes()
                .FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public static IEnumerable<XAttribute> GetAttributes(this XDocument doc, string name)
        {
            var s = doc.Descendants().SelectMany(d => d.Attributes()
                .Where(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)));
            return s;
        }

        public static IEnumerable<XElement> FindElementsByElementAndAttributeValue(this XElement element, string elementName, string attributeName, string attributeValue)
        {
            return element.FindElementsByName(elementName)
                .Where(d => d.Attributes()
                    .Any(a => a.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase) &&
                              a.Value.Equals(attributeValue, StringComparison.OrdinalIgnoreCase)));
        }

        public static IEnumerable<XElement> FindElementsByElementAndAttributeValue(this XDocument doc, string elementName, string attributeName, string attributeValue)
        {
            return doc.FindElementsByName(elementName)
                .Where(element => element.Attributes()
                    .Any(a => a.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase) &&
                              a.Value.Equals(attributeValue, StringComparison.OrdinalIgnoreCase)));
        }

        public static IEnumerable<XElement> FindElementsByElementAndAttributeStartValue(this XDocument doc, string elementName, string attributeName, string attributeValue)
        {
            return doc.FindElementsByName(elementName)
                .Where(element => element.Attributes()
                    .Any(a => a.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase) &&
                              a.Value.ToLower().StartsWith(attributeValue.ToLower())));
        }

        public static string GetElementValue(this XElement el, string name)
        {
            return el.FindElementsByName(name).FirstOrDefault()?.Value ?? String.Empty;
        }
        //xmlns

        public static IEnumerable<XElement> GetInteractions(this XDocument doc)
        {
            return doc.Document?.Root.GetInteractions();
        }

        public static IEnumerable<XElement> GetInteractions(this XElement el)
        {
            var qti2Elements = el.FindElementsByLastPartOfName("interaction")
                .Where(d => d.Name.LocalName.Contains("audio", StringComparison.OrdinalIgnoreCase))
                .Where(d => d.Attributes()
                    .Any(a => a.Name.LocalName.Equals("responseIdentifier", StringComparison.OrdinalIgnoreCase) &&
                              a.Value.Equals("RESPONSE", StringComparison.OrdinalIgnoreCase)));
            var qti3Elements = el.FindElementsByLastPartOfName("Interaction")
                .Where(d => d.Name.LocalName.Contains("audio", StringComparison.OrdinalIgnoreCase))
                .Where(d => d.Attributes()
                    .Any(a => a.Name.LocalName.Equals("response-identifier", StringComparison.OrdinalIgnoreCase) &&
                              a.Value.Equals("RESPONSE", StringComparison.OrdinalIgnoreCase)));
            return qti2Elements.Concat(qti3Elements);
        }
        public static XElement GetInteraction(this XElement element)
        {
            return element.GetInteractions().FirstOrDefault();
        }
        public static XElement GetInteraction(this XDocument doc)
        {
            return doc.GetInteractions().FirstOrDefault();
        }
        public static void SetAttributeValue(this XElement el, string name, string value)
        {
            el.GetAttribute(name)?.SetValue(value);
        }

        public static XElement ToXElement(this BaseValue value)
        {
            return XElement.Parse($"<baseValue baseType=\"{value.BaseType.GetString()}\">{value.Value}</baseValue>");
        }

        public static OutcomeVariable ToVariable(this OutcomeDeclaration outcomeDeclaration)
        {
            return new OutcomeVariable
            {
                BaseType = outcomeDeclaration.BaseType,
                Cardinality = outcomeDeclaration.Cardinality,
                Identifier = outcomeDeclaration.Identifier,
                Value = outcomeDeclaration.DefaultValue
            };
        }

        public static XElement ToVariableElement(this OutcomeDeclaration outcomeDeclaration)
        {
            return XElement.Parse($"<variable identifier=\"{outcomeDeclaration.Identifier}\" />");
        }

        public static XElement ToElement(this OutcomeDeclaration outcomeDeclaration)
        {
            return XElement.Parse($"<outcomeDeclaration " +
                $"identifier=\"{outcomeDeclaration.Identifier}\" cardinality=\"{outcomeDeclaration.Cardinality.GetString()}\" " +
                $"baseType=\"{outcomeDeclaration.BaseType.GetString()}\"><defaultValue><value>{outcomeDeclaration.DefaultValue}</value></defaultValue></outcomeDeclaration>");
        }

        public static XElement ToValueElement(this string value)
        {
            return XElement.Parse($"<value>{value}</value>");
        }

        public static XElement AddDefaultNamespace(this XElement element, XNamespace xnamespace)
        {
            element.Name = xnamespace + element.Name.LocalName;
            foreach (var child in element.Descendants())
            {
                child.Name = xnamespace + child.Name.LocalName;
            }
            return element;
        }
        public static OutcomeDeclaration ToOutcomeDeclaration(this float value, string identifier = "SCORE")
        {
            return new OutcomeDeclaration
            {
                Identifier = identifier,
                BaseType = BaseType.Float,
                Cardinality = Cardinality.Single,
                DefaultValue = value
            };
        }

        public static BaseValue ToBaseValue(this float value, string identifier = "SCORE")
        {
            return new BaseValue
            {
                BaseType = BaseType.Float,
                Value = value.ToString()
            };
        }

        public static OutcomeVariable ToOutcomeVariable(this float value, string identifier = "SCORE")
        {
            return new OutcomeVariable
            {
                BaseType = BaseType.Float,
                Value = value.ToString()
            };
        }

        /// <summary>
        /// This adds total and weighted score for all summed items +
        /// total and weighted score for all categories.
        /// </summary>
        /// <param name="assessmentTest"></param>
        /// <returns></returns>
        public static AssessmentTest AddTotalAndCategoryScores(this AssessmentTest assessmentTest)
        {
            var changedTest = assessmentTest
                .AddTestOutcome("SCORE_TOTAL", "", null)
                .AddTestOutcome("SCORE_TOTAL_WEIGHTED", "WEIGHT", null)
                .AddTestOutcomeForCategories("SCORE_TOTAL", "")
                .AddTestOutcomeForCategories("SCORE_TOTAL_WEIGHTED", "WEIGHT");
            changedTest.Init();
            return changedTest;
        }

        public static AssessmentTest AddTestOutcomeForCategories(this AssessmentTest assessmentTest, string identifierPrefix, string weightIdentifier)
        {
            assessmentTest.Categories.ForEach(c =>
            {
                assessmentTest = assessmentTest.AddTestOutcome($"{identifierPrefix}_{c}", weightIdentifier, new List<string> { c });
            });
            return assessmentTest;
        }

        public static AssessmentTest AddTestOutcome(this AssessmentTest assessmentTest, string identifier, string weightIdentifier, List<string> includedCategories, bool reInit = false)
        {
            var outcomeProcessing = assessmentTest.FindElementByName("outcomeProcessing");
            if (outcomeProcessing == null || !outcomeProcessing.FindElementsByElementAndAttributeValue("setOutcomeValue", "identifier", identifier).Any())
            {
                var testVariable = new SumTestVariable
                {
                    Identifier = identifier,
                    WeightIdentifier = weightIdentifier,
                    IncludedCategories = includedCategories
                };
                var outcomeElement = testVariable.OutcomeElement();
                var testVariableElement = testVariable.ToSummedSetOutcomeElement();

                assessmentTest.Root.Add(outcomeElement.AddDefaultNamespace(assessmentTest.Root.GetDefaultNamespace()));

                if (outcomeProcessing == null)
                {
                    assessmentTest.Add(XElement.Parse("<outcomeProcessing></outcomeProcessing>"));
                    outcomeProcessing = assessmentTest.FindElementByName("outcomeProcessing");
                }
                outcomeProcessing.Add(testVariableElement.AddDefaultNamespace(assessmentTest.Root.GetDefaultNamespace()));
                if (reInit)
                {
                    assessmentTest.Init();
                }
                return assessmentTest;
            }
            else
            {
                // variable already exist
                return assessmentTest;
            }

        }
    }
}