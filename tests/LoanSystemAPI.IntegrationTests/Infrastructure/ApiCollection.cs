namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    [CollectionDefinition(Name)]
    public class ApiCollection : ICollectionFixture<ApiFixture>
    {
        public const string Name = "Api";
    }
}
