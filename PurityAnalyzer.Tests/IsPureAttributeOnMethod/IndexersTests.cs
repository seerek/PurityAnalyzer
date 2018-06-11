﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace PurityAnalyzer.Tests.IsPureAttributeOnMethod
{
    [TestFixture]
    public class IndexersTests
    {
        [Test]
        public void AccessingPureIndexerGetKeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class MyClass
{
   public string this[string s]
   {
      get { return s; }
   }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(MyClass param)
    {
        return param[""str""];
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }

        [Test]
        public void AccessingPureIndexerSetKeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class MyClass
{
   public string this[string s]
   {
      set { }
   }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(MyClass param)
    {
        param[""str""] = ""anything"";

        return """";
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }

        [Test]
        public void AccessingImpureIndexerGetMakesMethodImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class MyClass
{
   static int state = 0;
   public string this[string s]
   {
      get { state++; return s; }
   }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(MyClass param)
    {
        return param[""str""];
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void AccessingImpureIndexerSetMakesMethodImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class MyClass
{
    static int state = 0;
    public string this[string s]
    {
        set { state++; }
    }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(MyClass param)
    {
        param[""str""] = ""anything"";

        return """";
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }
    }
}