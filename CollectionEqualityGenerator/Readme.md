hi

```
[CollectionEquality]
partial record R(IReadOnlyCollection<int> Numbers);

var a = new R([1, 2, 3]);
var b = new R([1, 2, 3]);
Assert.Equal(a, b);     // 'true'
```