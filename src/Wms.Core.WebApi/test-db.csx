#r "Microsoft.Data.SqlClient"

using Microsoft.Data.SqlClient;

var connStr = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=WmsDb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
try {
    using var conn = new SqlConnection(connStr);
    conn.Open();
    Console.WriteLine("Connection successful!");
}
catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
}
