//
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
