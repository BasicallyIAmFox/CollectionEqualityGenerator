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
	IEnumerable<int> Numbers4);

[CollectionEquality]
partial record Foo(List<int> Numbers);

internal class Program
{
	public static void Main(string[] args) {
		var left = new Foo([1, 2, 3]);
		var right = new Foo([1, 2, 3]);
		
		Console.WriteLine(left.Equals(right));
	}
}
