using System.Collections.Generic;

namespace UniGame.AddressableAtlases
{
    public interface IAddressableAtlasesState
    {
        IReadOnlyList<string> AtlasTags { get; }
    }
}