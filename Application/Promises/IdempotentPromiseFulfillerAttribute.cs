using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Rtl.News.RtlPoc.Application.Promises;

/// <summary>
/// <para>
/// Indicates that the annotated method is used to fulfill a promise with the given <paramref name="actionName"/>.
/// </para>
/// <para>
/// The annotated method should be idempotent, but not necessarily re-entrant.
/// This allows it to simply check that the work was not already done, and then execute it, without atomicity between check and execution.
/// </para>
/// <para>
/// Additionally, if <see cref="Promise.IsFirstAttempt"/>, the method can skip the idempotency check.
/// </para>
/// </summary>
/// <param name="actionName">A unique, stable name for the action fulfilling the promise. <strong>Must not be changed once any promises are stored.</strong></param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IdempotentPromiseFulfillerAttribute(
    string actionName)
    : Attribute
{
    /// <summary>
    /// Contains a delegate to each annotated method, indexed by the unique <see cref="ActionName"/>.
    /// </summary>
    private static FrozenDictionary<string, Func<IServiceProvider, Promise, CancellationToken, Task>> DelegatesByActionName { get; } =
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.FullName?.StartsWith("Rtl.News.RtlPoc.") == true)
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => KeyValuePair.Create(method, method.GetCustomAttribute<IdempotentPromiseFulfillerAttribute>()))
            .Where(pair => pair.Value is not null)
            .ToFrozenDictionary(pair => pair.Value!.ActionName, pair => AsDelegateOrThrow(pair.Key, pair.Value!.ActionName));

    /// <summary>
    /// <para>
    /// A unique, stable name for the action fulfilling the promise.
    /// </para>
    /// <para>
    /// <strong>Must not be changed once any promises are stored.</strong>
    /// </para>
    /// </summary>
    public string ActionName { get; } = actionName ?? throw new ArgumentNullException(nameof(actionName));

    /// <summary>
    /// Turns the given <paramref name="method"/> into a delegate that invokes it, or throws if it does not precisely match the expectations.
    /// </summary>
    private static Func<IServiceProvider, Promise, CancellationToken, Task> AsDelegateOrThrow(MethodInfo method, string actionName)
    {
        if (method.IsStatic ||
            method.IsAbstract ||
            method.IsGenericMethod ||
            method.ReflectedType?.IsClass != true ||
            method.ReturnType != typeof(Task) ||
            method.GetParameters() is not ParameterInfo[] parameters ||
            parameters.Length != 2 ||
            parameters[0].ParameterType != typeof(Promise) ||
            parameters[1].ParameterType != typeof(CancellationToken))
        {
            throw new MissingMethodException(
                $@"[IdempotentPromiseFulfiller(""{actionName}"")] must annotate a non-static, non-abstract, non-generic class instance method that satisfies Func<Promise, CancellationToken, Task>.");
        }

        var methodInvoker = MethodInvoker.Create(method);
        return (IServiceProvider serviceProvider, Promise promise, CancellationToken cancellationToken) => ActivateAndInvokeAsync(serviceProvider, method.ReflectedType, methodInvoker, promise, cancellationToken);
    }

    /// <summary>
    /// Obtains an instance of <paramref name="serviceType"/> from the <paramref name="serviceProvider"/>, and passes it to the <paramref name="methodInvoker"/>, parameterized with the remaining arguments.
    /// </summary>
    private static Task ActivateAndInvokeAsync(IServiceProvider serviceProvider, Type serviceType, MethodInvoker methodInvoker, Promise promise, CancellationToken cancellationToken)
    {
        var instance = serviceProvider.GetRequiredService(serviceType);
        var result = (Task)methodInvoker.Invoke(obj: instance, promise, cancellationToken)!;
        return result;
    }

    /// <summary>
    /// Returns the <see cref="MethodInfo"/> represented by the given <paramref name="methodExpression"/>.
    /// </summary>
    /// <param name="methodExpression">A method on <typeparamref name="TService"/>, e.g. (MyUseCase useCase) => useCase.SendEmailAsync.</param>
    private static MethodInfo ExtractMethodInfo<TService>(Expression<Func<TService, Func<Promise, CancellationToken, Task>>> methodExpression)
    {
        var (method, parameter) = methodExpression switch
        {
            // Option A: Compiler wrapped the MethodInfo in a call to methodInfo.CreateDelegate
            {
                Body: UnaryExpression
                {
                    NodeType: ExpressionType.Convert,
                    Operand: MethodCallExpression
                    {
                        Method.Name: "CreateDelegate",
                        Arguments:
                        [
                            ConstantExpression
                        {
                            Value: Type,
                        },
                            ParameterExpression param,
                        ],
                        Object: ConstantExpression
                        {
                            Value: MethodInfo methodInfo
                        },
                    },
                },
            } => (methodInfo, param),

            // Option B: Compiler wrapped the MethodInfo in a call to methodInfo.CreateDelegate
            {
                Body: UnaryExpression
                {
                    NodeType: ExpressionType.Convert,
                    Operand: MethodCallExpression
                    {
                        Method.Name: "CreateDelegate",
                        Arguments:
                        [
                            ConstantExpression
                        {
                            Value: MethodInfo methodInfo
                        },
                            ParameterExpression param,
                        ],
                    },
                },
            } => (methodInfo, param),

            // Option C: A plain member expression leading to the MethodInfo
            {
                Body: MemberExpression
                {
                    Member: MethodInfo methodInfo,
                    Expression: ParameterExpression param,
                }
            } => (methodInfo, param),

            // Unsuited
            _ => (null, null),
        };

        if (method is null || parameter is null || parameter.Type != typeof(TService))
            ThrowNotAMethodReferenceExpression();

        return method;
    }

    [DoesNotReturn]
    private static void ThrowNotAMethodReferenceExpression()
    {
        throw new InvalidOperationException($"Expected an expression referencing a method, in the form of '(MyUseCase useCase) => useCase.SendEmailAsync', where the method satisfies Func<Promise, CancellationToken, Task>.");
    }

    /// <summary>
    /// <para>
    /// Executes the action annotated by the <see cref="IdempotentPromiseFulfillerAttribute"/> matching the given <see cref="Promise.ActionName"/>.
    /// </para>
    /// <para>
    /// Throws if no matching attribute exists.
    /// </para>
    /// </summary>
    public static Task ExecuteActionAsync(IServiceProvider serviceProvider, Promise promise, CancellationToken cancellationToken)
    {
        if (!DelegatesByActionName.TryGetValue(promise.ActionName, out var action))
            throw new InvalidOperationException($"No {nameof(IdempotentPromiseFulfillerAttribute)} exists with action name '{promise.ActionName}'. Was one of the names inadvertently changed?");

        return action(serviceProvider, promise, cancellationToken);
    }

    /// <summary>
    /// Returns the <see cref="IdempotentPromiseFulfillerAttribute"/> annotating the method described by the given <paramref name="methodExpression"/>, if any.
    /// </summary>
    /// <param name="methodExpression">A method on <typeparamref name="TService"/>, e.g. (MyUseCase useCase) => useCase.SendEmailAsync.</param>
    public static IdempotentPromiseFulfillerAttribute? GetAttributeForMethod<TService>(Expression<Func<TService, Func<Promise, CancellationToken, Task>>> methodExpression)
    {
        var method = ExtractMethodInfo(methodExpression);
        var result = AttributeCache<TService>.GetAttributeForMethod(method);
        return result;
    }
}

