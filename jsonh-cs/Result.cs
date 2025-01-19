using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ValueResults;

/// <summary>
/// Success or an error.
/// </summary>
public interface IResult {
    /// <summary>
    /// Returns the error that occurred or <see langword="default"/>(<see cref="Error"/>).
    /// </summary>
    public Error ErrorOrDefault { get; }
    /// <summary>
    /// Returns the error that occurred or throws an exception.
    /// </summary>
    public Error Error { get; }
    /// <summary>
    /// Returns <see langword="true"/> if an error occurred.
    /// </summary>
    public bool IsError { get; }
    /// <summary>
    /// Returns <see langword="true"/> if an error occurred and provides the error or <see langword="default"/>(<see cref="Error"/>).
    /// </summary>
    public bool TryGetError([NotNullWhen(true)] out Error Error);
}

/// <summary>
/// A value or an error.
/// </summary>
public interface IResult<T> : IResult {
    /// <summary>
    /// Returns the value or <see langword="default"/>(<typeparamref name="T"/>).
    /// </summary>
    public T? ValueOrDefault { get; }
    /// <summary>
    /// Returns the value or throws an exception.
    /// </summary>
    public T Value { get; }
    /// <summary>
    /// Returns <see langword="true"/> if a value was successfully returned.
    /// </summary>
    public bool IsValue { get; }
    /// <summary>
    /// Transforms the value using a mapping function if a value was successfully returned or returns the error.
    /// </summary>
    public IResult<TNew> Try<TNew>(Func<T, TNew> Map);
    /// <summary>
    /// Returns <see langword="true"/> if a value was successfully returned and provides the value or <see langword="default"/>(<typeparamref name="T"/>)
    /// and the error or <see langword="default"/>(<see cref="Error"/>).
    /// </summary>
    public bool TryGetValue([NotNullWhen(true)] out T? Value, [NotNullWhen(false)] out Error Error);
    /// <summary>
    /// Returns <see langword="true"/> if a value was successfully returned and provides the value or <see langword="default"/>(<typeparamref name="T"/>).
    /// </summary>
    public bool TryGetValue([NotNullWhen(true)] out T? Value);
    /// <summary>
    /// Returns <see langword="true"/> if a value was successfully returned and is equal to <paramref name="Other"/>.
    /// </summary>
    public bool ValueEquals(T? Other);
}

/// <summary>
/// Success or an error.
/// </summary>
public readonly struct Result : IResult {
    /// <summary>
    /// A successful result.
    /// </summary>
    public static Result Success { get; } = new();

    /// <inheritdoc/>
    public Error ErrorOrDefault { get; }
    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(ErrorOrDefault))]
    public bool IsError { get; }

    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    public Result() {
        IsError = false;
    }
    /// <summary>
    /// Constructs a failed result.
    /// </summary>
    public Result(Error Error) {
        ErrorOrDefault = Error;
        IsError = true;
    }
    /// <summary>
    /// Constructs a failed result.
    /// </summary>
    public Result(string? ErrorMessage)
        : this(new Error(ErrorMessage)) {
    }

    /// <summary>
    /// Returns the error that occurred or throws an exception.
    /// </summary>
    public Error Error => IsError ? ErrorOrDefault : throw new InvalidOperationException("Result was value");

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    public override string ToString() {
        if (IsError) {
            return $"Error: {ErrorOrDefault.Message}";
        }
        else {
            return "Success";
        }
    }
    /// <inheritdoc/>
    public bool TryGetError([NotNullWhen(false)] out Error Error) {
        Error = ErrorOrDefault;
        return IsError;
    }

    /// <summary>
    /// Creates a successful result or a failed result an error.
    /// </summary>
    public static implicit operator Result(Error? Error) {
        return Error is not null ? new Result(Error.Value) : Success;
    }
}

/// <summary>
/// A value or an error.
/// </summary>
public readonly struct Result<T> : IResult<T> {
    /// <inheritdoc/>
    public T? ValueOrDefault { get; }
    /// <inheritdoc/>
    public Error ErrorOrDefault { get; }
    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(ErrorOrDefault))]
    public bool IsError { get; }

    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    public Result(T Value) {
        ValueOrDefault = Value;
        IsError = false;
    }
    /// <summary>
    /// Constructs a failed result.
    /// </summary>
    public Result(Error Error) {
        ErrorOrDefault = Error;
        IsError = true;
    }
    /// <summary>
    /// Constructs a failed result.
    /// </summary>
    public Result(string? ErrorMessage)
        : this(new Error(ErrorMessage)) {
    }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(ValueOrDefault))]
    public bool IsValue => !IsError;
    /// <inheritdoc/>
    public T Value => IsValue ? ValueOrDefault : throw new InvalidOperationException($"Result was error: \"{Error.Message}\"");
    /// <inheritdoc/>
    public Error Error => IsError ? ErrorOrDefault : throw new InvalidOperationException("Result was value");

    /// <inheritdoc/>
    public override string ToString() {
        if (IsError) {
            return $"Error: {Error.Message}";
        }
        else {
            return $"Success: {Value}";
        }
    }
    /// <inheritdoc/>
    public Result<TNew> Try<TNew>(Func<T, TNew> Map) {
        return IsValue ? Map(Value) : Error;
    }
    /// <inheritdoc/>
    public bool TryGetError([NotNullWhen(false)] out Error Error) {
        Error = ErrorOrDefault;
        return IsError;
    }
    /// <inheritdoc/>
    public bool TryGetValue([NotNullWhen(true)] out T? Value, [NotNullWhen(false)] out Error Error) {
        Value = ValueOrDefault;
        Error = ErrorOrDefault;
        return IsValue;
    }
    /// <inheritdoc/>
    public bool TryGetValue([NotNullWhen(true)] out T? Value) {
        Value = ValueOrDefault;
        return IsValue;
    }
    /// <inheritdoc/>
    public bool ValueEquals(T? Other) {
        return IsValue && Equals(Value, Other);
    }

    /// <summary>
    /// Creates a successful result from a value.
    /// </summary>
    public static implicit operator Result<T>(T Value) {
        return new Result<T>(Value);
    }
    /// <summary>
    /// Creates a failed result from an error.
    /// </summary>
    public static implicit operator Result<T>(Error Error) {
        return new Result<T>(Error);
    }

    IResult<TNew> IResult<T>.Try<TNew>(Func<T, TNew> Map) => Try(Map);
}

/// <summary>
/// An error that occurred.
/// </summary>
public readonly struct Error {
    /// <summary>
    /// The optional custom error code (or 0).
    /// </summary>
    public long Code { get; }
    /// <summary>
    /// The error message for debugging purposes.
    /// </summary>
    public object? Message { get; }

    /// <summary>
    /// Constructs an error that occurred with a custom error code.
    /// </summary>
    public Error(long Code, object? Message) {
        this.Code = Code;
        this.Message = Message;
    }
    /// <summary>
    /// Constructs an error that occurred.
    /// </summary>
    public Error(object? Message)
        : this(0, Message) {
    }

    /// <summary>
    /// Returns the custom error code as an enum.
    /// </summary>
    public T GetCode<T>() where T : struct {
        return Unsafe.BitCast<long, T>(Code);
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    public override string ToString() {
        return $"Error: \"{Message}\"";
    }
}