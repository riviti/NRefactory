﻿// Copyright (c) 2010 AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ICSharpCode.NRefactory.TypeSystem
{
	[TestFixture]
	public class ReflectionHelperTests
	{
		void TestGetClass(Type type)
		{
			ITypeDefinition t = CecilLoaderTests.Mscorlib.GetClass(type);
			Assert.NotNull(t, type.FullName);
			Assert.AreEqual(type.FullName, t.DotNetName);
		}
		
		[Test]
		public void TestGetInnerClass()
		{
			TestGetClass(typeof(Environment.SpecialFolder));
		}
		
		[Test]
		public void TestGetGenericClass1()
		{
			TestGetClass(typeof(Action<>));
		}
		
		[Test]
		public void TestGetGenericClass2()
		{
			TestGetClass(typeof(Action<,>));
		}
		
		[Test]
		public void TestGetInnerClassInGenericClass1()
		{
			TestGetClass(typeof(Dictionary<,>.ValueCollection));
		}
		
		[Test]
		public void TestGetInnerClassInGenericClass2()
		{
			TestGetClass(typeof(Dictionary<,>.ValueCollection.Enumerator));
		}
		
		[Test]
		public void TestToTypeReferenceInnerClass()
		{
			Assert.AreEqual("System.Environment+SpecialFolder",
			                typeof(Environment.SpecialFolder).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceUnboundGenericClass()
		{
			Assert.AreEqual("System.Action`1",
			                typeof(Action<>).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
			Assert.AreEqual("System.Action`2",
			                typeof(Action<,>).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceBoundGenericClass()
		{
			Assert.AreEqual("System.Action`1[[System.String]]",
			                typeof(Action<string>).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
			Assert.AreEqual("System.Action`2[[System.Int32],[System.Int16]]",
			                typeof(Action<int, short>).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		
		[Test]
		public void TestToTypeReferenceNullableType()
		{
			Assert.AreEqual("System.Nullable`1[[System.Int32]]",
			                typeof(int?).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceInnerClassInUnboundGenericType()
		{
			Assert.AreEqual("System.Collections.Generic.Dictionary`2+ValueCollection[[`0],[`1]]",
			                typeof(Dictionary<,>.ValueCollection).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceInnerClassInBoundGenericType()
		{
			Assert.AreEqual("System.Collections.Generic.Dictionary`2+KeyCollection[[System.String],[System.Int32]]",
			                typeof(Dictionary<string, int>.KeyCollection).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceArrayType()
		{
			Assert.AreEqual(typeof(int[]).FullName,
			                typeof(int[]).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceMultidimensionalArrayType()
		{
			Assert.AreEqual(typeof(int[,]).FullName,
			                typeof(int[,]).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceJaggedMultidimensionalArrayType()
		{
			Assert.AreEqual(typeof(int[,][,,]).FullName,
			                typeof(int[,][,,]).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferencePointerType()
		{
			Assert.AreEqual(typeof(int*).FullName,
			                typeof(int*).ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceByReferenceType()
		{
			Assert.AreEqual(typeof(int).MakeByRefType().FullName,
			                typeof(int).MakeByRefType().ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
		
		[Test]
		public void TestToTypeReferenceGenericType()
		{
			MethodInfo convertAllInfo = typeof(List<>).GetMethod("ConvertAll");
			Type parameterType = convertAllInfo.GetParameters()[0].ParameterType; // Converter[[`0],[``0]]
			// cannot resolve generic types without knowing the parent entity:
			Assert.AreEqual("System.Converter`2[[?],[?]]",
			                parameterType.ToTypeReference().Resolve(CecilLoaderTests.Mscorlib).DotNetName);
			// now try with parent entity:
			IMethod convertAll = CecilLoaderTests.Mscorlib.GetClass(typeof(List<>)).Methods.Single(m => m.Name == "ConvertAll");
			Assert.AreEqual("System.Converter`2[[`0],[``0]]",
			                parameterType.ToTypeReference(entity: convertAll).Resolve(CecilLoaderTests.Mscorlib).DotNetName);
		}
	}
}