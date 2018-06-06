﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace PurityAnalyzer.Tests
{
    [TestFixture]
    public class AssumeIsPureAttributeTests
    {
        [Test]
        public void MethodThatCallsAnImpureMethodThatHasTheAssumeIsPureAttributeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class AssumeIsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething()
    {
        return DoSomethingElseImpure();
    }

    private static int state;

    [AssumeIsPure]
    private static string DoSomethingElseImpure()
    {   
        return state.ToString();
    }

}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }


        [Test]
        public void MethodThatCallsAnImpureMethodInWhichTypeHasTheAssumeIsPureAttributeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class AssumeIsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething()
    {
        return AnotherModule.DoSomethingElseImpure();
    }
}

[AssumeIsPure]
public static class AnotherModule
{
    private static int state;

    public static string DoSomethingElseImpure()
    {   
        return state.ToString();
    }
}
";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }

        [Test]
        public void CallingPureHigherOrderFunctionWithAnImpureFunctionClassMarkedWithAssumeIsPureAttributeKeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class AssumeIsPureAttribute : Attribute
{
}

public interface IInterface
{
    string Call(int input);
}

[AssumeIsPure]
public class ImpureClass : IInterface
{
    int state = 0;

    public string Call(int input)
    {
        state++;

        return input.ToString();
    }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething()
    {
        return HigherOrderFunction(new ImpureClass());
    }

    [IsPure]
    public static string HigherOrderFunction(IInterface function)
    {
        return function.Call(1);
    }
}";
            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }

    }
}
