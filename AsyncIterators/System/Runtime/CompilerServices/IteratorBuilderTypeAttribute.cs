//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type used to refer to a builder type for a synchronous iterator-like type.
    /// See <see cref="IteratorBuilder{T}"/> for the requirements imposed on such a type.
    /// </summary>
    /// <remarks>Only classes are supported, because code generation depends on inheritance.</remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class IteratorBuilderTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets the builder type for the synchronous iterator-like type.
        /// </summary>
        public Type Type { get; }
    }
}
