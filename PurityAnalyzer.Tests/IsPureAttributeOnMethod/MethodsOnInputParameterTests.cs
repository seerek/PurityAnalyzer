﻿using FluentAssertions;
using NUnit.Framework;

namespace PurityAnalyzer.Tests.IsPureAttributeOnMethod
{
    [TestFixture]
    public class MethodsOnInputParameterTests
    {
        [Test]
        public void MethodThatInvokesAMethodThatReadsAnAutomaticReadOnlyPropertyOnParameterWhoseTypeIsDefinedInCodeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public int Prop1 {get;} = 5;
    
    public int Method() => Prop1;

}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void MethodThatInvokesAMethodThatReadsAnAutomaticReadWritePropertyOnParameterWhoseTypeIsDefinedInCodeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public int Prop1 {get; set;} = 5;
    
    public int Method() => Prop1;

}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void MethodThatInvokesAMethodThatWritesAnAutomaticReadWritePropertyOnParameterWhoseTypeIsDefinedInCodeIsImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public int Prop1 {get; set;} = 5;
    
    public int Method()
    {
        Prop = 1;
        return 1;
    }

}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void MethodThatInvokesAMethodThatReadsAReadOnlyFieldOnParameterWhoseTypeIsDefinedInCodeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public readonly int Field = 5;
    
    public int Method() => Field;
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void MethodThatInvokesAMethodThatReadsAnReadWriteFieldOnParameterWhoseTypeIsDefinedInCodeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public int Field = 5;
    
    public int Method() => Field;
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void MethodThatInvokesAMethodThatWritesAnReadWriteFieldOnParameterWhoseTypeIsDefinedInCodeIsImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    public int Field = 5;
    
    public int Method()
    {
        Field = 1;
        return 1;
    }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method();
    }
}";
            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void MethodThatInvokesAMethodThatdReadsAnNonAutomaticReadOnlyPropertyThatModifiesStateOnParameterWhoseTypeIsDefinedInCodeIsImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    int state = 0;

    public int Prop1
    {
        get
        {
            state++;
            return 1;
        }
    }

    public int Method() => Prop1;
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        
        return input.Method().ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void MethodThatInvokesAMethodThatInvokesAnotherMethodThatReadsMutableStateOnParameterWhoseTypeIsDefinedInCodeIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    int state = 0;

    public int Method() => Method2();
    public int Method2() => state;
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        
        return input.Method().ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void MethodThatInvokesAMethodThatInvokesAnotherMethodThatWritesMutableStateOnParameterWhoseTypeIsDefinedInCodeIsImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Dto1
{
    int state = 0;

    public int Method() => Method2();
    public int Method2()
    {
        state = 2;
        return 1;
    }
}

public static class Module1
{
    [IsPure]
    public static string DoSomething(Dto1 input)
    {
        return input.Method().ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);

            dignostics.Length.Should().BePositive();
        }

    }
}