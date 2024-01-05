// Copyright 2023-2024 BasicallyIAmFox
// 
//    Licensed under the CLA (the "License")
//    See LICENSE.txt for more info.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CollectionEqualityGenerator.DataStructures;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public readonly struct EquatableArray<T>
	: IEquatable<EquatableArray<T>>, IEnumerable<T>
	where T : IEquatable<T> {
	private readonly T[]? _array;
	
	public bool IsEmpty {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => AsImmutableArray().IsEmpty;
	}
	
	public EquatableArray(ImmutableArray<T> array) {
		_array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);
	}
	
	public ref readonly T this[int index] {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => ref AsImmutableArray().ItemRef(index);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ImmutableArray<T> AsImmutableArray() {
		return Unsafe.As<T[]?, ImmutableArray<T>>(ref Unsafe.AsRef(in _array));
	}
	
	public ReadOnlySpan<T> AsSpan() {
		return AsImmutableArray().AsSpan();
	}
	
	public T[] ToArray() {
		return AsImmutableArray().ToArray();
	}
	
	public override bool Equals(object? obj) {
		return obj is EquatableArray<T> array && Equals(this, array);
	}
	
	public bool Equals(EquatableArray<T> other) {
		return AsSpan().SequenceEqual(_array.AsSpan());
	}

	public override int GetHashCode() {
		if (_array is null)
			return 0;

		var hashCode = default(HashCode);

		foreach (var item in _array)
			hashCode.Add(item);

		return hashCode.ToHashCode();
	}
	
	public ImmutableArray<T>.Enumerator GetEnumerator() {
		return AsImmutableArray().GetEnumerator();
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator() {
		return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return ((IEnumerable)AsImmutableArray()).GetEnumerator();
	}
	
	public static implicit operator EquatableArray<T>(ImmutableArray<T> array) {
		return new EquatableArray<T>(array);
	}
	
	public static implicit operator ImmutableArray<T>(EquatableArray<T> array) {
		return array.AsImmutableArray();
	}
	
	public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) {
		return left.Equals(right);
	}

	public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) {
		return !(left == right);
	}
}
