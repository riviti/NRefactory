﻿// Copyright (c) 2010 AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under MIT X11 license (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ICSharpCode.NRefactory.TypeSystem.Implementation
{
	/// <summary>
	/// Stores a set of types and allows resolving them.
	/// Synchronization is implemented using a <see cref="ReaderWriterLockSlim"/>.
	/// </summary>
	public sealed class TypeStorage : ITypeResolveContext
	{
		ReaderWriterLockSlim readWriteLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		
		public ReaderWriterLockSlim ReadWriteLock {
			get { return readWriteLock; }
		}
		
		#region FullNameAndTypeParameterCount
		struct FullNameAndTypeParameterCount
		{
			public readonly string FullName;
			public readonly int TypeParameterCount;
			
			public FullNameAndTypeParameterCount(string fullName, int typeParameterCount)
			{
				this.FullName = fullName;
				this.TypeParameterCount = typeParameterCount;
			}
		}
		
		sealed class FullNameAndTypeParameterCountComparer : IEqualityComparer<FullNameAndTypeParameterCount>
		{
			public static readonly FullNameAndTypeParameterCountComparer Ordinal = new FullNameAndTypeParameterCountComparer(StringComparer.Ordinal);
			
			public readonly StringComparer NameComparer;
			
			public FullNameAndTypeParameterCountComparer(StringComparer nameComparer)
			{
				this.NameComparer = nameComparer;
			}
			
			public bool Equals(FullNameAndTypeParameterCount x, FullNameAndTypeParameterCount y)
			{
				return x.TypeParameterCount == y.TypeParameterCount && NameComparer.Equals(x.FullName, y.FullName);
			}
			
			public int GetHashCode(FullNameAndTypeParameterCount obj)
			{
				return NameComparer.GetHashCode(obj.FullName);
			}
		}
		#endregion
		
		#region Type Dictionary Storage
		Dictionary<FullNameAndTypeParameterCount, ITypeDefinition>[] _typeDicts = {
			new Dictionary<FullNameAndTypeParameterCount, ITypeDefinition>(FullNameAndTypeParameterCountComparer.Ordinal)
		};
		readonly object typeDictsLock = new object();
		
		Dictionary<FullNameAndTypeParameterCount, ITypeDefinition> GetTypeDictionary(StringComparer nameComparer)
		{
			// Gets the dictionary for the specified comparer, creating it if necessary.
			// New dictionaries might be added during read accesses, so this method needs to be thread-safe,
			// as we allow concurrent read-accesses without locking.
			var typeDicts = this._typeDicts;
			foreach (var dict in typeDicts) {
				FullNameAndTypeParameterCountComparer comparer = (FullNameAndTypeParameterCountComparer)dict.Comparer;
				if (comparer.NameComparer == nameComparer)
					return dict;
			}
			
			try {
				// ensure we have a read-lock on the PC so that no concurrent modifications are attempted
				readWriteLock.EnterReadLock();
				// ensure that no other thread can try to lazy-create this (or another) dict
				lock (typeDictsLock) {
					typeDicts = this._typeDicts; // fetch fresh value after locking
					// try looking for it again, maybe it was added while we were waiting for a lock
					foreach (var dict in typeDicts) {
						FullNameAndTypeParameterCountComparer comparer = (FullNameAndTypeParameterCountComparer)dict.Comparer;
						if (comparer.NameComparer == nameComparer)
							return dict;
					}
					
					// now create new dict
					var oldDict = typeDicts[0]; // Ordinal dict
					var newDict = new Dictionary<FullNameAndTypeParameterCount, ITypeDefinition>(
						oldDict.Count,
						new FullNameAndTypeParameterCountComparer(nameComparer));
					foreach (var pair in oldDict) {
						// don't use Add() as there might be conflicts in the target language
						newDict[pair.Key] = pair.Value;
					}
					
					// add the new dict to the array of dicts
					var newTypeDicts = new Dictionary<FullNameAndTypeParameterCount, ITypeDefinition>[typeDicts.Length + 1];
					Array.Copy(typeDicts, 0, newTypeDicts, 0, typeDicts.Length);
					newTypeDicts[typeDicts.Length] = newDict;
					this._typeDicts = newTypeDicts;
					return newDict;
				}
			} finally {
				readWriteLock.ExitReadLock();
			}
		}
		#endregion
		
		#region ITypeResolveContext implementation
		/// <inheritdoc/>
		public ITypeDefinition GetClass(string fullTypeName, int typeParameterCount, StringComparer nameComparer)
		{
			if (fullTypeName == null)
				throw new ArgumentNullException("fullTypeName");
			if (nameComparer == null)
				throw new ArgumentNullException("nameComparer");
			var key = new FullNameAndTypeParameterCount(fullTypeName, typeParameterCount);
			ITypeDefinition result;
			if (GetTypeDictionary(nameComparer).TryGetValue(key, out result))
				return result;
			else
				return null;
		}
		
		/// <inheritdoc/>
		public IEnumerable<ITypeDefinition> GetClasses()
		{
			return _typeDicts[0].Values;
		}
		
		/// <inheritdoc/>
		public IEnumerable<ITypeDefinition> GetClasses(string nameSpace, StringComparer nameComparer)
		{
			return GetClasses().Where(c => nameComparer.Equals(nameSpace, c.Namespace));
		}
		
		/// <inheritdoc/>
		public IEnumerable<string> GetNamespaces()
		{
			return GetClasses().Select(c => c.Namespace).Distinct();
		}
		#endregion
		
		#region Synchronize
		/// <inheritdoc/>
		public ISynchronizedTypeResolveContext Synchronize()
		{
			readWriteLock.EnterReadLock();
			return new ReadWriteSynchronizedTypeResolveContext(this, readWriteLock);
		}
		
		sealed class ReadWriteSynchronizedTypeResolveContext : ProxyTypeResolveContext, ISynchronizedTypeResolveContext
		{
			readonly ReaderWriterLockSlim readWriteLock;
			bool disposed;
			
			public ReadWriteSynchronizedTypeResolveContext(ITypeResolveContext context, ReaderWriterLockSlim readWriteLock)
				: base(context)
			{
				this.readWriteLock = readWriteLock;
			}
			
			public void Dispose()
			{
				if (!disposed) {
					disposed = true;
					readWriteLock.ExitReadLock();
				}
			}
		}
		#endregion
		
		#region RemoveType
		/// <summary>
		/// Removes a type definition from this project content.
		/// </summary>
		public void RemoveType(string fullName, int typeParameterCount)
		{
			if (fullName == null)
				throw new ArgumentNullException("fullName");
			try {
				readWriteLock.EnterWriteLock();
				var key = new FullNameAndTypeParameterCount(fullName, typeParameterCount);
				foreach (var dict in _typeDicts) {
					dict.Remove(key);
				}
			} finally {
				readWriteLock.ExitWriteLock();
			}
		}
		#endregion
		
		#region UpdateType
		/// <summary>
		/// Adds the type definition to this project content.
		/// Replaces existing type definitions with the same name.
		/// </summary>
		public void UpdateType(ITypeDefinition typeDefinition)
		{
			if (typeDefinition == null)
				throw new ArgumentNullException("typeDefinition");
			try {
				readWriteLock.EnterWriteLock();
				var key = new FullNameAndTypeParameterCount(typeDefinition.FullName, typeDefinition.TypeParameterCount);
				foreach (var dict in _typeDicts) {
					dict[key] = typeDefinition;
				}
			} finally {
				readWriteLock.ExitWriteLock();
			}
		}
		#endregion
	}
}