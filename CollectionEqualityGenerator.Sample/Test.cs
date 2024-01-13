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

using CollectionEquality;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CollectionEqualityGenerator.Sample;

[CollectionEquality]
partial record LotOfLists(
	List<int> Numbers1,
	IReadOnlyList<int> Numbers2,
	IReadOnlyCollection<int> Numbers3,
	ICollection Something,
	IEnumerable<int> Numbers4,
	int Singleton,
	int[] Array) {
	private IEnumerable<char> notIgnored;

	public IEnumerable<char> Ignored => notIgnored;
}

[CollectionEquality]
partial record Foo(List<int> Numbers) {
	public const int ConstantValue = 5;
	public static int StaticValue = 5;
}

[CollectionEquality]
partial record struct LotOfListsStruct(
	List<int> Numbers1,
	IReadOnlyList<int> Numbers2,
	IReadOnlyCollection<int> Numbers3,
	ICollection Something,
	IEnumerable<int> Numbers4,
	int Singleton,
	int[] Array) {
	private IEnumerable<char> notIgnored;

	public IEnumerable<char> Ignored => notIgnored;
}

[CollectionEquality]
partial record struct FooStruct(List<int> Numbers);

internal class Program {
	public static void Main() {
		var left = new Foo(new() { 1, 2, 3 });
		var right = new Foo(new() { 1, 2, 3 });

		Console.WriteLine(left.Equals(right));
	}
}
