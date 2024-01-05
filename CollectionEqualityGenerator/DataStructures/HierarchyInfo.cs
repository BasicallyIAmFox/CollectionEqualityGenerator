// Copyright 2023-2024 BasicallyIAmFox
// 
//    Licensed under the CLA (the "License")
//    See LICENSE.txt for more info.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CollectionEqualityGenerator.DataStructures;

public readonly struct HierarchyInfo : IEquatable<HierarchyInfo> {
	public readonly ref struct WriteObject {
		private readonly IndentedStringBuilder _writer;
		private readonly HierarchyInfo _info;

		internal WriteObject(IndentedStringBuilder writer, HierarchyInfo info) {
			_writer = writer;
			_info = info;

			Init();
		}

		private void Init() {
			// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
			foreach (var typeInfo in _info._hierarchyTypeInfos) {
				if (typeInfo.Type is HierarchyType.Unknown)
					continue;

				_writer.Write(typeInfo.Type switch {
					HierarchyType.Namespace => "namespace",
					HierarchyType.Class => "partial class",
					HierarchyType.RecordClass => "partial record class",
					HierarchyType.Struct => "partial struct",
					HierarchyType.RecordStruct => "partial record struct",
					HierarchyType.RefStruct => "partial ref struct",
					_ => default
				});
				_writer.Write(' ');
				_writer.Write(typeInfo.Name);
				_writer.WriteLine();

				_writer.WriteLine('{');
				_writer.Indent++;
			}
		}

		// ReSharper disable once UnusedMember.Global
		public void Dispose() {
			// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
			foreach (var typeInfo in _info._hierarchyTypeInfos) {
				if (typeInfo.Type is HierarchyType.Unknown)
					continue;

				_writer.Indent--;
				_writer.WriteLine('}');
			}
		}
	}

	private readonly EquatableArray<HierarchyTypeInfo> _hierarchyTypeInfos;

	public HierarchyInfo(ISymbol symbol) {
		var hiBuilder = ImmutableArray.CreateBuilder<HierarchyTypeInfo>();

		PopulateBuilder(symbol, hiBuilder);

		_hierarchyTypeInfos = hiBuilder.ToImmutable();
	}
	
	[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
	private static void PopulateBuilder(ISymbol symbol, ImmutableArray<HierarchyTypeInfo>.Builder builder) {
		for (var containingSymbol = symbol; containingSymbol != null; containingSymbol = containingSymbol.ContainingSymbol) {
			if (containingSymbol is INamespaceOrTypeSymbol namedTypeSymbol) {
				builder.Insert(0, new HierarchyTypeInfo(namedTypeSymbol.Name, namedTypeSymbol switch {
					ITypeSymbol { IsReferenceType: true, IsRecord: false } => HierarchyType.Class,
					ITypeSymbol { IsReferenceType: true, IsRecord: true } => HierarchyType.RecordClass,
					ITypeSymbol { IsValueType: true, IsRefLikeType: false, IsRecord: false } => HierarchyType.Struct,
					ITypeSymbol { IsValueType: true, IsRefLikeType: false, IsRecord: true } => HierarchyType.RecordStruct,
					ITypeSymbol { IsValueType: true, IsRefLikeType: true, IsRecord: false } => HierarchyType.RefStruct,
					{ IsNamespace: true } and not INamespaceSymbol { IsGlobalNamespace: true } => HierarchyType.Namespace,
					_ => HierarchyType.Unknown
				}));
			}
		}
	}

	public override int GetHashCode() {
		return _hierarchyTypeInfos.GetHashCode();
	}

	public bool Equals(HierarchyInfo other) {
		return _hierarchyTypeInfos.Equals(other._hierarchyTypeInfos);
	}

	public override bool Equals(object? obj) {
		return obj is HierarchyInfo other && Equals(other);
	}
}

public readonly record struct HierarchyTypeInfo(in string Name, in HierarchyType Type);

public enum HierarchyType {
	Unknown,
	Namespace,
	Class,
	Struct,
	RefStruct,
	RecordClass,
	RecordStruct
}

public static class HierarchyInfoExtensions {
	public static HierarchyInfo RetrieveHierarchyTree(this ISymbol symbol) {
		return new HierarchyInfo(symbol);
	}
}
