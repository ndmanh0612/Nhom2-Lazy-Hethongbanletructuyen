using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-24 / N2LHTBLTT-38: Test host dùng PostgreSQL thật (docker `phonestore-postgres`).
///
/// Một database riêng (tên sinh ngẫu nhiên) được tạo trước khi chạy và xoá sau khi chạy.
/// Migration được áp dụng TRƯỚC khi host khởi động, vì Program.cs seed dữ liệu
/// ngay khi app chạy ở môi trường Development.
///
/// Vì connection string được override bằng biến môi trường (global), factory này
/// phải được dùng chung cho toàn bộ test class — xem <see cref="ApiTestCollection"/>.
/// </summary>
public class PhoneStoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private readonly string _testConnectionString;

    public PhoneStoreApiFactory()
    {
        _databaseName = $"phonestore_test_{Guid.NewGuid():N}";
        _adminConnectionString = Environment.GetEnvironmentVariable("PHONESTORE_TEST_ADMIN_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";
        _testConnectionString =
            $"Host=localhost;Port=5433;Database={_databaseName};Username=postgres;Password=postgres";

        // Program.cs đọc connection string NGAY khi đăng ký DbContext, tức là trước khi
        // ConfigureWebHost của WebApplicationFactory được áp dụng. Vì vậy phải override
        // qua biến môi trường (Configuration mặc định đọc EnvironmentVariables).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _testConnectionString);
    }

    public string DatabaseName => _databaseName;

    async Task IAsyncLifetime.InitializeAsync()
    {
        await CreateDatabaseAsync();
        await DatabaseMigrator.MigrateAsync(_testConnectionString);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Giải phóng host trước, rồi xoá database.
        await base.DisposeAsync();
        await DropDatabaseAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        await using var connection = new Npgsql.NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

/// <summary>
/// Collection dùng chung để toàn bộ integration test dùng một host và một database,
/// tránh việc các factory khác nhau ghi đè connection string toàn cục.
/// </summary>
[CollectionDefinition(ApiTestCollection.Name)]
public sealed class ApiTestCollection : ICollectionFixture<PhoneStoreApiFactory>
{
    public const string Name = "PhoneStore API";
}
