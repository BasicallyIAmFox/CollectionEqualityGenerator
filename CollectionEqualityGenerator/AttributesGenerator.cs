﻿using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace CollectionEqualityGenerator;

[Generator]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class AttributesGenerator : IIncrementalGenerator {
	public const string Namespace = "CollectionEquality";

	public const string CollectionEqualityAttributeName = "CollectionEqualityAttribute";

	public const string CollectionEqualityAttributeMetadataName = "CollectionEqualityAttribute";
	
	public const string CollectionEqualityAttributeQualifiedName = $"{Namespace}.{CollectionEqualityAttributeName}";
	
	public const string CollectionEqualityAttributeQualifiedMetadataName = $"{Namespace}.{CollectionEqualityAttributeMetadataName}";

	public const string CollectionEqualityAttributeFullyQualifiedMetadataName = CollectionEqualityAttributeQualifiedMetadataName;

	private const string CollectionEqualityAttributeTemplate =
		$$"""
		  {{Constants.AutoGeneratedComment}}
		  namespace {{Namespace}}
		  {
		    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
		    internal sealed class {{CollectionEqualityAttributeName}} : global::System.Attribute
		    {
		    }
		  }
		  """;
	
    public void Initialize(IncrementalGeneratorInitializationContext ctx) {
	    ctx.RegisterPostInitializationOutput(static ctx => {
		    ctx.AddSource($"{CollectionEqualityAttributeFullyQualifiedMetadataName}.g", CollectionEqualityAttributeTemplate);
	    });
    }
}
