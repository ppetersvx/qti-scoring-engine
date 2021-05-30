﻿using Microsoft.Extensions.Logging;
using Moq;
using Citolab.QTI.Scoring.Helper;
using Citolab.QTI.Scoring.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Citolab.QTI.Scoring.ResponseProcessing.Operators;

namespace Citolab.QTI.Scoring.Tests
{
    public static class LogCheckExtensions
    {
        /// <summary>
        /// Verify that the log was called and get access to check the log arguments 
        /// </summary>
        public static void VerifyLog<TException>(
                   this Mock<ILogger> loggerMock,
                   Expression<Func<object, Type, bool>> match,
                   LogLevel logLevel,
                   int times) where TException : Exception
        {
            if (loggerMock == null) throw new ArgumentNullException(nameof(loggerMock));

            loggerMock.Verify
            (
                l => l.Log
                (
                    //Check the severity level
                    logLevel,
                    //This may or may not be relevant to your scenario
                    //If you need to check this, add a parameter for it
                    It.IsAny<EventId>(),
                    //This is the magical Moq code that exposes internal log processing from the extension methods
                    It.Is<It.IsAnyType>(match),
                    //Confirm the exception type
                    It.IsAny<TException>(),
                    //Accept any valid Func here. The Func is specified by the extension methods
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()
                ),
                //Make sure the message was logged the correct number of times
                Times.Exactly(times)
            );
        }

        /// <summary>
        /// Verify that the log was called and get access to check the log arguments 
        /// </summary>
        public static void VerifyLog(
           this Mock<ILogger> loggerMock,
           Expression<Func<object, Type, bool>> match,
           LogLevel logLevel,
           int times)
        => VerifyLog<Exception>(loggerMock, match, logLevel, times);

        /// <summary>
        /// Check whether or not the log arguments match the expected result
        /// </summary>
        public static bool CheckValue<T>(this object state, string key, T expectedValue)
        => CheckValue<T>(state, key, (actualValue)
               => (actualValue == null && expectedValue == null) || (actualValue != null && actualValue.Equals(expectedValue)));

        public static bool ContainsValue<T>(this object state, T partOfValue)
       => CheckValue<T>(state, "{OriginalFormat}", (actualValue)
              => (actualValue == null && partOfValue == null) || (actualValue != null && actualValue.ToString().Contains(partOfValue.ToString())));

        /// <summary>
        /// Check whether or not the log arguments match the expected result
        /// </summary>
        public static bool CheckValue<T>(this object state, string key, Func<T, bool> compare)
        {
            if (compare == null) throw new ArgumentNullException(nameof(compare));

            var exists = state.GetValue<T>(key, out var actualValue);

            return exists && compare(actualValue);
        }

        public static bool GetValue<T>(this object state, string key, out T value)
        {
            var keyValuePairList = (IReadOnlyList<KeyValuePair<string, object>>)state;

            var keyValuePair = keyValuePairList.FirstOrDefault(kvp => string.Compare(kvp.Key, key, StringComparison.Ordinal) == 0);

            value = (T)keyValuePair.Value;

            return keyValuePair.Key != null;
        }
    }

    public static class Extensions
    {
        internal static string GetScoreForItem(this AssessmentResult assessmentResult, string itemIdentifier, string outcomeIdentifier)
        {
            var itemResult = assessmentResult
                .FindElementsByElementAndAttributeValue("itemResult", "identifier", itemIdentifier)
                .FirstOrDefault();
            var outcomeVariable = itemResult?.FindElementsByElementAndAttributeValue("outcomeVariable", "identifier", outcomeIdentifier).FirstOrDefault();
            return outcomeVariable?.Value;
        }

        internal static string GetScoreForTest(this AssessmentResult assessmentResult, string testIdentifier, string outcomeIdentifier)
        {
            var testResult = assessmentResult
                .FindElementsByElementAndAttributeValue("testResult", "identifier", testIdentifier)
                .FirstOrDefault();
            var outcomeVariable = testResult?.FindElementsByElementAndAttributeValue("outcomeVariable", "identifier", outcomeIdentifier).FirstOrDefault();
            return outcomeVariable?.Value;
        }
        internal static void ChangeResponse(this AssessmentResult assessmentTest, string itemId, string responseIdentifier, string newValue)
        {
            var itemResult = assessmentTest
            .FindElementsByElementAndAttributeValue("itemResult", "identifier", itemId)
            .FirstOrDefault();
            if (itemResult != null)
            {
                var value = itemResult.FindElementsByElementAndAttributeValue("responseVariable", "identifier", responseIdentifier).FirstOrDefault()?
                    .FindElementsByName("value").FirstOrDefault();
                if (value != null)
                {
                    value.Value = newValue;
                }
                assessmentTest.ItemResults[itemId].ResponseVariables[responseIdentifier].Value = newValue;
            }
        }

        internal static AssessmentResult ChangeItemResult(this AssessmentResult result, string itemIdentifier, string value, string outcomeIdentifier = "SCORE")
        {
            if (result.ItemResults.ContainsKey(itemIdentifier))
            {
                var itemResult = result.ItemResults[itemIdentifier];

                var currentOutcome = itemResult.OutcomeVariables[outcomeIdentifier];
                currentOutcome.Value = value;

                var currentItemResult = result.FindElementsByElementAndAttributeValue("itemResult", "identifier", itemIdentifier).FirstOrDefault();
                currentItemResult.Remove();
                result.Root.Add(itemResult.ToElement().AddDefaultNamespace(result.Root.GetDefaultNamespace()));
            }
            return result;
        }

