//
// Prototype of async iterators using WaitForNextAsync/TryGetNext.
//
// bartde - February 2018
//


namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Type describing the shape of a base class that can be used for compilers to emit asynchronous
    /// iterators for async iterable-like types.
    /// </summary>
    /// <typeparam name="T">The type of the elements returned by the iterator.</typeparam>
    /// <typeparam name="TIterator">The type of the derived iterator type.</typeparam>
    /// <remarks>
    /// Asynchronous iterable-like types can be returned from asynchronous iterator methods, e.g.
    /// <code>
    /// async IAsyncIterable{int} FooAsync()
    /// {
    ///     await Task.Yield();
    ///     yield return 42;
    /// }
    /// </code>
    /// The C# compiler classifies a method as an asynchronous iterator method if it has an <c>async</c>
    /// modifier and has one or more <c>yield</c> statements, to disambiguate it from an <c>async</c>
    /// method which may have a task-like return type.
    /// 
    /// Next, it checks the return type of the method and determines if it's an asynchronous iterator-like
    /// type by checking for the presence of the <see cref="AsyncIteratorBuilderTypeAttribute"/> attribute
    /// whose <see cref="AsyncIteratorBuilderTypeAttribute.Type"/> property returns the builder type.
    /// 
    /// Given this builder type, checks are made to assert the shape of the type, according to the shape
    /// described by <see cref="AsyncIteratorBuilder{T}"/>. If the type has all of the required members,
    /// the return type of <see cref="AsyncIteratorBuilder{T}.TryMoveNext"/> is treated as the element
    /// type. If this type is a generic parameter type, no further checks are performed. Otherwise, the
    /// operand of all the <c>yield return</c> statements should be implicitly convertible to this type. If
    /// the element type is a generic parameter type, the builder type is asserted to have a single generic
    /// type parameter. Otherwise, the builder type is asserted to be non-generic.
    /// 
    /// Note that the <see cref="AsyncIteratorBuilder{T}.AwaitOnCompleted"/> methods are marked optional.
    /// If these methods do not exist, the compiler asserts that the async iterator method does not have
    /// any <c>await</c> expressions inside its body.
    /// 
    /// Warnings about the lack of <c>await</c> expressions are not emitted if the async iterator builder
    /// type supports asynchronous execution (see <see cref="AsyncIteratorBuilder{T}.AwaitOnCompleted"/>)
    /// and if the async iterator method's return type is not also classified as a synchronous iterator-
    /// like type, which would allow the user to fix the warning by omitting the <c>async</c> modifier.
    /// In other cases, no obvious "fix" for such a warning is possible, short of getting rid of all
    /// <c>yield</c> statements and building a state machine by hand.
    /// 
    /// After checking for an asynchronous iterator-like type, as described above, the compiler checks
    /// for the presence of the <see cref="AsyncIterableBuilderTypeAttribute"/> attribute. Note this
    /// happens in case the checks for an asynchronous iterator-like type succeeded or failed. In the
    /// former case, the compiler seeks to refine the classification of the asynchronous iterator to
    /// an asynchronous iterable. In the latter case, this check acts as a fallback. Note that the shape
    /// for an asynchronous iterable-type is a superset of an asynchronous iterator-like type, modeled
    /// here using an inheritance relationship on <see cref="AsyncIterableBuilder{T, TIterator}"/>. This
    /// process, if successful, will derive the element type, as described above. If the element type is
    /// a generic parameter, the builder type is asserted to have two generic type parameters; one is
    /// used for the return type of <see cref="AsyncIterableBuilder{T, TIterator}.Clone"/>, and the other
    /// one is used for the return type. Otherwise, one generic type parameter is expected.
    /// 
    /// If the return type is not classified as an asynchronous iterable- or iterator-like type, an error
    /// is raised, suggesting that the use of <c>yield</c> statements is invalid because the method does
    /// not return a compatible type. Errors about missing custom attributes and/or required members on
    /// builder types are produced.
    /// 
    /// If the return type is classified as an asynchronous iterable-like type, the compiler generates a
    /// derived class from the builder type, by closing any generic type parameters over the derived type
    /// and the element type (if it was found to be an open generic parameter). For example:
    /// <code>
    /// sealed class FooAsyncIterable : AsyncIterableBuilder{int, FooAsyncIterable}
    /// {
    ///     ...
    /// }
    /// </code>
    /// 
    /// If the return type is classified as an asynchronous iterator-like type, the compiler generates a
    /// derived class from the builder type, which may be generic. If it's a generic type, its sole type
    /// parameter is closed over the element type. For example:
    /// <code>
    /// sealed class FooAsyncIterator : AsyncIteratorBuilder{int}
    /// {
    ///     ...
    /// }
    /// </code>
    /// 
    /// Note that resulting iterable or iterator types are always sealed non-generic types. The compiler
    /// then proceeds to implement the abstract members on these types.
    /// </remarks>
    public abstract class AsyncIterableBuilder<T, TIterator> : AsyncIteratorBuilder<T>
    {
        /// <summary>
        /// Generated code for the iterator should override this method to create a clone of the
        /// iterator instance.
        /// </summary>
        /// <returns>A clone of the iterator in an initialized but non-started state.</returns>
        protected abstract TIterator Clone();
    }
}
