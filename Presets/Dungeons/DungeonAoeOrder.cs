namespace MasterPlayer.Mechanics.Dungeons;

internal static class DungeonAoeOrder
{
    internal static void Sort<T>(List<T> group, Func<T, DateTime> activation) =>
        Sort(group, activation, 0, group.Count - 1);

    private static void Sort<T>(List<T> group, Func<T, DateTime> activation, int left, int right)
    {
        // Preserve the source's right-pivot sort, including equal-clock ordering.
        while (left < right)
        {
            var pivot = activation(group[right]);
            var store = left;
            for (var i = left; i < right; ++i)
                if (activation(group[i]) < pivot)
                {
                    (group[store], group[i]) = (group[i], group[store]);
                    ++store;
                }
            (group[store], group[right]) = (group[right], group[store]);
            if (store - left < right - store)
            {
                Sort(group, activation, left, store - 1);
                left = store + 1;
            }
            else
            {
                Sort(group, activation, store + 1, right);
                right = store - 1;
            }
        }
    }
}
