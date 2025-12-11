using System.Collections.Immutable;
using System.Reflection;
using Xunit.Sdk;

namespace Altinn.Register.Persistence.Tests.Utils;

public class EnumMembersDataAttribute<TEnum>
    : DataAttribute
    where TEnum : struct, Enum
{
    private static readonly ImmutableArray<TEnum> _values = [..Enum.GetValues<TEnum>()];

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        foreach (var value in _values)
        {
            yield return [value];
        }
    }
}
