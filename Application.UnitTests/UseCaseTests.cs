using Rtl.News.RtlPoc.Application.Shared;

namespace Rtl.News.RtlPoc.Application.UnitTests;

public sealed class UseCaseTests
{
    [Fact]
    public void UseCaseClasses_Always_ShouldInheritFromAbstractUseCase()
    {
        var useCaseClasses = typeof(ApplicationRegistrationExtensions).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("UseCase") && type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition);

        foreach (var useCaseClass in useCaseClasses)
        {
            var hasUseCaseBaseType = HasBaseClass(
                useCaseClass,
                baseClass =>
                    // Either UseCase or UseCase<TResult>
                    baseClass == typeof(UseCase) ||
                    (baseClass.IsGenericType && baseClass.GetGenericTypeDefinition() == typeof(UseCase<>)));

            Assert.True(hasUseCaseBaseType, $"{useCaseClass.Name} should inherit from {nameof(UseCase)} or {nameof(UseCase)}<TResult>.");
        }
    }

    private static bool HasBaseClass(Type type, Func<Type, bool> predicate)
    {
        var baseType = type;
        while ((baseType = baseType!.BaseType) is not null)
        {
            if (predicate(baseType))
                return true;
        }

        return false;
    }
}
