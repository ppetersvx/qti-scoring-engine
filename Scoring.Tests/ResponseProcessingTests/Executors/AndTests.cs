﻿using Citolab.QTI.ScoringEngine.Helper;
using Citolab.QTI.ScoringEngine.ResponseProcessing;
using Citolab.QTI.ScoringEngine.ResponseProcessing.Executors;
using Citolab.QTI.ScoringEngine.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace ScoringEngine.Tests.ResponseProcessingTests.Executors
{
    public class AndTests
    {
        [Fact]
        public void CheckTwoTrueValues()
        {
            // arrange
            var context = TestHelper.GetDefaultResponseProcessingContext(null);
            context.Executors = new Dictionary<string, Citolab.QTI.ScoringEngine.ResponseProcessing.Interfaces.IExecuteReponseProcessing>();

            var returnTrue = new ReturnTrue();
            var and = new And();

            context.Executors.Add(and.Name, and);
            context.Executors.Add(returnTrue.Name, returnTrue);

            var andElement = XElement.Parse("<and></and>");
            andElement.Add(XElement.Parse("<returnTrue/>"));
            andElement.Add(XElement.Parse("<returnTrue/>"));

            
            // act
            var result = and.Execute(andElement, context);

            //assert
            Assert.True(result);
        }

        [Fact]
        public void CheckOneTrueAndFalseValues()
        {
            // arrange
            var context = TestHelper.GetDefaultResponseProcessingContext(null);
            context.Executors = new Dictionary<string, Citolab.QTI.ScoringEngine.ResponseProcessing.Interfaces.IExecuteReponseProcessing>();
            var returnTrue = new ReturnTrue();
            var returnFalse = new ReturnFalse();
            var and = new And();
            context.Executors.Add(and.Name, and);
            context.Executors.Add(returnTrue.Name, returnTrue);
            context.Executors.Add(returnFalse.Name, returnFalse);
            var andElement = XElement.Parse("<and></and>");
            andElement.Add(XElement.Parse("<returnTrue/>"));
            andElement.Add(XElement.Parse("<returnFalse/>"));

            // act
            var result = and.Execute(andElement, context);
            //assert
            Assert.False(result);
        }

        [Fact]
        public void CheckFalseValues()
        {
            // arrange
            var context = TestHelper.GetDefaultResponseProcessingContext(null);
            context.Executors = new Dictionary<string, Citolab.QTI.ScoringEngine.ResponseProcessing.Interfaces.IExecuteReponseProcessing>();
            var returnTrue = new ReturnTrue();
            var returnFalse = new ReturnFalse();

            var and = new And();
            context.Executors.Add(and.Name, and);
            context.Executors.Add(returnTrue.Name, returnTrue);
            context.Executors.Add(returnFalse.Name, returnFalse);
            var andElement = XElement.Parse("<and></and>");
            andElement.Add(XElement.Parse("<returnFalse/>"));
            andElement.Add(XElement.Parse("<returnFalse/>"));
            // act
            var result = and.Execute(andElement, context);
            //assert
            Assert.False(result);
        }
    }
}