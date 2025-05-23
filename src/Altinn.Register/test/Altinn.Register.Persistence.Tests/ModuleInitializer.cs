using System.Runtime.CompilerServices;
using System.Text.Json;
using Altinn.Register.Core.ModelUtils;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.Persistence.Tests;

public static class ModuleInitializer
{
    private static readonly FlagsEnumModel<PartyFieldIncludes> _includesModel = FlagsEnumModel.Create<PartyFieldIncludes>(JsonNamingPolicy.KebabCaseLower, StringComparison.Ordinal);
    private static readonly FlagsEnumModel<PartyQueryFilters> _filtersModel = FlagsEnumModel.Create<PartyQueryFilters>(JsonNamingPolicy.KebabCaseLower, StringComparison.Ordinal);

    private static int _initialized = 0;

    [ModuleInitializer]
    public static void Init()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            VerifierSettings.NameForParameter<PartyFieldIncludes>(_includesModel.Format);
            VerifierSettings.NameForParameter<PartyQueryFilters>(_filtersModel.Format);

            VerifierSettings.UseSplitModeForUniqueDirectory();
            UseProjectRelativeDirectory("Snapshots");
        }
    }
}
