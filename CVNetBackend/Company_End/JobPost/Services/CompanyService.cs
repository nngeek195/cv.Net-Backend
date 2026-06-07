using Npgsql;

namespace CVNetBackend.Services;

public class CompanyService
{
    private readonly DatabaseService _db;
    public CompanyService(DatabaseService db) => _db = db;

    public async Task DeleteCompany(Guid companyId)
    {
        using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try {
            // 1. Delete associated locations
            var locCmd = new NpgsqlCommand("DELETE FROM public.company_locations WHERE company_id = @id", conn, trans);
            locCmd.Parameters.AddWithValue("id", companyId);
            await locCmd.ExecuteNonQueryAsync();

            // 2. Delete company
            var compCmd = new NpgsqlCommand("DELETE FROM public.companies WHERE id = @id", conn, trans);
            compCmd.Parameters.AddWithValue("id", companyId);
            await compCmd.ExecuteNonQueryAsync();

            await trans.CommitAsync();
        } catch {
            await trans.RollbackAsync();
            throw;
        }
    }
}