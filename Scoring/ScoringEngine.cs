﻿using Citolab.QTI.Scoring.Model;
using Citolab.QTI.Scoring.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Citolab.QTI.Scoring.Helper;
using System.Threading.Tasks;
using Citolab.QTI.Scoring.OutcomeProcessing;
using Microsoft.Extensions.Logging;
using Citolab.QTI.Scoring.ResponseProcessing;

namespace Citolab.QTI.Scoring
{
    public class ScoringEngine : IScoringEngine
    {
        public List<XDocument> ProcessOutcomes(IOutcomeProcessingContext ctx)
        {
            if (ctx == null)
            {
                throw new ScoringEngineException("context cannot be null");
            }
            if (ctx.AssessmentTest == null)
            {
                throw new ScoringEngineException("AssessmentTest cannot be null when calling outcomeProcessing");
            }
            if (ctx.Logger == null)
            {
                ctx.Logger = ctx.Logger = new NullLogger<ScoringEngine>();
            }
            var assessmentTest = new AssessmentTest(ctx.Logger, ctx.AssessmentTest);
            if (ctx.ProcessParallel.HasValue && ctx.ProcessParallel == true)
            {
                Parallel.ForEach(ctx.AssessmentmentResults,
                    assessmentResultDoc => AssessmentResultOutcomeProcessing(assessmentResultDoc, assessmentTest, ctx.Logger));
            }
            else
            {
                ctx.AssessmentmentResults.ForEach(
                    assessmentResultDoc => AssessmentResultOutcomeProcessing(assessmentResultDoc, assessmentTest, ctx.Logger));
            }
            return ctx.AssessmentmentResults;
        }

        public List<XDocument> ProcessResponses(IResponseProcessingContext ctx)
        {
            if (ctx == null)
            {
                throw new ScoringEngineException("context cannot be null");
            }
            if (ctx.AssessmentItems == null)
            {
                throw new ScoringEngineException("AssessmentItems cannot be null when calling responseProcessing");
            }
            if (ctx.Logger ==null)
            {
                ctx.Logger = ctx.Logger = new NullLogger<ScoringEngine>();
            }
            var assessmentItems = ctx.AssessmentItems
                .Select(assessmentItemDoc => new AssessmentItem(ctx.Logger, assessmentItemDoc))
                .ToList();
            if (ctx.ProcessParallel.HasValue && ctx.ProcessParallel == true)
            {
                Parallel.ForEach(ctx.AssessmentmentResults,
                    assessmentResultDoc => AssessmentResultResponseProcessing(assessmentResultDoc, assessmentItems, ctx.Logger));
            }
            else
            {
                ctx.AssessmentmentResults.ForEach(
                    assessmentResultDoc => AssessmentResultResponseProcessing(assessmentResultDoc, assessmentItems, ctx.Logger));
            }
            return ctx.AssessmentmentResults;
        }

        public List<XDocument> ProcessResponsesAndOutcomes(IScoringContext ctx)
        {
            ProcessResponses(ctx);
            ProcessOutcomes(ctx);
            return ctx.AssessmentmentResults;
        }


        private void AssessmentResultOutcomeProcessing(XDocument assessmentResultDocument, AssessmentTest assessmentTest, ILogger logger)
        {
            var assessmentResult = new AssessmentResult(logger, assessmentResultDocument);
            OutcomeProcessor.Process(assessmentTest, assessmentResult, logger);
        }

        private void AssessmentResultResponseProcessing(XDocument assessmentResultDocument, List<AssessmentItem> assessmentItems, ILogger logger)
        {
            var assessmentResult = new AssessmentResult(logger, assessmentResultDocument);
            foreach(var assessmentItem in assessmentItems)
            {
                ResponseProcessor.Process(assessmentItem, assessmentResult, logger);
            }
        }
    }
}