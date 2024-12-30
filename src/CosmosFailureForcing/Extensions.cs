using Microsoft.Azure.Cosmos;

public static class Extensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this FeedIterator<T> iterator)
    {
        while (iterator.HasMoreResults)
        {
            var results = await iterator.ReadNextAsync();
            foreach (var resource in results.Resource)
            {
                yield return resource;
            }
        }
    }
}
