using Supabase.Postgrest;
using Supabase.Postgrest.Models;

public static class SupabaseQueryExtensions
{
    /// <summary>
    /// Fetches every row of a table. Supabase caps a single request at 1000 rows,
    /// so this pages through (ordered by Id for stable paging) until done.
    /// </summary>
    public static async Task<List<T>> GetAllAsync<T>(this Supabase.Client client)
        where T : BaseModel, new()
    {
        const int pageSize = 1000;
        var all = new List<T>();

        while (true)
        {
            var page = (await client.From<T>()
                .Order("Id", Constants.Ordering.Ascending)
                .Range(all.Count, all.Count + pageSize - 1)
                .Get()).Models;
            all.AddRange(page);
            if (page.Count < pageSize) break;
        }

        return all;
    }
}
