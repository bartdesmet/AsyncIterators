//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type used to refer to a builder type for an asynchronous iterator-like type.
    /// See <see cref="AsyncIteratorBuilder{T}"/> for the requirements imposed on such a type.
    /// </summary>
    /// <remarks>Only classes and interfaces are supported, because code generation depends on inheritance.</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class AsyncIteratorBuilderTypeAttribute : Attribute
    {
        /// <summary>
        /// Creates a new attribute instance.
        /// </summary>
        /// <param name="type">The builder type for the asynchronous iterator-like type.</param>
        public AsyncIteratorBuilderTypeAttribute(Type type)
        {
            Type = type;
        }

        /// <summary>
        /// Gets the builder type for the asynchronous iterator-like type.
        /// </summary>
        public Type Type { get; }
    }
}
