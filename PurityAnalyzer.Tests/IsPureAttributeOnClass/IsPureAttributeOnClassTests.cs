﻿using FluentAssertions;
using NUnit.Framework;

namespace PurityAnalyzer.Tests.IsPureAttributeOnClass
{
    [TestFixture]
    public class IsPureAttributeOnClassTests
    {
        [Test]
        public void IsPureOnClassRequiresStaticMethodsToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    static int c = 1;
    
    public static string DoSomething()
    {
        return c.ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void IsPureOnClassRequiresInstanceMethodsToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    int c = 1;
    
    public string DoSomething()
    {
        return c.ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void IsPureOnClassRequiresStaticPropertiesToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    static int a;

    
    public static int Prop1
    {
        get
        {
            return a++;
        }
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void IsPureOnClassRequiresInstancePropertiesToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    int a;
    
    public int Prop1
    {
        get
        {
            return a++;
        }
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void IsPureOnClassRequiresStaticConstructorToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    static Class1() => AnotherClass.state = 1;
}

public static class AnotherClass
{
    public static int state;
}
";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }

        [Test]
        public void IsPureOnClassRequiresInstanceConstructorToBePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{
    public Class1() => AnotherClass.state = 1;
}

public static class AnotherClass
{
    public static int state;
}
";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();

        }


        [Test]
        public void CaseWhereMembersArePure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

[IsPure]
public class Class1
{

    public int Prop1
    {
        get
        {
            return 1;
        }
    }

    public int DoSomething(int a)
    {
        return a + 1;
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);

        }

    }
}
