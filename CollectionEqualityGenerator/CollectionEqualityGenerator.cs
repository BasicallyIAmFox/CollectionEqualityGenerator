﻿//
//    Copyright 2023-2024 BasicallyIAmFox
//
//    Licensed under the Apache License, Version 2.0 (the "License")
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using CollectionEqualityGenerator.DataStructures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CollectionEqualityGenerator;

[Generator]
public sealed class CollectionEqualityGenerator : IIncrementalGenerator {
	private static SymbolDisplayFormat QualifiedOmittedGlobalAndGenerics { get; } = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.None,
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted);

	private static SymbolDisplayFormat QualifiedIncludeGenerics { get; } = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted);

	[SuppressMessage("ReSharper", "ConvertToLambdaExpression")]
	[SuppressMessage("ReSharper", "InvertIf")]
	public void Initialize(IncrementalGeneratorInitializationContext ctx) {
		var provider = ctx.SyntaxProvider.ForAttributeWithMetadataName(
			AttributesGenerator.CollectionEqualityAttributeFullyQualifiedMetadataName,
			static (node, _) => node.IsKind(SyntaxKind.RecordDeclaration) ||
								node.IsKind(SyntaxKind.RecordStructDeclaration),
			static (ctx, _) => {
				var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
				var compilation = ctx.SemanticModel.Compilation;

				string symbolName = symbol.ToDisplayString(QualifiedOmittedGlobalAndGenerics);
				string symbolTypeName = symbol.ToDisplayString(QualifiedIncludeGenerics);
				var symbolHierarchy = symbol.RetrieveHierarchyTree();
				bool isValueType = symbol.IsValueType;

				var symbolMembersArray = symbol.GetMembers()
					.Where(symbol => symbol
						// We don't want any static or constants to be included.
						is IFieldSymbol { IsStatic: false, IsConst: false }
						or IPropertySymbol { IsStatic: false })
					.Where(symbol => {
						// We don't want implicit (aka compiler generated) symbols.
						// They often have weird/uncompilable names, such as:
						// * <Numbers>k__BackingField
						return !symbol.IsImplicitlyDeclared;
					})
					.Where(symbol => {
						// No property getters/setters should be explicit.
						if (symbol is IPropertySymbol propertySymbol) {
							if (!propertySymbol.GetMethod?.IsImplicitlyDeclared ?? true)
								return false;

							if (!propertySymbol.SetMethod?.IsImplicitlyDeclared ?? true)
								return false;
						}

						return true;
					})
					.Select(symbol => (Symbol: symbol, Type: GetType(symbol)!))
					.Select(symbolType => new RecordPropertyContext(compilation, symbolType))
					.ToImmutableArray();

				return new CollectionEqualityProvider(symbolName, symbolTypeName, symbolHierarchy, symbolMembersArray, isValueType);
			});

		ctx.RegisterSourceOutput(provider, static (context, sourceProvider) => {
			context.AddSource(sourceProvider.Name, GenerateText(sourceProvider));
		});

		return;

		static string GenerateText(in CollectionEqualityProvider provider) {
			var writer = new IndentedStringBuilder();

			writer.WriteLine(Constants.AutoGeneratedComment);

			writer.WriteLine("#nullable enable");

			using (writer.WriteHierarchyInfo(provider.Hierarchy)) {
				if (!provider.IsValueType)
					writer.WriteLine($"public virtual bool Equals({provider.TypeName}? other)");
				else
					writer.WriteLine($"public readonly bool Equals({provider.TypeName} other)");

				using (writer.WriteBlock()) {
					if (!provider.IsValueType) {
						writer.WriteLine("if (other == null)");
						using (writer.WriteIndent())
							writer.WriteLine("return false;");

						writer.WriteLine("if (this.EqualityContract != other.EqualityContract)");
						using (writer.WriteIndent())
							writer.WriteLine("return false;");
					}

					foreach (var property in provider.RecordProperties)
						switch (property.PropertyType) {
							case RecordPropertyType.Normal:
								WriteNormalPropertyEquals(writer, in property);
								break;
							case RecordPropertyType.IEnumerable:
							case RecordPropertyType.IEnumerable_1:
								WriteIEnumerablePropertyEquals(writer, in property);
								break;
							case RecordPropertyType.ICollection:
								WriteICollectionPropertyEquals(writer, in property);
								break;
							case RecordPropertyType.IReadOnlyCollection_1:
								WriteIReadOnlyCollectionPropertyEquals(writer, in property);
								break;
							case RecordPropertyType.IReadOnlyList_1:
								WriteIReadOnlyListPropertyEquals(writer, in property);
								break;
							case RecordPropertyType.Array:
								WriteArrayPropertyEquals(writer, in property);
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

					writer.WriteLine("return true;");
				}

				writer.WriteLine();

				writer.WriteLine("public override int GetHashCode()");
				using (writer.WriteBlock()) {
					writer.WriteLine("const int favoriteNumber = -1521134295;");
					writer.WriteLine("int hashCode = 0;");

					foreach (var property in provider.RecordProperties)
						switch (property.PropertyType) {
							case RecordPropertyType.Normal:
								WriteNormalPropertyGetHashCode(writer, in property);
								break;
							case RecordPropertyType.IEnumerable:
							case RecordPropertyType.IEnumerable_1:
								WriteIEnumerablePropertyGetHashCode(writer, in property);
								break;
							case RecordPropertyType.ICollection:
								WriteICollectionPropertyGetHashCode(writer, in property);
								break;
							case RecordPropertyType.IReadOnlyCollection_1:
								WriteIReadOnlyCollectionPropertyGetHashCode(writer, in property);
								break;
							case RecordPropertyType.IReadOnlyList_1:
								WriteIReadOnlyListPropertyGetHashCode(writer, in property);
								break;
							case RecordPropertyType.Array:
								WriteArrayPropertyGetHashCode(writer, in property);
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

					writer.WriteLine("return hashCode;");
				}
			}

			return writer.ToString();
		}

		static void WriteNormalPropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{ctx.TypeName}>.Default.Equals(this.{ctx.Name}, other.{ctx.Name}))");
			using (writer.WriteIndent())
				writer.WriteLine("return false;");
		}

		static void WriteIEnumerablePropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != other.{ctx.Name})");
			using (writer.WriteBlock()) {
				writer.WriteLine($"using var left = this.{ctx.Name}.GetEnumerator();");
				writer.WriteLine($"using var right = other.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("var leftMove = left.MoveNext();");
				writer.WriteLine("var rightMove = right.MoveNext();");

				writer.WriteLine("while (leftMove && rightMove)");
				using (writer.WriteBlock()) {
					if (ctx.SpecialInfo is not null) {
						writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.Equals(left.Current, right.Current))");
						using (writer.WriteIndent())
							writer.WriteLine("return false;");
					}
					else {
						writer.WriteLine("if (object.Equals(left.Current, right.Current))");
						using (writer.WriteIndent())
							writer.WriteLine("return false;");
					}

					writer.WriteLine("leftMove = left.MoveNext();");
					writer.WriteLine("rightMove = right.MoveNext();");
				}

				writer.WriteLine("if (leftMove ^ rightMove)");
				using (writer.WriteIndent())
					writer.WriteLine("return false;");
			}
		}

		static void WriteICollectionPropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != other.{ctx.Name})");
			using (writer.WriteBlock()) {
				writer.WriteLine($"if (this.{ctx.Name}.Count != other.{ctx.Name}.Count)");
				using (writer.WriteIndent())
					writer.WriteLine("return false;");

				writer.WriteLine($"var left = this.{ctx.Name}.GetEnumerator();");
				writer.WriteLine($"var right = other.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (left.MoveNext() && right.MoveNext())");
				using (writer.WriteBlock()) {
					writer.WriteLine("if (!object.Equals(left.Current, right.Current))");
					using (writer.WriteIndent())
						writer.WriteLine("return false;");
				}

				writer.WriteLine("(left as global::System.IDisposable)?.Dispose();");
				writer.WriteLine("(right as global::System.IDisposable)?.Dispose();");
			}
		}

		static void WriteIReadOnlyCollectionPropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != other.{ctx.Name})");
			using (writer.WriteBlock()) {
				writer.WriteLine($"if (this.{ctx.Name}.Count != other.{ctx.Name}.Count)");
				using (writer.WriteIndent())
					writer.WriteLine("return false;");

				writer.WriteLine($"using var left = this.{ctx.Name}.GetEnumerator();");
				writer.WriteLine($"using var right = other.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (left.MoveNext() && right.MoveNext())");
				using (writer.WriteBlock()) {
					writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.Equals(left.Current, right.Current))");
					using (writer.WriteIndent())
						writer.WriteLine("return false;");
				}
			}
		}

		static void WriteIReadOnlyListPropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != other.{ctx.Name})");
			using (writer.WriteBlock()) {
				writer.WriteLine($"if (this.{ctx.Name}.Count != other.{ctx.Name}.Count)");
				using (writer.WriteIndent())
					writer.WriteLine("return false;");

				writer.WriteLine($"for (int i = 0, c = this.{ctx.Name}.Count; i < c; i++)");
				using (writer.WriteBlock()) {
					writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.Equals(this.{ctx.Name}[i], other.{ctx.Name}[i]))");
					using (writer.WriteIndent())
						writer.WriteLine("return false;");
				}
			}
		}

		static void WriteArrayPropertyEquals(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != other.{ctx.Name})");
			using (writer.WriteBlock()) {
				writer.WriteLine($"if (this.{ctx.Name}.Length != other.{ctx.Name}.Length)");
				using (writer.WriteIndent())
					writer.WriteLine("return false;");

				writer.WriteLine($"var left = this.{ctx.Name}.GetEnumerator();");
				writer.WriteLine($"var right = other.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (left.MoveNext() && right.MoveNext())");
				using (writer.WriteBlock()) {
					writer.WriteLine("if (!object.Equals(left.Current, right.Current))");
					using (writer.WriteIndent())
						writer.WriteLine("return false;");
				}
			}
		}

		static void WriteNormalPropertyGetHashCode(TextWriter writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"hashCode += global::System.Collections.Generic.EqualityComparer<{ctx.TypeName}>.Default.GetHashCode(this.{ctx.Name}) * favoriteNumber;");
		}

		static void WriteIEnumerablePropertyGetHashCode(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != null)");
			using (writer.WriteBlock()) {
				writer.WriteLine($"using var enumerator = this.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (enumerator.MoveNext())");
				// ReSharper disable once RemoveRedundantBraces
				using (writer.WriteBlock()) {
					// ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
					if (ctx.SpecialInfo is not null)
						writer.WriteLine($"hashCode += global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.GetHashCode(enumerator.Current) * favoriteNumber;");
					else
						writer.WriteLine("hashCode += (enumerator.Current?.GetHashCode() ?? 0) * favoriteNumber;");
				}
			}
		}

		static void WriteICollectionPropertyGetHashCode(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != null)");
			using (writer.WriteBlock()) {
				writer.WriteLine($"var enumerator = this.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (enumerator.MoveNext())");
				// ReSharper disable once RemoveRedundantBraces
				using (writer.WriteBlock()) {
					// ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
					if (ctx.SpecialInfo is not null)
						writer.WriteLine($"hashCode += global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.GetHashCode(enumerator.Current) * favoriteNumber;");
					else
						writer.WriteLine("hashCode += (enumerator.Current?.GetHashCode() ?? 0) * favoriteNumber;");
				}

				writer.WriteLine("(enumerator as global::System.IDisposable)?.Dispose();");
			}
		}

		static void WriteIReadOnlyCollectionPropertyGetHashCode(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != null)");
			using (writer.WriteBlock()) {
				writer.WriteLine($"using var enumerator = this.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (enumerator.MoveNext())");
				using (writer.WriteBlock())
					writer.WriteLine($"hashCode += global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.GetHashCode(enumerator.Current) * favoriteNumber;");
			}
		}

		static void WriteIReadOnlyListPropertyGetHashCode(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != null)");
			using (writer.WriteBlock()) {
				writer.WriteLine($"for (int i = 0, c = this.{ctx.Name}.Count; i < c; i++)");
				using (writer.WriteBlock())
					writer.WriteLine($"hashCode += global::System.Collections.Generic.EqualityComparer<{ctx.SpecialInfo}>.Default.GetHashCode(this.{ctx.Name}[i]) * favoriteNumber;");
			}
		}

		static void WriteArrayPropertyGetHashCode(IndentedStringBuilder writer, in RecordPropertyContext ctx) {
			writer.WriteLine($"if (this.{ctx.Name} != null)");
			using (writer.WriteBlock()) {
				writer.WriteLine($"var enumerator = this.{ctx.Name}.GetEnumerator();");

				writer.WriteLine("while (enumerator.MoveNext())");
				using (writer.WriteBlock())
					writer.WriteLine("hashCode += (enumerator.Current?.GetHashCode() ?? 0) * favoriteNumber;");
			}
		}
	}

	private static ITypeSymbol? GetType(ISymbol symbol) {
		return symbol switch {
			IFieldSymbol fieldSymbol => fieldSymbol.Type,
			IPropertySymbol propertySymbol => propertySymbol.Type,
			_ => null
		};
	}
}