/// <summary>
/// Caches <see cref="IdempotentPromiseFulfillerAttribute"/> objects for methods on type <typeparamref name="T"/>, per <see cref="MethodInfo"/>.
/// </summary>
file static class AttributeCache<T>
{
    private static object? _cachedValues;

    public static IdempotentPromiseFulfillerAttribute? GetAttributeForMethod(MethodInfo method)
    {
        // Hot path: a single annotated method per containing type
        if (_cachedValues is Tuple<MethodInfo, IdempotentPromiseFulfillerAttribute?> singleResult && singleResult.Item1 == method)
            return singleResult.Item2;

        // Uncommon path: a few annotated methods per containing type
        if (_cachedValues is KeyValuePair<MethodInfo, IdempotentPromiseFulfillerAttribute?>[] multiResult)
        {
            foreach (var pair in multiResult)
                if (pair.Key == method)
                    return pair.Value;
        }

        // Cold path: first time seeing the method
        Populate(method);
        return GetAttributeForMethod(method); // Recurse
    }

    /// <summary>
    /// Populates the cache with the given 
    /// </summary>
    private static void Populate(MethodInfo method)
    {
        var attribute = method.GetCustomAttribute<IdempotentPromiseFulfillerAttribute>(inherit: false);
        var spinWait = new SpinWait();

        object? cachedValues;
        object newCachedValues;
        do
        {
            // Avoid overeager attempts in the face of concurrency conflicts
            spinWait.SpinOnce();

            cachedValues = _cachedValues;

            if (cachedValues is null)
            {
                newCachedValues = new Tuple<MethodInfo, IdempotentPromiseFulfillerAttribute?>(method, attribute);
            }
            else if (cachedValues is Tuple<MethodInfo, IdempotentPromiseFulfillerAttribute?> tuple)
            {
                if (tuple.Item1 == method)
                    return;

                newCachedValues = new[] { KeyValuePair.Create(tuple.Item1, tuple.Item2), KeyValuePair.Create(method, attribute), };
            }
            else if (cachedValues is KeyValuePair<MethodInfo, IdempotentPromiseFulfillerAttribute?>[] array)
            {
                foreach (var pair in array)
                    if (pair.Key == method)
                        return;

                newCachedValues = (KeyValuePair<MethodInfo, IdempotentPromiseFulfillerAttribute?>[])
                    [.. array, KeyValuePair.Create(method, attribute)];
            }
            else
            {
                throw new InvalidOperationException("This case should have been unreachable.");
            }
        } while (Interlocked.CompareExchange(ref _cachedValues, value: newCachedValues, comparand: cachedValues) != cachedValues);
    }
}