        internal static XElement GetElementWithValue(this ResponseIf _, bool result, string value, string identifier)
        {
            var gte1 = result ? 1 : 0;
            return XElement.Parse($@"<responseIf><gte><baseValue baseType=""float"">{gte1}</baseValue><baseValue baseType=""float"">1</baseValue></gte><setOutcomeValue identifier=""{identifier}""><baseValue baseType=""string"">{value}</baseValue></setOutcomeValue></responseIf>");
        }

        internal static XElement GetElementWithValue(this ResponseElseIf _, bool result, string value, string identifier)
        {
            var gte1 = result ? 1 : 0;
            return XElement.Parse($@"<responseIf><gte><baseValue baseType=""float"">{gte1}</baseValue><baseValue baseType=""float"">1</baseValue></gte><setOutcomeValue identifier=""{identifier}""><baseValue baseType=""string"">{value}</baseValue></setOutcomeValue></responseIf>");
        }

        internal static XElement GetElementWithValue(this ResponseElse _, bool result, string value, string identifier)
        {
            var gte1 = result ? 1 : 0;
            return XElement.Parse($@"<responseIf><gte><baseValue baseType=""float"">{gte1}</baseValue><baseValue baseType=""float"">1</baseValue></gte><setOutcomeValue identifier=""{identifier}""><baseValue baseType=""string"">{value}</baseValue></setOutcomeValue></responseIf>");
        }


        internal static ResponseVariable ToResponseVariable(this string value, string identifier = "RESPONSE")
        {
            return new ResponseVariable
            {
                Identifier = identifier,
                BaseType = BaseType.Float,
                Cardinality = Cardinality.Single,
                Value = value
            };
        }

        internal static BaseValue ToBaseValue(this float value)
        {
            return new BaseValue
            {
                BaseType = BaseType.Float,
                Value = value.ToString()
            };
        }

        internal static BaseValue ToBaseValue(this string value)
        {
            return new BaseValue
            {
                BaseType = BaseType.String,
                Value = value
            };
        }

        internal static OutcomeVariable ToOutcomeVariable(this float value, string identifier = "SCORE")
        {
            return new OutcomeVariable
            {
                Identifier = identifier,
                BaseType = BaseType.Float,
                Value = value.ToString()
            };
        }

        internal static AssessmentResult AddCandidateResponse(this AssessmentResult assessmentResult, string itemIdentifer, string responseIdentifer, string value, BaseType baseType, Cardinality cardinality)
        {
            return AddCandidateResponses(assessmentResult, itemIdentifer, responseIdentifer, new List<string> { value }, baseType, cardinality);
        }
        internal static AssessmentResult AddCandidateResponses(this AssessmentResult assessmentResult, string itemIdentifer, string responseIdentifer, List<string> values, BaseType baseType, Cardinality cardinality)
        {
            if (!assessmentResult.ItemResults.ContainsKey(itemIdentifer))
            {
                assessmentResult.ItemResults.Add(itemIdentifer, new ItemResult { Identifier = itemIdentifer });
            }
            var itemResult = assessmentResult.ItemResults[itemIdentifer];
            if (!itemResult.ResponseVariables.ContainsKey(responseIdentifer))
            {
                if (cardinality == Cardinality.Single)
                {
                    itemResult.ResponseVariables.Add(responseIdentifer, new ResponseVariable { BaseType = baseType, Cardinality = cardinality, Identifier = responseIdentifer, Value = values.FirstOrDefault() });
                } else
                {
                    itemResult.ResponseVariables.Add(responseIdentifer, new ResponseVariable { BaseType = baseType, Cardinality = cardinality, Identifier = responseIdentifer, Values = values });
                }
               
            }
            else
            {
                if (values.Count == 1)
                {
                    itemResult.ResponseVariables[responseIdentifer].Value = values.FirstOrDefault();
                } else
                {
                    itemResult.ResponseVariables[responseIdentifer].Values = values;
                }
                    
            }
            var itemResultElement = assessmentResult.FindElementsByElementAndAttributeValue("itemResult", "identifier", itemIdentifer).FirstOrDefault();
            if (itemResultElement != null)
            {
                itemResultElement.Remove();
            }
            itemResultElement = itemResult.ToElement();
            var responseVariable = itemResultElement.FindElementsByElementAndAttributeValue("responseVariable", "identifier", responseIdentifer).FirstOrDefault();
            if (responseVariable == null)
            {
                responseVariable = XElement.Parse($@"<responseVariable identifier=""{responseIdentifer}"" cardinality=""{cardinality.GetString()}"" baseType=""{baseType.GetString()}""></responseVariable>");
                itemResultElement.Add(responseVariable);
            }
            var candidateResponseElement = XElement.Parse($@"<candidateResponse></candidateResponse>");
            foreach (var value in values)
            {
                candidateResponseElement.Add(XElement.Parse($@"<value>{value}</value>"));
            }
            responseVariable.Add(candidateResponseElement);
            assessmentResult.Root.Add(itemResultElement);
            return assessmentResult;
        }

    }
}
