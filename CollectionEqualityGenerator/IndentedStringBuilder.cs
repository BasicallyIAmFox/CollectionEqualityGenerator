// Copyright 2023-2024 BasicallyIAmFox
// 
//    Licensed under the CLA (the "License")
//    See LICENSE.txt for more info.

using CollectionEqualityGenerator.DataStructures;
using System.CodeDom.Compiler;
using System.IO;

namespace CollectionEqualityGenerator;

internal sealed class IndentedStringBuilder : IndentedTextWriter {
	public IndentedStringBuilder() : base(new StringWriter(), "\t") {
	}

	public override string ToString() {
		var stringWriter = (StringWriter)InnerWriter;
		var stringBuilder = stringWriter.GetStringBuilder();
		stringWriter.Close();
		return stringBuilder.ToString();
	}

	public HierarchyInfo.WriteObject WriteHierarchyInfo(HierarchyInfo value) {
		return new HierarchyInfo.WriteObject(this, value);
	}
}
