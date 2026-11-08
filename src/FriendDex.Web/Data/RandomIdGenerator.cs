using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace FriendDex.Web.Data;

// MongoDB doesn't auto-number int IDs, so the app picks a random positive int when a row is added.
// With 2 billion possible values, two friends getting the same ID is practically impossible for a personal app.
public class RandomIdGenerator : ValueGenerator<int>
{
    public override bool GeneratesTemporaryValues => false;

    public override int Next(EntityEntry entry) => Random.Shared.Next(1, int.MaxValue);
}
