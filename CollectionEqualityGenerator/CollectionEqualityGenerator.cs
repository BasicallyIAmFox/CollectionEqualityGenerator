﻿using CollectionEqualityGenerator.DataStructures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
			    
			    var symbolMembersArray = symbol.GetMembers()
				    .Where(symbol => symbol is IFieldSymbol or IPropertySymbol)
				    .Where(symbol => {
					    // We don't want implicit (aka compiler generated) symbols.
					    // They often have weird/uncompilable names, such as:
					    // * <Numbers>k__BackingField
					    return !symbol.IsImplicitlyDeclared;
				    })
				    .Select(symbol => (Symbol: symbol, Type: GetType(symbol)!))
				    .Select(symbolType => new RecordPropertyContext(compilation, symbolType))
				    .ToImmutableArray();
			    
			    return new CollectionEqualityProvider(symbolName, symbolTypeName, symbolHierarchy, symbolMembersArray);
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
			    writer.WriteLine($"public virtual bool Equals({provider.TypeName}? other) {{");
			    writer.Indent++;
			    
			    writer.WriteLine("if (other == null) return false;");

			    // This is horrible.
			    foreach (var property in provider.RecordProperties) {
				    switch (property.PropertyType) {
					    // Use default equality comparer
					    case RecordPropertyType.Normal:
						    writer.Write("if (!");
						    writer.Write($"global::System.Collections.Generic.EqualityComparer<{property.TypeName}>.Default.Equals(this.{property.Name}, other.{property.Name})");
						    writer.WriteLine(") return false;");
						    break;
					    /*
							var left = leftEnumerable.GetEnumerator();
						    var right = rightEnumerable.GetEnumerator();
						    
						    var leftMove = left.MoveNext();
						    var rightMove = right.MoveNext();
						    
						    while (leftMove && rightMove) {
						        if (!EqualityComparer<T>.Default.Equals(left.Current, right.Current))
						            return false;
						        
						        leftMove = left.MoveNext();
						        rightMove = right.MoveNext();
						    }
						    
						    if (leftMove ^ rightMove)
						        return false;
					     */
					    case RecordPropertyType.IEnumerable:
					    case RecordPropertyType.IEnumerable_1:
						    writer.WriteLine('{');
						    writer.Indent++;
						    
						    writer.WriteLine($"using var left = this.{property.Name}.GetEnumerator();");
						    writer.WriteLine($"using var right = this.{property.Name}.GetEnumerator();");
						    writer.WriteLine("var leftMove = left.MoveNext();");
						    writer.WriteLine("var rightMove = left.MoveNext();");
						    
						    writer.WriteLine("while (leftMove && rightMove) {");
						    writer.Indent++;

						    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
						    if (property.PropertyType is RecordPropertyType.IEnumerable_1) {
							    // Use default comparer
							    writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{property.SpecialInfo}>.Default.Equals(left.Current, right.Current)) return false;");
						    }
						    else {
							    // Cry and hope
							    writer.WriteLine("if (object.Equals(left.Current, right.Current)) return false;");
						    }

						    writer.WriteLine("leftMove = left.MoveNext();");
						    writer.WriteLine("rightMove = right.MoveNext();");

						    writer.Indent--;
						    writer.WriteLine('}');
						    
						    writer.WriteLine("if (leftMove ^ rightMove) return false;");

						    writer.Indent--;
						    writer.WriteLine('}');
						    break;
					    /*
							if (leftCollection.Count != rightCollection.Count)
								return false;
							
							var left = leftCollection.GetEnumerator();
						    var right = rightCollection.GetEnumerator();
						    
						    while (left.MoveNext() && right.MoveNext()) {
						        if (!EqualityComparer<T>.Default.Equals(left.Current, right.Current))
						            return false;
						    }
					     */
					    case RecordPropertyType.ICollection:
					    case RecordPropertyType.IReadOnlyCollection_1:
						    writer.WriteLine($"if (this.{property.Name}.Count != other.{property.Name}.Count) return false;");
						    
						    writer.WriteLine('{');
						    writer.Indent++;

						    if (property.PropertyType is RecordPropertyType.IEnumerable_1) {
							    writer.WriteLine($"using var left = this.{property.Name}.GetEnumerator();");
							    writer.WriteLine($"using var right = other.{property.Name}.GetEnumerator();");
						    }
						    else { // IEnumerable, for some reason, doesn't implements IDisposable
							    writer.WriteLine($"var left = this.{property.Name}.GetEnumerator();");
							    writer.WriteLine($"var right = other.{property.Name}.GetEnumerator();");
						    }

						    writer.WriteLine("while (left.MoveNext() && right.MoveNext()) {");
						    writer.Indent++;
						    
						    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
						    if (property.PropertyType is RecordPropertyType.IEnumerable_1) {
							    // Use default comparer
							    writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{property.SpecialInfo}>.Default.Equals(left.Current, right.Current)) return false;");
						    }
						    else {
							    // Cry and hope
							    writer.WriteLine("if (object.Equals(left.Current, right.Current)) return false;");
						    }
						    writer.Indent--;
						    writer.WriteLine('}');

						    writer.Indent--;
						    writer.WriteLine('}');
						    break;
					    /*
						    if (left.Count != right.Count) return false;
						    {
								for (int i = 0, c = left.Count; i < c; i++) {
									if (!EqualityComparer<{property.SpecialInfo}>.Default.Equals(left[i], right[i])) return false;
								}
						    }
						*/
					    case RecordPropertyType.IReadOnlyList_1:
						    writer.WriteLine($"if (this.{property.Name}.Count != other.{property.Name}.Count) return false;");
						    
						    writer.WriteLine('{');
						    writer.Indent++;
						    
						    writer.WriteLine($"for (int i = 0, c = this.{property.Name}.Count; i < c; i++) {{");
						    writer.Indent++;
						    
						    writer.WriteLine($"if (!global::System.Collections.Generic.EqualityComparer<{property.SpecialInfo}>.Default.Equals(this.{property.Name}[i], other.{property.Name}[i])) return false;");

						    writer.Indent--;
						    writer.WriteLine('}');
						    
						    writer.Indent--;
						    writer.WriteLine('}');
						    break;
					    default:
						    throw new ArgumentOutOfRangeException();
				    }
			    }
			    
			    writer.WriteLine("return true;");
			    
			    writer.Indent--;
			    writer.WriteLine('}');
		    }
		    
		    return writer.ToString();
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

file readonly record struct CollectionEqualityProvider(in string Name, in string TypeName, in HierarchyInfo Hierarchy, in EquatableArray<RecordPropertyContext> RecordProperties);

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

		specialInfo = null;

		if (symbol.Item2 is IErrorTypeSymbol)
			return RecordPropertyType.Normal;

		// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
		foreach (var interfaceSymbol in symbol.Item2.AllInterfaces.Prepend((INamedTypeSymbol)symbol.Item2)) {
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