file readonly record struct CollectionEqualityProvider(
	in string Name,
	in string TypeName,
	in HierarchyInfo Hierarchy,
	in EquatableArray<RecordPropertyContext> RecordProperties,
	in bool IsValueType);

[SuppressMessage("ReSharper", "InconsistentNaming")]
file enum RecordPropertyType {
	Normal,
	/// <summary>
	/// <see cref="System.Collections.IEnumerable"/>
	/// </summary>
	IEnumerable,
	/// <summary>
	/// <see cref="System.Collections.Generic.IEnumerable{T}"/>
	/// </summary>
	IEnumerable_1,
	/// <summary>
	/// <see cref="System.Collections.ICollection"/>
	/// </summary>
	ICollection,
	/// <summary>
	/// <see cref="System.Collections.Generic.IReadOnlyCollection{T}"/>
	/// </summary>
	IReadOnlyCollection_1,
	/// <summary>
	/// <see cref="System.Collections.Generic.IReadOnlyList{T}"/>
	/// </summary>
	IReadOnlyList_1,
	/// <summary>
	/// <see cref="System.Array"/>
	/// </summary>
	Array,
}

file readonly record struct RecordPropertyContext {
	public string Name { get; }
	public string TypeName { get; }
	public RecordPropertyType PropertyType { get; }
	public object? SpecialInfo { get; }

	public RecordPropertyContext(Compilation compilation, (ISymbol, ITypeSymbol) symbol) {
		Name = symbol.Item1.Name;
		TypeName = symbol.Item2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		PropertyType = GetPropertyType(compilation, symbol, out object? specialInfo);
		SpecialInfo = specialInfo;
	}

	private static RecordPropertyType GetPropertyType(Compilation compilation, (ISymbol, ITypeSymbol) symbol, out object? specialInfo) {
		var enumerableSymbol = compilation.GetTypeByMetadataName("System.Collections.IEnumerable");
		var enumerable1Symbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
		var collectionSymbol = compilation.GetTypeByMetadataName("System.Collections.ICollection");
		var readOnlyCollection1Symbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyCollection`1");
		var readOnlyList1Symbol = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
		var arraySymbol = compilation.GetTypeByMetadataName("System.Array");

		specialInfo = null;

		var firstType = symbol.Item2 is IArrayTypeSymbol arrayTypeSymbol ? arrayTypeSymbol.ElementType : symbol.Item2;
		if (firstType is not INamedTypeSymbol)
			return RecordPropertyType.Normal;

		for (var baseTypeSymbol = symbol.Item2; baseTypeSymbol != null; baseTypeSymbol = baseTypeSymbol.BaseType) {
			var originalDefinitionSymbol = baseTypeSymbol.OriginalDefinition;

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, arraySymbol))
				return RecordPropertyType.Array;
		}

		// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
		foreach (var interfaceSymbol in symbol.Item2.AllInterfaces.Prepend((INamedTypeSymbol)firstType)) {
			var originalDefinitionSymbol = interfaceSymbol.OriginalDefinition;

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, readOnlyList1Symbol)) {
				specialInfo = interfaceSymbol.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				return RecordPropertyType.IReadOnlyList_1;
			}

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, readOnlyCollection1Symbol)) {
				specialInfo = interfaceSymbol.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				return RecordPropertyType.IReadOnlyCollection_1;
			}

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, collectionSymbol))
				return RecordPropertyType.ICollection;

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, enumerable1Symbol)) {
				specialInfo = interfaceSymbol.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				return RecordPropertyType.IEnumerable_1;
			}

			if (SymbolEqualityComparer.Default.Equals(originalDefinitionSymbol, enumerableSymbol))
				return RecordPropertyType.IEnumerable;
		}

		return RecordPropertyType.Normal;
	}
}
