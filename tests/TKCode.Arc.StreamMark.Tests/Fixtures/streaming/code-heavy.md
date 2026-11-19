Here is the implementation:

```python
def fib(n):
    a, b = 0, 1

    for _ in range(n):
        a, b = b, a + b

    return a
```

And a shell session:

~~~bash
$ dotnet build
Build succeeded.
~~~

    indented code
    still code

Nested fence inside a list:

- step one

  ```js
  const x = 1;

  console.log(x);
  ```

- step two

Done.
