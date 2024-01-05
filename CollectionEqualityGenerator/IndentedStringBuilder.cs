// Copyright 2023-2024 BasicallyIAmFox
// 
//    Licensed under the CLA (the "License")
//    See LICENSE.txt for more info.

using CollectionEqualityGenerator.DataStructures;
using System;
using System.CodeDom.Compiler;
using System.IO;

namespace CollectionEqualityGenerator;

internal sealed class IndentedStringBuilder : IndentedTextWriter {
	public readonly struct Block : IDisposable {
		private readonly IndentedTextWriter _writer;

		public Block(IndentedTextWriter writer) {
			_writer = writer;
		
			_writer.WriteLine('{');
			_writer.Indent++;
		}

		public void Dispose() {
			_writer.Indent--;
			_writer.WriteLine('}');
		}
	}
	public readonly struct BlockIndentOnly : IDisposable {
		private readonly IndentedTextWriter _writer;

		public BlockIndentOnly(IndentedTextWriter writer) {
			_writer = writer;
			
			_writer.Indent++;
		}

		public void Dispose() {
			_writer.Indent--;
		}
	}

	public IndentedStringBuilder() : base(new StringWriter(), "\t") {
	}

	public override string ToString() {
		var stringWriter = (StringWriter)InnerWriter;
		var stringBuilder = stringWriter.GetStringBuilder();
		stringWriter.Close();
		return stringBuilder.ToString();
	}

	public Block WriteBlock() {
		return new Block(this);
	}
	
	public BlockIndentOnly WriteIndent() {
		return new BlockIndentOnly(this);
	}

	public HierarchyInfo.WriteObject WriteHierarchyInfo(HierarchyInfo value) {
		return new HierarchyInfo.WriteObject(this, value);
	}
}
