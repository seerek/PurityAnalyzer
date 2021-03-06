﻿using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace PurityAnalyzer.Tests.IsPureAttributeOnMethod
{
    [TestFixture]
    public class LinqTests
    {
        [Test]
        public void CallingEnumerableLinqMethodsKeepsMethodPure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(int[] data)
    {
        return
            data
                .Where(x => x > 0)
                .Select(x => x + 1)
                .SelectMany(x => new []{x, x * 2})
                .GroupBy(x => x > 2)
                .Select(x => x.Key.ToString())
                .First();
    }
}";
            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void CallingSelectMethodAndPassedLambdaCallsImpureMethodMakesMethodImpure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(int[] data)
    {
        return
            data
                .Select(x => ImpureMethod(x))
                .First();
    }

    static int state = 0;

    public static string ImpureMethod(int input)
    {
        state++;

        return """";
    }

}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void CallingSelectMethodAndPassedLambdaIncrementsFieldMakesMethodImpure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static int DoSomething(int[] data)
    {
        return
            data
                .Select(x => state++)
                .First();
    }

    static int state = 0;
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void CallingSelectMethodAndPassedLambdaReadsMutableFieldMakesMethodImpure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static int DoSomething(int[] data)
    {
        return
            data
                .Select(x => state)
                .First();
    }

    static int state = 0;
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void CallingSelectMethodPassingImpureMethodGroupMakesMethodImpure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(int[] data)
    {
        return
            data
                .Select(ImpureMethod)
                .First();
    }

    static int state = 0;

    public static string ImpureMethod(int input)
    {
        state++;

        return """";
    }

}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }


        [Test]
        public void CallingSelectMethodPassingPureMethodGroupKeepsMethodPure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(int[] data)
    {
        return
            data
                .Select(PureMethod)
                .First();
    }

    static int state = 0;

    public static string PureMethod(int input)
    {
        return """";
    }

}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void CallingEnumerableLinqMethodsViaQuerySyntaxKeepsMethodPure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(int[] data)
    {
        var result =
            from x in data
            where x > 0
            let x2 = x + 1
            group x2 by x2 > 2 into g
            select g.Key;

        return result.First().ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }

        [Test]
        public void CallingEnumerableLinqMethodsViaQuerySyntaxAndLetUsesImpureMethodMakesMethodImpure()
        {
            string code = @"
using System;
using System.Linq;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    [IsPure]
    public static bool DoSomething(int[] data)
    {
        var result =
            from x in data
            where x > 0
            let x2 = ImpureMethod( x + 1)
            group x2 by x2 > 2 into g
            select g.Key;

        return result.First();
    }

    static int state = 0;

    public static int ImpureMethod(int input)
    {
        return state++;
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

    }
}
