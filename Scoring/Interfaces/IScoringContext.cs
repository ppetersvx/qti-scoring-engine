﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Citolab.QTI.Scoring.Interfaces
{
    public interface IScoringContext : IResponseProcessingContext, IOutcomeProcessingContext
    {
    }
    public interface IResponseProcessingContext : IScoringContextBase
    {
        List<XDocument> AssessmentItems { get; set; }
        List<ICustomOperator> CustomOperators { get; set; }
    }
    public interface IOutcomeProcessingContext : IScoringContextBase
    {
        XDocument AssessmentTest { get; set; }
    }
    public interface IScoringContextBase
    {

        List<XDocument> AssessmentmentResults { get; set; }
        ILogger Logger { get; set; }

        bool? ProcessParallel { get; set; }
    }
}
