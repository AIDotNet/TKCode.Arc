using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>An immutable array with value equality, so incremental pipeline steps cache.</summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _items;

        public EquatableArray(ImmutableArray<T> items)
        {
            _items = items;
        }

        public EquatableArray(IEnumerable<T> items)
        {
            _items = items.ToImmutableArray();
        }

        public static readonly EquatableArray<T> Empty = new EquatableArray<T>(ImmutableArray<T>.Empty);

        public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

        public int Count => Items.Length;

        public T this[int index] => Items[index];

        public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var hash = 17;
            foreach (var item in Items)
            {
                hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
            }
            return hash;
        }

        public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();
    }

    /// <summary>A diagnostic that can live in a cached pipeline model (no Location or symbol references).</summary>
    internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
    {
        public DiagnosticInfo(DiagnosticDescriptor descriptor, string? filePath, TextSpan span, LinePositionSpan lineSpan, params string[] arguments)
        {
            Descriptor = descriptor;
            FilePath = filePath;
            Span = span;
            LineSpan = lineSpan;
            Arguments = arguments;
        }

        public DiagnosticDescriptor Descriptor { get; }
        public string? FilePath { get; }
        public TextSpan Span { get; }
        public LinePositionSpan LineSpan { get; }
        public string[] Arguments { get; }

        public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
        {
            if (location is null || location.SourceTree is null)
            {
                return new DiagnosticInfo(descriptor, null, default, default, arguments);
            }
            var line = location.GetLineSpan();
            return new DiagnosticInfo(descriptor, line.Path, location.SourceSpan, line.Span, arguments);
        }

        public Diagnostic ToDiagnostic()
        {
            var location = FilePath is null ? Location.None : Location.Create(FilePath, Span, LineSpan);
            return Diagnostic.Create(Descriptor, location, Arguments);
        }

        public bool Equals(DiagnosticInfo? other)
            => other is not null
               && Descriptor.Id == other.Descriptor.Id
               && FilePath == other.FilePath
               && Span.Equals(other.Span)
               && Arguments.SequenceEqual(other.Arguments);

        public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);

        public override int GetHashCode() => unchecked(Descriptor.Id.GetHashCode() * 31 + Span.GetHashCode());
    }
}
