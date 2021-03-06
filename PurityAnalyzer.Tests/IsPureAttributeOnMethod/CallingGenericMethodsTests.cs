﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using PurityAnalyzer.Tests.IsPureAttributeOnMethod.OverriddenMethods;

namespace PurityAnalyzer.Tests.IsPureAttributeOnMethod
{
    [TestFixture]
    public class CallingGenericMethodsTests
    {
        [Test]
        public void CallingSimpleGenericMethod_MethodIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Class1 
{
}

public static class Module1
{
    [IsPure]
    public static void DoSomething()
    {
        GenericMethod<Class1>(new Class1());
    }

    public static void GenericMethod<T>(T param)
    {

    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void CallingGenericMethodWhereTHasNoConstraintsAndTypeArgumentToStringMethodIsImpure_AndToStringMethodOfTIsUsed_MethodIsImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Class1 
{
    public static int state = 0;

    public override string ToString()
    {
        state++;
        return """";
    }
}

public static class Module1
{
    [IsPure]
    public static void DoSomething()
    {
        GenericMethod<Class1>(new Class1());
    }

    public static void GenericMethod<T>(T param)
    {
        var str = param.ToString();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }


        [Test]
        public void CallingGenericMethodWhereTHasNoConstraintsAndTypeArgumentToStringMethodIsImpure_AndNoObjectMethodOfTIsUsed_MethodIsPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public class Class1 
{
    public static int state = 0;

    public override string ToString()
    {
        state++;
        return """";
    }
}

public static class Module1
{
    [IsPure]
    public static void DoSomething()
    {
        GenericMethod<Class1>(new Class1());
    }

    public static void GenericMethod<T>(T param)
    {

    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }


        [Test]
        public void CallingCompiledGenericMethodWhereTHasNoConstraintsAndTypeArgumentToStringMethodIsImpure_AndTIsNotAnnotatedWithAttributes_MethodIsImpure()
        {
            string code = @"
using System;
using PurityAnalyzer.Tests.CompiledCsharpLib;

public class IsPureAttribute : Attribute
{
}

public class Class1 
{
    public static int state = 0;

    public override string ToString()
    {
        state++;
        return """";
    }
}

public static class Module1
{
    [IsPure]
    public static void DoSomething()
    {
        ClassWithGenericMethods.MethodThatUsesTAsObject<Class1>(new Class1());
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code, Utilities.GetTestsCompiledCsharpLibProjectReference());
            dignostics.Length.Should().BePositive();
        }


        [Test]
        public void CallingCompiledGenericMethodWhereTHasNoConstraintsAndTypeArgumentToStringMethodIsImpure_TIsAnnotatedWithNotUsedAsObjectAttribute_MethodIsPure()
        {
            string code = @"
using System;
using PurityAnalyzer.Tests.CompiledCsharpLib;


public class IsPureAttribute : Attribute
{
}

public class Class1 
{
    public static int state = 0;

    public override string ToString()
    {
        state++;
        return """";
    }
}

public static class Module1
{
    [IsPure]
    public static void DoSomething()
    {
        ClassWithGenericMethods.MethodThatDoesNotUseTAsObject<Class1>(new Class1());
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code, Utilities.GetTestsCompiledCsharpLibProjectReference());
            dignostics.Length.Should().Be(0);
        }


        [Test]
        public void CallingGenericMethodThatInvokesToStringOnAGenericTypeParameter_FromAGenericMethodPassingCallingMethodTAsArgumentToCalledMethodT_KeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public static class Module1
{
    public static string Do<T>()
    {
        return default(T).ToString();
    }

    [IsPure]
    public static string Do2<T>()
    {
        return Do<T>();
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }

        [Test]
        public void CallingGenericMethodThatInvokesToStringOnAGenericTypeParameter_FromAGenericMethodPassingCallingMethodTAsArgumentToCalledMethodT_CalledMethodTypeParameterHasAConstraintWhichIsAnInterface_CallerMethodConstraintIsAClassWhichImplementationOfTheInterfaceIsImpure_MakesMethodImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public interface IInterface1
{
    void DoSomething();
}

public class Class1 : IInterface1
{
    private static int state;

    public void DoSomething()
    {
        state++;
    }
}

public static class Module1
{
    public static void Do<T>(T input) where T:IInterface1
    {
        input.DoSomething();
    }

    [IsPure]
    public static void Do2<T>(T input) where T: Class1
    {
        Do(input);
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void CallingGenericMethodThatInvokesToStringOnAGenericTypeParameter_FromAGenericMethodPassingCallingMethodTAsArgumentToCalledMethodT_CalledMethodTypeParameterHasAConstraintWhichIsAnInterface_CallerMethodConstraintIsAClassWhichImplementationOfTheInterfaceIsPure_KeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public interface IInterface1
{
    void DoSomething();
}

public class Class1 : IInterface1
{
    public void DoSomething()
    {
    }
}

public static class Module1
{
    public static void Do<T>(T input) where T:IInterface1
    {
        input.DoSomething();
    }

    [IsPure]
    public static void Do2<T>(T input) where T: Class1
    {
        Do(input);
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }


        [Test]
        public void CallingGenericMethodThatInvokesToStringOnAGenericTypeParameter_FromAGenericMethodPassingCallingMethodTAsArgumentToCalledMethodT_CalledMethodTypeParameterHasAConstraintWhichIsAnInterface_CallerMethodConstraintIsAClassWithImpureToString_MakesMethodImpure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}


public interface IInterface1
{
}


public class Class1 : IInterface1
{
    private static int state;

    public override string ToString()
    {
        state++;
        return string.Empty;
    }
}

public static class Module1
{
    public static string Do<T>(T input) where T: IInterface1
    {
        return input.ToString();
    }

    [IsPure]
    public static string Do2<T>(T input) where T: Class1
    {
        return Do(input);
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().BePositive();
        }

        [Test]
        public void CallingGenericMethodThatInvokesToStringOnAGenericTypeParameter_FromAGenericMethodPassingCallingMethodTAsArgumentToCalledMethodT_CalledMethodTypeParameterHasAConstraintWhichIsAnInterface_CallerMethodConstraintIsAClassWithPureToString_KeepsMethodPure()
        {
            string code = @"
using System;

public class IsPureAttribute : Attribute
{
}

public interface IInterface1
{
}


public class Class1 : IInterface1
{
    public override string ToString()
    {
        return string.Empty;
    }
}

public static class Module1
{
    public static string Do<T>(T input) where T: IInterface1
    {
        return input.ToString();
    }

    [IsPure]
    public static string Do2<T>(T input) where T: Class1
    {
        return Do(input);
    }
}";

            var dignostics = Utilities.RunPurityAnalyzer(code);
            dignostics.Length.Should().Be(0);
        }
    }
}
